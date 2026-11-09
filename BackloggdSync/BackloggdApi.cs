using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Playnite.SDK;

namespace BackloggdSync
{
    public class GameHit
    {
        public int Id { get; set; }
        public string Slug { get; set; }
        public string Title { get; set; }
        public string Year { get; set; }

        public override string ToString() { return string.IsNullOrEmpty(Year) ? Title : Title + " (" + Year + ")"; }
    }

    public class DayResult
    {
        public int BaseMinutes { get; set; }
        public int TargetMinutes { get; set; }
        public bool Changed { get; set; }
        public bool Verified { get; set; }
    }

    /// <summary>Operações do Backloggd confirmadas pela captura (busca, status, nota e sessões por dia).</summary>
    public partial class BackloggdApi
    {
        /// <summary>Nome do Log criado pelo plugin no diário; sessões de outros Logs nunca são alteradas.</summary>
        public const string PlaythroughTitle = "Playnite";

        private readonly BackloggdWeb _web;
        private readonly PluginSettings _settings;
        private readonly ILogger _logger;

        public BackloggdApi(BackloggdWeb web, PluginSettings settings, ILogger logger)
        {
            _web = web;
            _settings = settings;
            _logger = logger;
        }

        private static string Snip(string s)
        {
            var t = (s ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            return t.Length > 160 ? t.Substring(0, 160) + "..." : t;
        }

        private static string Enc(string s) { return Uri.EscapeDataString(s ?? ""); }

        private static string Form(IEnumerable<KeyValuePair<string, string>> fields)
        {
            return string.Join("&", fields.Select(kv => Enc(kv.Key) + "=" + Enc(kv.Value)));
        }

        // ---------------------------------------------------------------- sessão

        /// <summary>Nome do usuário logado, ou null se não houver login. Também tenta descobrir o id numérico da conta.</summary>
        /// <summary>Por que o último GetLoggedUserAsync devolveu null: "loggedout" (botão Log In), "unknownpage" (página não reconhecida) ou "".</summary>
        public string LoginProblem { get; private set; } = "";

        public async Task<string> GetLoggedUserAsync()
        {
            LoginProblem = "";
            WebResult home = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                home = await _web.SendAsync("GET", "/", null, true);
                if (home.Ok) break;

                _logger.Warn("[BLS] Home page check failed: status " + home.Status + ", url '" + home.Url + "', text: " + Snip(home.Text));
                if (attempt == 0) await _web.ReopenAsync();
            }
            if (home == null || !home.Ok) return null;

            var html = home.Text ?? "";

            // Sem login a barra do site mostra o botão "Log In".
            if (Regex.IsMatch(html, ">\\s*Log In\\s*<", RegexOptions.IgnoreCase))
            {
                _logger.Info("[BLS] The site shows the Log In button: the Backloggd session is not signed in (page " + html.Length + " chars, url '" + home.Url + "').");
                LoginProblem = "loggedout";
                return null;
            }
            if (html.IndexOf("nav-bar-search", StringComparison.OrdinalIgnoreCase) < 0)
            {
                _logger.Warn("[BLS] Home page loaded but the expected navigation bar was not found (" + html.Length + " chars, url '" + home.Url + "'): " + Snip(html));
                LoginProblem = "unknownpage";
                return null;
            }

            var m = Regex.Match(html, "href=[\"']/u/([^/\"']+)/?[\"'][^>]*>(?:\\s|<[^>]+>)*Profile", RegexOptions.IgnoreCase);
            var user = m.Success ? m.Groups[1].Value : (!string.IsNullOrEmpty(_settings.Username) ? _settings.Username : "Backloggd");

            TryReadUserId(html);
            return user;
        }

        private bool TryReadUserId(string html)
        {
            if (string.IsNullOrEmpty(html)) return false;

            var patterns = new[]
            {
                "/api/user/(\\d+)/",
                "data-user-id=[\"'](\\d+)[\"']",
                "current[_-]?user[_-]?id[\"']?\\s*[:=]\\s*[\"']?(\\d+)",
                "user[_-]id[\"']?\\s*[:=]\\s*[\"']?(\\d+)"
            };

            foreach (var p in patterns)
            {
                var m = Regex.Match(html, p, RegexOptions.IgnoreCase);
                if (!m.Success) continue;

                int id;
                if (int.TryParse(m.Groups[1].Value, out id) && id > 0)
                {
                    _settings.UserId = id;
                    return true;
                }
            }

            return false;
        }

        private async Task EnsureUserIdAsync(int gameId, string slug)
        {
            if (_settings.UserId > 0) return;

            var pages = new List<string>();
            if (!string.IsNullOrEmpty(slug)) pages.Add("/games/" + slug + "/");
            if (!string.IsNullOrEmpty(_settings.Username)) pages.Add("/u/" + _settings.Username + "/");

            foreach (var page in pages)
            {
                var r = await _web.SendAsync("GET", page, null, true);
                if (r.Ok && TryReadUserId(r.Text)) return;
            }

            var editor = await _web.SendAsync("POST", "/render-editor/" + gameId, "editor_type=quick");
            if (editor.Ok && TryReadUserId(editor.Text)) return;

            throw new InvalidOperationException("Could not find your Backloggd numeric user id. Type it in the plugin settings.");
        }

        // ---------------------------------------------------------------- busca

        public async Task<List<GameHit>> SearchAsync(string query)
        {
            var list = new List<GameHit>();
            if (string.IsNullOrWhiteSpace(query)) return list;

            var r = await _web.SendAsync("GET", "/autocomplete.json?filter_editions=true&query=" + Enc(query.Trim()));
            if (!r.Ok) return list;

            var root = JObject.Parse(r.Text);
            var sug = root["suggestions"] as JArray;
            if (sug == null) return list;

            foreach (var s in sug)
            {
                var d = s["data"];
                if (d == null) continue;
                list.Add(new GameHit
                {
                    Id = (int?)d["id"] ?? 0,
                    Slug = (string)d["slug"],
                    Title = (string)d["title"] ?? (string)s["value"],
                    Year = (string)d["year"]
                });
            }

            return list.Where(h => h.Id > 0).ToList();
        }

        public static string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var d = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in d)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- estado do jogo

        /// <summary>Estado completo do jogo para o seu usuário: flags, nota e todos os Logs com as sessões por dia.</summary>
        public async Task<JObject> GetLogAsync(int gameId)
        {
            var r = await _web.SendAsync("GET", "/log/edit/" + gameId + "?is_native=false");
            if (!r.Ok) throw new InvalidOperationException("GET /log/edit/" + gameId + " returned " + r.Status + ". Are you logged in?");

            var root = JObject.Parse(r.Text);
            if (root["playthroughs"] == null) root["playthroughs"] = new JObject();
            return root;
        }

        private static bool Flag(JObject gl, string name)
        {
            if (gl == null) return false;
            var t = gl[name];
            return t != null && t.Type == JTokenType.Boolean && (bool)t;
        }

        /// <summary>
        /// Status de lista do site ("play" = Played, "playing" ou "backlog"). Cada um é um botão independente que alterna
        /// a cada POST, então só enviamos quando o estado atual ainda não é o desejado. Nada é desligado nem a Wishlist tocada.
        /// </summary>
        public async Task<bool> ApplyStatusAsync(int gameId, string wanted)
        {
            if (wanted != "play" && wanted != "playing" && wanted != "backlog") return false;

            var root = await GetLogAsync(gameId);
            var gl = root["game_log"] as JObject;
            if (Flag(gl, "is_" + wanted)) return false;

            var r = await _web.SendAsync("POST", "/log/", "type=" + wanted + "&game_id=" + gameId);
            if (!r.Ok) throw new InvalidOperationException("POST /log/ (" + wanted + ") returned " + r.Status);
            return true;
        }

        /// <summary>Nota de 1 a 10 (meia estrela = 1). Só envia se for diferente da atual.</summary>
        public async Task<bool> ApplyRatingAsync(int gameId, int rating)
        {
            if (rating < 1 || rating > 10) return false;

            var root = await GetLogAsync(gameId);
            var gl = root["game_log"] as JObject;
            int current = (gl != null && gl["rating"] != null && gl["rating"].Type != JTokenType.Null) ? (int)Math.Round((double)gl["rating"]) : 0;
            if (current == rating) return false;

            var r = await _web.SendAsync("POST", "/rate/" + gameId, "rating=" + rating);
            if (!r.Ok) throw new InvalidOperationException("POST /rate/" + gameId + " returned " + r.Status);
            return true;
        }

        // ---------------------------------------------------------------- diário

        private static int MinutesOf(JToken playDate)
        {
            double h = playDate["hours"] != null && playDate["hours"].Type != JTokenType.Null ? (double)playDate["hours"] : 0;
            double m = playDate["minutes"] != null && playDate["minutes"].Type != JTokenType.Null ? (double)playDate["minutes"] : 0;
            return (int)Math.Round(h * 60 + m);
        }

        private static JObject FindOurPlaythrough(JObject root)
        {
            var pts = root["playthroughs"] as JObject;
            if (pts == null) return null;

            foreach (var p in pts.Properties())
            {
                var o = p.Value as JObject;
                if (o != null && string.Equals((string)o["title"], PlaythroughTitle, StringComparison.Ordinal)) return o;
            }

            return null;
        }

        /// <summary>Minutos já registrados no dia, no Log do plugin (0 se não existir).</summary>
        public static int ReadDayMinutes(JObject root, DateTime day)
        {
            var mine = FindOurPlaythrough(root);
            var dates = mine == null ? null : mine["play_dates"] as JArray;
            if (dates == null) return 0;

            var key = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            foreach (var d in dates)
            {
                if ((string)d["range_start_date"] == key) return MinutesOf(d);
            }
            return 0;
        }

        private static string Str(JToken t)
        {
            if (t == null || t.Type == JTokenType.Null) return "";
            if (t.Type == JTokenType.Boolean) return (bool)t ? "true" : "false";
            if (t.Type == JTokenType.Float || t.Type == JTokenType.Integer)
                return Convert.ToString(((IConvertible)t.ToObject<double>()).ToDouble(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            return (string)t ?? "";
        }

        private static string Whole(JToken t)
        {
            if (t == null || t.Type == JTokenType.Null) return "";
            double d;
            return double.TryParse(t.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out d)
                ? ((long)Math.Round(d)).ToString(CultureInfo.InvariantCulture) : t.ToString();
        }

        /// <summary>
        /// Grava o tempo de um dia no Log "Playnite". Se absoluteTarget vier preenchido, o dia passa a ter exatamente
        /// esse valor (reenvio seguro); senão soma addMinutes ao que já existe. Depois relê o site e confere.
        /// </summary>
        public async Task<DayResult> UpsertDayAsync(int gameId, string slug, DateTime day, int addMinutes, int? absoluteTarget, int? platformId = null)
        {
            var root = await GetLogAsync(gameId);
            int baseMin = ReadDayMinutes(root, day);
            int target = absoluteTarget ?? (baseMin + Math.Max(0, addMinutes));
            var result = new DayResult { BaseMinutes = baseMin, TargetMinutes = target };

            if (target == baseMin)
            {
                result.Verified = true;
                return result;
            }

            var targets = new SortedDictionary<DateTime, int>();
            targets[day.Date] = target;
            var res = await UpsertDaysAsync(gameId, slug, targets, platformId);
            result.Changed = true;
            result.Verified = res.Verified;
            return result;
        }

        public class DaysResult
        {
            public int Changed { get; set; }
            public bool Verified { get; set; }
        }

        /// <summary>
        /// Grava vários dias de uma vez no Log "Playnite" (cada dia passa a ter exatamente o valor pedido). Dias que já
        /// estão certos são ignorados. Depois relê o site e confere todos.
        /// </summary>
        public async Task<DaysResult> UpsertDaysAsync(int gameId, string slug, SortedDictionary<DateTime, int> targets, int? platformId = null)
        {
            await EnsureUserIdAsync(gameId, slug);

            var root = await GetLogAsync(gameId);
            var gl = root["game_log"] as JObject ?? new JObject();
            var pts = root["playthroughs"] as JObject ?? new JObject();
            var mine = FindOurPlaythrough(root);

            // Só o que realmente muda.
            var todo = new SortedDictionary<DateTime, int>();
            int deltaTotal = 0;
            foreach (var kv in targets)
            {
                int cur = ReadDayMinutes(root, kv.Key);
                if (cur == kv.Value || kv.Value <= 0) continue;
                todo[kv.Key.Date] = kv.Value;
                deltaTotal += kv.Value - cur;
            }

            var result = new DaysResult { Changed = todo.Count, Verified = true };
            if (todo.Count == 0) return result;

            var firstDay = todo.Keys.First();
            var firstKey = firstDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            var f = new List<KeyValuePair<string, string>>();
            Action<string, string> add = (k, v) => f.Add(new KeyValuePair<string, string>(k, v ?? ""));

            add("game_id", gameId.ToString(CultureInfo.InvariantCulture));

            string pid;
            var dates = new List<JObject>();
            bool firstEver = true;

            if (mine != null)
            {
                pid = Whole(mine["id"]);
                var keys = new[]
                {
                    "id", "created_at", "updated_at", "rating", "review", "review_spoilers", "medium_id", "storefront_id",
                    "platform", "played_platform", "sync_sessions", "hours_played", "mins_played", "hours_finished",
                    "mins_finished", "hours_mastered", "mins_mastered", "is_master", "is_replay", "title", "start_date",
                    "finish_date", "edition_id", "edition_type"
                };
                foreach (var k in keys)
                {
                    var val = Str(mine[k]);
                    // Plataforma do site: só preenche se o Log ainda não tem uma (nunca troca a que você escolheu).
                    if (k == "platform" && platformId.HasValue && string.IsNullOrEmpty(val)) val = platformId.Value.ToString(CultureInfo.InvariantCulture);
                    add("playthroughs[0][" + k + "]", val);
                }

                var existing = mine["play_dates"] as JArray;
                if (existing != null)
                {
                    foreach (var d in existing) dates.Add((JObject)d);
                    firstEver = existing.Count == 0;
                }
            }
            else
            {
                pid = "-1";
                bool hasOthers = pts.Count > 0;
                add("playthroughs[0][id]", "-1");
                add("playthroughs[0][title]", PlaythroughTitle);
                add("playthroughs[0][rating]", "NaN"); // o próprio site envia NaN quando o Log não tem nota
                add("playthroughs[0][sync_sessions]", "true");
                add("playthroughs[0][review]", "");
                add("playthroughs[0][review_spoilers]", "false");
                add("playthroughs[0][platform]", platformId.HasValue ? platformId.Value.ToString(CultureInfo.InvariantCulture) : "");
                add("playthroughs[0][hours_played]", "");
                add("playthroughs[0][mins_played]", "");
                add("playthroughs[0][is_master]", hasOthers ? "false" : "true");
                add("playthroughs[0][is_replay]", hasOthers ? "true" : "false");
                add("playthroughs[0][start_date]", firstKey);
                add("playthroughs[0][finish_date]", "");
                add("playthroughs[0][edition_id]", "");
                add("playthroughs[0][edition_type]", "");
                add("playthroughs[0][medium_id]", "");
                add("playthroughs[0][played_platform]", "");
                add("playthroughs[0][storefront_id]", "");
            }

            // Sessões: reenvia as que já existem (sem editar) e grava/atualiza as pedidas.
            int idx = 0;
            var seen = new HashSet<DateTime>();
            foreach (var d in dates)
            {
                var startStr = (string)d["range_start_date"];
                DateTime dd;
                bool hit = DateTime.TryParseExact(startStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dd) && todo.ContainsKey(dd.Date);
                if (hit) seen.Add(dd.Date);
                int minutes = hit ? todo[dd.Date] : MinutesOf(d);
                WriteDate(add, pid, idx++, Whole(d["id"]), startStr, (string)d["range_end_date"], hit,
                    d["status"] == null || d["status"].Type == JTokenType.Null ? "" : Whole(d["status"]), Str(d["note"]), minutes,
                    Str(d["start_date"]), Str(d["finish_date"]), Str(d["privacy"]));
            }

            int newId = -1;
            foreach (var kv in todo)
            {
                if (seen.Contains(kv.Key)) continue;
                var k1 = kv.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var k2 = kv.Key.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                bool isFirst = firstEver && mine == null && kv.Key == firstDay;
                WriteDate(add, pid, idx++, newId.ToString(CultureInfo.InvariantCulture), k1, k2, true, "5", "", kv.Value, isFirst ? k1 : "", "", "");
                newId--;
            }

            int oldTotal = (int)Math.Round(DoubleOr(gl["total_hours"]) * 60 + DoubleOr(gl["total_minutes"]));
            int newTotal = Math.Max(0, oldTotal + deltaTotal);

            add("log[id]", Whole(gl["id"]));
            add("log[status]", Str(gl["status"]));
            add("log[rating]", Whole(gl["rating"]));
            add("log[is_play]", Str(gl["is_play"]));
            add("log[is_playing]", Str(gl["is_playing"]));
            add("log[is_backlog]", Str(gl["is_backlog"]));
            add("log[is_wishlist]", Str(gl["is_wishlist"]));
            add("log[game_liked]", Str(gl["game_liked"]));
            add("log[time_source]", Whole(gl["time_source"]));
            add("log[total_hours]", (newTotal / 60).ToString(CultureInfo.InvariantCulture));
            add("log[total_minutes]", (newTotal % 60).ToString(CultureInfo.InvariantCulture));
            add("log[avg_finish_time]", Whole(gl["avg_finish_time"]));
            add("log[avg_master_time]", Whole(gl["avg_master_time"]));
            add("log[override_cover_id]", Whole(gl["override_cover_id"]));
            add("log[last_edited_at]", Whole(gl["last_edited_at"]));
            add("modal_type", "quick");

            var post = await _web.SendAsync("POST", "/api/user/" + _settings.UserId + "/log/" + gameId, Form(f));
            if (!post.Ok) throw new InvalidOperationException("POST /api/user/.../log/" + gameId + " returned " + post.Status);

            // Confere relendo o site: nada de "achar que funcionou".
            var after = await GetLogAsync(gameId);
            var minePost = FindOurPlaythrough(after);
            if (platformId.HasValue && minePost != null)
                _logger.Info("[BLS] Platform sent: " + platformId.Value + ", site has: " + Str(minePost["platform"]));
            foreach (var kv in todo)
            {
                int now = ReadDayMinutes(after, kv.Key);
                if (now != kv.Value)
                {
                    result.Verified = false;
                    _logger.Warn("[BLS] Verification failed for game " + gameId + " on " + kv.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ": expected " + kv.Value + " min, site has " + now + " min.");
                }
            }

            return result;
        }

        private static double DoubleOr(JToken t)
        {
            if (t == null || t.Type == JTokenType.Null) return 0;
            double d;
            return double.TryParse(t.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out d) ? d : 0;
        }

        private static void WriteDate(Action<string, string> add, string pid, int i, string id, string start, string end, bool edited,
            string status, string note, int minutes, string startDate, string finishDate, string privacy)
        {
            var p = "dates[" + pid + "][" + i + "]";
            add(p + "[id]", id);
            add(p + "[range_start_date]", start);
            add(p + "[range_end_date]", end);
            add(p + "[edited]", edited ? "true" : "false");
            add(p + "[status]", status);
            add(p + "[note]", note);
            add(p + "[hours]", minutes >= 60 ? (minutes / 60).ToString(CultureInfo.InvariantCulture) : "");
            add(p + "[minutes]", (minutes % 60).ToString(CultureInfo.InvariantCulture));
            add(p + "[start_date]", startDate);
            add(p + "[finish_date]", finishDate);
            add(p + "[privacy]", privacy);
        }
    }
}
