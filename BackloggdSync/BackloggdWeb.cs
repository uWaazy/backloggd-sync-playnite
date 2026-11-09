using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.SDK;
using Playnite.SDK.Events;

namespace BackloggdSync
{
    public class WebResult
    {
        public int Status { get; set; }
        public string Text { get; set; } = "";
        public string Url { get; set; } = "";

        public bool Ok { get { return Status >= 200 && Status < 300; } }

        /// <summary>Proteção anti-robô, limite de requisições ou site fora do ar: o plugin para e avisa, não insiste.</summary>
        public bool Blocked
        {
            get
            {
                if (Status == 403 || Status == 429 || Status == 503) return true;
                var t = Text ?? "";
                return t.IndexOf("Just a moment", StringComparison.OrdinalIgnoreCase) >= 0
                    || t.IndexOf("cf-chl", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
    }

    /// <summary>Cookie guardado para restaurar a sessão depois de reiniciar o Playnite.</summary>
    public class SavedCookie
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string Domain { get; set; }
        public string Path { get; set; }
        public DateTime? Expires { get; set; }
    }

    public class BackloggdBlockedException : Exception
    {
        public BackloggdBlockedException(string message) : base(message) { }
    }

    /// <summary>
    /// Navegador interno (invisível) já posicionado em backloggd.com. As chamadas são feitas com fetch() dentro da
    /// própria página, então usam a sessão e o token CSRF do seu login, como o site faz. Nenhuma senha passa por aqui.
    /// </summary>
    public class BackloggdWeb : IDisposable
    {
        public const string BaseUrl = "https://backloggd.com";

        private readonly IPlayniteAPI _api;
        private readonly ILogger _logger;
        private readonly string _baseUrl;
        private IWebView _view;

        /// <summary>Arquivo (na pasta de dados do plugin) onde os cookies do Backloggd são guardados. Definido pelo plugin.</summary>
        public static string CookieFile;

        private DateTime _lastRequestUtc = DateTime.MinValue;

        public BackloggdWeb(IPlayniteAPI api, ILogger logger) : this(api, logger, BaseUrl) { }

        public BackloggdWeb(IPlayniteAPI api, ILogger logger, string baseUrl)
        {
            _api = api;
            _logger = logger;
            _baseUrl = baseUrl;
        }

        private static void RunOnUi(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess()) dispatcher.Invoke(action);
            else action();
        }

        private static async Task<bool> NavigateAsync(IWebView view, string url, int timeoutMs)
        {
            var tcs = new TaskCompletionSource<bool>();
            EventHandler<WebViewLoadingChangedEventArgs> handler = (s, e) =>
            {
                if (!e.IsLoading) tcs.TrySetResult(true);
            };

            view.LoadingChanged += handler;
            try
            {
                view.Navigate(url);
                var done = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
                return done == tcs.Task;
            }
            finally
            {
                try { view.LoadingChanged -= handler; } catch { }
            }
        }

        private static async Task<string> EvalStringAsync(IWebView view, string script)
        {
            dynamic res = await view.EvaluateScriptAsync(script);
            return res?.Result as string;
        }

        /// <summary>Fecha e abre de novo o navegador interno (usado quando a página inicial não respondeu direito).</summary>
        public async Task ReopenAsync()
        {
            var v = _view;
            _view = null;
            if (v != null) { try { RunOnUi(() => v.Dispose()); } catch { } }
            await OpenAsync();
        }

        private static bool IsSiteDomain(string domain)
        {
            return domain != null && domain.IndexOf("backloggd.com", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Recoloca no navegador interno os cookies do Backloggd guardados (cookies de sessão somem ao fechar o Playnite).</summary>
        private void RestoreCookies()
        {
            try
            {
                if (string.IsNullOrEmpty(CookieFile) || !File.Exists(CookieFile)) return;
                var saved = JsonConvert.DeserializeObject<List<SavedCookie>>(File.ReadAllText(CookieFile));
                if (saved == null || saved.Count == 0) return;

                dynamic v = _view;
                var have = new HashSet<string>();
                foreach (dynamic c in v.GetCookies())
                {
                    string d = c.Domain;
                    if (IsSiteDomain(d)) have.Add((string)c.Name);
                }

                int restored = 0;
                foreach (var c in saved)
                {
                    if (have.Contains(c.Name)) continue;
                    var expires = c.Expires.HasValue && c.Expires.Value > DateTime.Now ? c.Expires.Value : DateTime.Now.AddDays(30);
                    v.SetCookies(_baseUrl, c.Domain ?? ".backloggd.com", c.Name, c.Value ?? "", c.Path ?? "/", expires);
                    restored++;
                }
                if (restored > 0) _logger.Info("[BLS] Restored " + restored + " saved cookie(s) into the internal browser.");
            }
            catch (Exception ex)
            {
                _logger.Warn("[BLS] Could not restore the saved session: " + ex.Message);
            }
        }

        /// <summary>Guarda os cookies atuais do Backloggd (chamado depois de confirmar que você está logado).</summary>
        public void SaveCookies()
        {
            try
            {
                if (_view == null || string.IsNullOrEmpty(CookieFile)) return;
                dynamic v = _view;
                var list = new List<SavedCookie>();
                foreach (dynamic c in v.GetCookies())
                {
                    string d = c.Domain;
                    if (!IsSiteDomain(d)) continue;
                    DateTime? exp = null;
                    try { exp = (DateTime?)c.Expires; } catch { }
                    list.Add(new SavedCookie { Name = c.Name, Value = c.Value, Domain = d, Path = c.Path, Expires = exp });
                }
                if (list.Count == 0) return;
                File.WriteAllText(CookieFile, JsonConvert.SerializeObject(list));
            }
            catch (Exception ex)
            {
                _logger.Warn("[BLS] Could not save the session cookies: " + ex.Message);
            }
        }

        /// <summary>Abre o navegador interno e espera a página inicial (e uma eventual verificação do site) terminar.</summary>
        public async Task OpenAsync()
        {
            if (_view != null) return;

            IWebView view = null;
            RunOnUi(() => { view = _api.WebViews.CreateOffscreenView(); });
            if (view == null) throw new InvalidOperationException("Could not create the internal browser.");
            _view = view;
            if (_baseUrl == BaseUrl) RestoreCookies();

            if (!await NavigateAsync(_view, _baseUrl + "/", 30000))
                throw new BackloggdBlockedException(_baseUrl + " did not load in time.");

            for (int i = 0; i < 12; i++)
            {
                var title = (await EvalStringAsync(_view, "document.title") ?? "").ToLowerInvariant();
                var challenge = title.Contains("just a moment") || title.Contains("checking your browser") || title.Contains("attention required");
                if (!challenge && title.Length > 0) { _logger.Info("[BLS] Internal browser ready (title: '" + title + "')."); return; }
                await Task.Delay(1500);
            }

            throw new BackloggdBlockedException(_baseUrl + " is showing a browser check (anti-bot).");
        }

        /// <summary>Envia uma requisição como a própria página faria. Espaça as chamadas para não sobrecarregar o site.</summary>
        public async Task<WebResult> SendAsync(string method, string path, string formBody = null, bool html = false)
        {
            try
            {
                return await SendOnceAsync(method, path, formBody, html);
            }
            catch (TimeoutException)
            {
                // Só leituras são repetidas: reenviar um POST poderia alternar um botão duas vezes.
                if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)) throw;
                _logger.Warn("[BLS] Timeout on GET " + path + "; trying once more.");
                return await SendOnceAsync(method, path, formBody, html);
            }
        }

        private async Task<WebResult> SendOnceAsync(string method, string path, string formBody, bool html)
        {
            if (_view == null) await OpenAsync();

            var wait = 500 - (int)(DateTime.UtcNow - _lastRequestUtc).TotalMilliseconds;
            if (wait > 0) await Task.Delay(wait);

            var id = Guid.NewGuid().ToString("N");
            var headers = new JObject();
            if (html)
            {
                headers["Accept"] = "text/html,application/xhtml+xml";
            }
            else
            {
                headers["Accept"] = "application/json, text/javascript, */*; q=0.01";
                headers["X-Requested-With"] = "XMLHttpRequest";
            }
            if (formBody != null) headers["Content-Type"] = "application/x-www-form-urlencoded; charset=UTF-8";

            var url = path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? path : _baseUrl + path;

            var script =
                "(function(){var t=(document.querySelector('meta[name=csrf-token]')||{}).content||'';" +
                "var h=" + headers.ToString(Formatting.None) + ";h['X-CSRF-Token']=t;" +
                "var o={method:" + JsonConvert.SerializeObject(method) + ",headers:h,credentials:'same-origin'};" +
                "var b=" + (formBody == null ? "null" : JsonConvert.SerializeObject(formBody)) + ";if(b!==null)o.body=b;" +
                "window.__blq=window.__blq||{};" +
                "fetch(" + JsonConvert.SerializeObject(url) + ",o).then(function(r){return r.text().then(function(x){window.__blq['" + id + "']={s:r.status,t:x,u:r.url};});})" +
                ".catch(function(e){window.__blq['" + id + "']={s:0,t:String(e),u:''};});return 'ok';})()";

            await _view.EvaluateScriptAsync(script);

            var read = "(window.__blq&&window.__blq['" + id + "'])?JSON.stringify(window.__blq['" + id + "']):''";
            for (int i = 0; i < 100; i++)
            {
                await Task.Delay(300);
                var json = await EvalStringAsync(_view, read);
                if (string.IsNullOrEmpty(json)) continue;

                try { await _view.EvaluateScriptAsync("delete window.__blq['" + id + "'];"); } catch { }
                _lastRequestUtc = DateTime.UtcNow;

                var o = JObject.Parse(json);
                var result = new WebResult
                {
                    Status = (int?)o["s"] ?? 0,
                    Text = (string)o["t"] ?? "",
                    Url = (string)o["u"] ?? ""
                };

                if (result.Blocked)
                    throw new BackloggdBlockedException("Backloggd returned " + result.Status + " (blocked or rate limited).");

                return result;
            }

            _lastRequestUtc = DateTime.UtcNow;
            throw new TimeoutException("Backloggd did not answer " + method + " " + path + ".");
        }

        public void Dispose()
        {
            var v = _view;
            _view = null;
            if (v == null) return;
            try { RunOnUi(() => v.Dispose()); } catch { }
        }
    }
}
