using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Playnite.SDK;

namespace BackloggdSync
{
    /// <summary>Exporta um relatório de diagnóstico com os registros [BLS] do extensions.log, mascarando caminhos pessoais e tokens.</summary>
    internal static class DiagnosticExporter
    {
        private static readonly Regex UserPathRx = new Regex(@"(?i)[A-Z]:\\Users\\[^\\/\r\n]+", RegexOptions.Compiled);
        private static readonly Regex SecretRx = new Regex(@"(?i)(cookie|csrf|x-csrf-token|token|authorization)\s*[:=]\s*[^\s;,&]+", RegexOptions.Compiled);
        private static readonly Regex LogStartRx = new Regex(@"^\d{2,4}[-/]\d{2}[-/]\d{2,4}\s+\d{2}:\d{2}", RegexOptions.Compiled);

        public static string BuildReport(IPlayniteAPI api, PluginSettings settings, SyncEngine engine, out int lineCount)
        {
            var sb = new StringBuilder();
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            sb.AppendLine("=== Backloggd Sync Diagnostics ===");
            sb.AppendLine("Plugin version: " + (ver != null ? ver.ToString(3) : "?"));
            sb.AppendLine("Playnite version: " + (api != null && api.ApplicationInfo != null ? api.ApplicationInfo.ApplicationVersion.ToString() : "?"));
            sb.AppendLine("Generated (UTC): " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine("Signed in: " + (!string.IsNullOrEmpty(settings.Username) ? "yes" : "no"));
            sb.AppendLine("Pending items: " + engine.PendingCount);
            sb.AppendLine("Linked games: " + settings.Links.Count);
            sb.AppendLine("Last problem: " + (string.IsNullOrEmpty(engine.LastProblem) ? "none" : engine.LastProblem));
            sb.AppendLine("Options: Auto=" + settings.AutoSyncOnStop +
                          ", Time=" + settings.SyncPlaytime +
                          ", Status=" + settings.SyncStatus +
                          ", Rating=" + settings.SyncRating +
                          ", MasterOn100%=" + settings.MasterOnFullAchievements);
            sb.AppendLine();

            var logPath = FindLogFile(api);
            var kept = new List<string>();
            if (!string.IsNullOrEmpty(logPath) && File.Exists(logPath))
            {
                kept = ExtractBlsLines(logPath, settings);
            }

            lineCount = kept.Count;
            sb.AppendLine("--- [BLS] log entries (" + lineCount + ") ---");
            foreach (var line in kept) sb.AppendLine(line);
            return sb.ToString();
        }

        private static string FindLogFile(IPlayniteAPI api)
        {
            if (api == null || api.Paths == null) return null;
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(api.Paths.ConfigurationPath))
                candidates.Add(Path.Combine(api.Paths.ConfigurationPath, "extensions.log"));
            if (!string.IsNullOrEmpty(api.Paths.ExtensionsDataPath))
            {
                var parent = Path.GetDirectoryName(api.Paths.ExtensionsDataPath.TrimEnd('\\', '/'));
                if (!string.IsNullOrEmpty(parent))
                    candidates.Add(Path.Combine(parent, "extensions.log"));
            }

            foreach (var c in candidates)
            {
                if (File.Exists(c)) return c;
            }
            return null;
        }

        private static List<string> ExtractBlsLines(string logPath, PluginSettings settings)
        {
            var result = new List<string>();
            bool inBlsStack = false;
            int stackBudget = 0;

            using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var sr = new StreamReader(fs, Encoding.UTF8))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.IndexOf("[BLS]", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        result.Add(Sanitize(line, settings));
                        inBlsStack = true;
                        stackBudget = 8;
                    }
                    else if (inBlsStack && stackBudget > 0 && !LogStartRx.IsMatch(line))
                    {
                        result.Add(Sanitize(line, settings));
                        stackBudget--;
                    }
                    else
                    {
                        inBlsStack = false;
                        stackBudget = 0;
                    }
                }
            }

            const int maxLines = 400;
            if (result.Count > maxLines)
                result = result.GetRange(result.Count - maxLines, maxLines);

            return result;
        }

        private static string Sanitize(string line, PluginSettings settings)
        {
            if (string.IsNullOrEmpty(line)) return "";
            var s = UserPathRx.Replace(line, "%USERPROFILE%");
            s = SecretRx.Replace(s, "$1=[REDACTED]");
            s = Regex.Replace(s, @"/api/user/\d+/", "/api/user/<userid>/", RegexOptions.IgnoreCase);
            if (settings != null && !string.IsNullOrEmpty(settings.Username))
                s = ReplaceIgnoreCase(s, settings.Username, "<user>");
            return s;
        }

        private static string ReplaceIgnoreCase(string input, string search, string replacement)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(search)) return input;
            return Regex.Replace(input, Regex.Escape(search), replacement, RegexOptions.IgnoreCase);
        }
    }
}
