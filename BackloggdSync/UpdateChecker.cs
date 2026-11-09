using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Playnite.SDK;

namespace BackloggdSync
{
    public class UpdateChecker
    {
        private const string RepoUrl = "https://api.github.com/repos/uWaazy/backloggd-sync-playnite/releases/latest";
        private const string UserAgent = "Playnite-BackloggdSync";

        private readonly IPlayniteAPI _api;
        private readonly ILogger _logger;
        private readonly PluginSettings _settings;

        public UpdateChecker(IPlayniteAPI api, ILogger logger, PluginSettings settings)
        {
            _api = api;
            _logger = logger;
            _settings = settings;
        }

        public class ReleaseInfo
        {
            public string Tag = "";
            public Version Version;
            public string DownloadUrl = "";
            public string FileName = "";
            public string Body = "";
        }

        private static Version ParseVersion(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;
            var m = Regex.Match(tag, @"(\d+)\.(\d+)\.(\d+)");
            if (!m.Success) return null;
            return new Version(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
        }

        public async Task<ReleaseInfo> CheckLatestReleaseAsync()
        {
            ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol | SecurityProtocolType.Tls12;

            var request = (HttpWebRequest)WebRequest.Create(RepoUrl);
            request.UserAgent = UserAgent;
            request.Accept = "application/vnd.github.v3+json";
            request.Timeout = 10000;

            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                var json = await reader.ReadToEndAsync();
                var root = JObject.Parse(json);

                var tag = (string)root["tag_name"] ?? "";
                var ver = ParseVersion(tag);
                if (ver == null) return null;

                var assets = root["assets"] as JArray;
                if (assets == null) return null;

                string downloadUrl = null;
                string fileName = null;
                foreach (var a in assets)
                {
                    var name = (string)a["name"] ?? "";
                    if (name.EndsWith(".pext", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = (string)a["browser_download_url"];
                        fileName = name;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl)) return null;

                return new ReleaseInfo
                {
                    Tag = tag,
                    Version = ver,
                    DownloadUrl = downloadUrl,
                    FileName = fileName,
                    Body = (string)root["body"] ?? ""
                };
            }
        }

        public void CheckForUpdates(bool manual)
        {
            Task.Run(async () =>
            {
                try
                {
                    var currentVer = typeof(BackloggdSyncPlugin).Assembly.GetName().Version;
                    var release = await CheckLatestReleaseAsync();
                    if (release == null)
                    {
                        if (manual) _api.Dialogs.ShowMessage(L.T("update.noupdate", currentVer.ToString(3)), "Backloggd Sync");
                        return;
                    }

                    var current3 = new Version(currentVer.Major, currentVer.Minor, Math.Max(0, currentVer.Build));
                    if (release.Version > current3)
                    {
                        _logger.Info(string.Format("[BLS] New version found: {0} (current is {1})", release.Tag, current3));

                        _api.Notifications.Add(new NotificationMessage(
                            "BLS-update",
                            L.T("notify.update", release.Tag),
                            NotificationType.Info,
                            () => PromptAndInstall(release)));

                        if (manual) PromptAndInstall(release);
                    }
                    else if (manual)
                    {
                        _api.Dialogs.ShowMessage(L.T("update.noupdate", current3.ToString(3)), "Backloggd Sync");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn("[BLS] Update check failed: " + ex.Message);
                    if (manual) _api.Dialogs.ShowErrorMessage(L.T("update.failed", ex.Message), "Backloggd Sync");
                }
            });
        }

        private void PromptAndInstall(ReleaseInfo release)
        {
            var res = _api.Dialogs.ShowMessage(
                L.T("update.confirm", release.Tag),
                "Backloggd Sync",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (res != System.Windows.MessageBoxResult.Yes) return;

            string destFile = null;
            string error = null;

            _api.Dialogs.ActivateGlobalProgress(args =>
            {
                try
                {
                    args.Text = L.T("update.downloading", release.Tag);
                    var tempDir = Path.Combine(Path.GetTempPath(), "BackloggdSyncUpdate");
                    if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

                    destFile = Path.Combine(tempDir, release.FileName);
                    if (File.Exists(destFile)) File.Delete(destFile);

                    using (var client = new WebClient())
                    {
                        client.Headers[HttpRequestHeader.UserAgent] = UserAgent;
                        client.DownloadFile(release.DownloadUrl, destFile);
                    }

                    var fi = new FileInfo(destFile);
                    if (!fi.Exists || fi.Length < 1024)
                    {
                        throw new InvalidOperationException("Downloaded package is invalid or corrupted.");
                    }

                    byte[] header = new byte[4];
                    using (var fs = File.OpenRead(destFile))
                    {
                        fs.Read(header, 0, 4);
                    }
                    if (header[0] != 0x50 || header[1] != 0x4B || header[2] != 0x03 || header[3] != 0x04)
                    {
                        throw new InvalidOperationException("Downloaded package is not a valid Playnite extension.");
                    }
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
            }, new GlobalProgressOptions(L.T("update.downloading", release.Tag), false));

            if (!string.IsNullOrEmpty(error))
            {
                _api.Dialogs.ShowErrorMessage(L.T("update.failed", error), "Backloggd Sync");
                return;
            }

            if (string.IsNullOrEmpty(destFile) || !File.Exists(destFile)) return;

            try
            {
                var playniteExe = Process.GetCurrentProcess().MainModule.FileName;
                if (!File.Exists(playniteExe) || !playniteExe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    playniteExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Playnite", "Playnite.DesktopApp.exe");
                }

                _logger.Info("[BLS] Launching Playnite extension installer for " + destFile);
                var psi = new ProcessStartInfo
                {
                    FileName = playniteExe,
                    Arguments = string.Format("--installext \"{0}\"", destFile),
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[BLS] Failed to trigger installer: " + ex.Message);
                _api.Dialogs.ShowErrorMessage(L.T("update.failed", ex.Message), "Backloggd Sync");
            }
        }
    }
}
