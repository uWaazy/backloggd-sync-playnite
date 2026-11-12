using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace BackloggdSync
{
    /// <summary>
    /// Lê os arquivos do SuccessStory (ExtensionsData\cebe6d32-...\SuccessStory\&lt;id do jogo&gt;.json) e diz se TODAS as
    /// conquistas do jogo estão desbloqueadas. Formato lido de forma tolerante; sem arquivo ou sem conquistas = false.
    /// </summary>
    internal static class AchievementReader
    {
        public const string SuccessStoryId = "cebe6d32-8c46-4459-b993-5a5189d60788";

        public static bool IsComplete(string extensionsDataPath, Guid gameId)
        {
            try
            {
                var root = Path.Combine(extensionsDataPath, SuccessStoryId);
                if (!Directory.Exists(root)) return false;
                var file = Directory.GetFiles(root, gameId.ToString() + ".json", SearchOption.AllDirectories).FirstOrDefault();
                if (file == null) return false;

                var obj = JObject.Parse(File.ReadAllText(file));
                if (string.Equals((string)obj["IsIgnored"], "true", StringComparison.OrdinalIgnoreCase)) return false;
                var items = (obj["Items"] ?? obj["Achievements"] ?? obj["items"]) as JArray;
                if (items == null || items.Count == 0) return false;

                foreach (var t in items)
                {
                    var it = t as JObject;
                    if (it == null) return false;
                    if (!IsUnlocked(it)) return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsUnlocked(JObject it)
        {
            var flag = it["IsUnlock"];
            if (flag != null && flag.Type == JTokenType.Boolean) return (bool)flag;

            var d = it["DateUnlocked"] ?? it["DateWhenUnlocked"];
            if (d == null || d.Type == JTokenType.Null) return false;

            DateTime when;
            if (d.Type == JTokenType.Date) when = (DateTime)d;
            else if (!DateTime.TryParse((string)d, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out when)) return false;
            return when.Year > 1;
        }
    }
}
