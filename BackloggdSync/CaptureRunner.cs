using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.SDK;

namespace BackloggdSync
{
    /// <summary>Abre a janela do site para um passo, registra o que acontece nela e devolve o resultado já com segredos mascarados.</summary>
    public class CaptureRunner
    {
        private static readonly Regex[] RedactPatterns =
        {
            new Regex("(authenticity_token[^>]{0,80}?value=[\"'])[^\"']*", RegexOptions.IgnoreCase),
            new Regex("(csrf[-_]?token[\"']?\\s*(?:content=|[:=])\\s*[\"']?)[^\"'&\\s,}]{4,}", RegexOptions.IgnoreCase),
            new Regex("(bearer\\s+)[A-Za-z0-9\\-\\._~\\+/=]{8,}", RegexOptions.IgnoreCase),
            new Regex("((?:password|passwd|secret|session[_-]?id|cookie)[\"']?\\s*[:=]\\s*[\"']?)[^\"'&\\s,}]+", RegexOptions.IgnoreCase)
        };

        private readonly IPlayniteAPI _api;
        private readonly ILogger _logger;

        private volatile string _logJson = "[]";
        private readonly object _snapLock = new object();
        private readonly Dictionary<string, JObject> _snapshots = new Dictionary<string, JObject>();

        public CaptureRunner(IPlayniteAPI api, ILogger logger)
        {
            _api = api;
            _logger = logger;
        }

        /// <summary>Precisa ser chamado na thread da interface (a janela é modal).</summary>
        public JObject RunStep(CaptureStep step, int index, int total)
        {
            _logJson = "[]";
            lock (_snapLock) { _snapshots.Clear(); }

            var startedUtc = DateTime.UtcNow;
            var finalUrl = "";

            var banner = JsonConvert.SerializeObject(new
            {
                title = L.T("banner.title", index + 1, total, L.Pick(step.Title)),
                body = L.Pick(step.Instruction),
                hint = L.T("banner.hint")
            });
            var prefix = "window.__blBanner=" + banner + ";";

            var settings = new WebViewSettings
            {
                WindowWidth = 1200,
                WindowHeight = 820,
                JavaScriptEnabled = true
            };

            using (var view = _api.WebViews.CreateView(settings))
            {
                // O registro é lido por um temporizador na thread da interface; nada de usar a API do Playnite
                // dentro de eventos do navegador (isso pode travar o Playnite durante redirecionamentos).
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
                timer.Tick += (s, e) =>
                {
                    try
                    {
                        view.EvaluateScriptAsync(prefix + CaptureScript.Hook);

                        view.EvaluateScriptAsync("sessionStorage.getItem('__bl_log')||'[]'").ContinueWith(t =>
                        {
                            try
                            {
                                dynamic r = t.Result;
                                string text = r?.Result as string;
                                if (!string.IsNullOrEmpty(text) && text.Length > 2) _logJson = text;
                            }
                            catch { }
                        });

                        view.EvaluateScriptAsync("window.__blSnapshot?window.__blSnapshot():''").ContinueWith(t =>
                        {
                            try
                            {
                                dynamic r = t.Result;
                                string text = r?.Result as string;
                                if (string.IsNullOrEmpty(text)) return;
                                var snap = JObject.Parse(text);
                                var url = (string)snap["url"] ?? "";
                                lock (_snapLock)
                                {
                                    _snapshots[url] = snap;
                                    if (_snapshots.Count > 15) _snapshots.Remove(_snapshots.Keys.First());
                                }
                            }
                            catch { }
                        });

                        var address = view.GetCurrentAddress();
                        if (!string.IsNullOrEmpty(address)) finalUrl = address;
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug("[BLS] Captura: " + ex.Message);
                    }
                };

                view.Navigate(step.StartUrl);
                timer.Start();
                view.OpenDialog();
                timer.Stop();
            }

            return BuildResult(step, index, startedUtc, finalUrl);
        }

        private JObject BuildResult(CaptureStep step, int index, DateTime startedUtc, string finalUrl)
        {
            var entries = new JArray();
            try
            {
                var raw = JArray.Parse(_logJson);
                var notBefore = startedUtc.AddSeconds(-2);
                foreach (var e in raw.OfType<JObject>())
                {
                    DateTime t;
                    var stamp = (string)e["t"];
                    if (stamp != null && DateTime.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out t) && t < notBefore) continue;
                    entries.Add(e);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn("[BLS] Registro de captura ilegível: " + ex.Message);
            }

            var snaps = new JArray();
            lock (_snapLock)
            {
                foreach (var s in _snapshots.Values) snaps.Add(s);
            }

            var requestCount = entries.OfType<JObject>().Count(e =>
            {
                var k = (string)e["k"];
                return k == "fetch" || k == "xhr" || k == "form";
            });

            var result = new JObject
            {
                ["step"] = index + 1,
                ["id"] = step.Id,
                ["title"] = step.Title[1],
                ["startedUtc"] = startedUtc.ToString("o"),
                ["finishedUtc"] = DateTime.UtcNow.ToString("o"),
                ["finalUrl"] = finalUrl,
                ["requestCount"] = requestCount,
                ["entries"] = entries,
                ["snapshots"] = snaps
            };

            RedactTree(result);
            return result;
        }

        private static void RedactTree(JToken token)
        {
            if (token == null) return;

            var value = token as JValue;
            if (value != null)
            {
                var text = value.Value as string;
                if (text != null) value.Value = Redact(text);
                return;
            }

            foreach (var child in token.Children().ToList()) RedactTree(child);
        }

        public static string Redact(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            foreach (var pattern in RedactPatterns) text = pattern.Replace(text, "$1***");
            return text;
        }
    }
}
