using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace BackloggdSync
{
    /// <summary>Fila de envio ao Backloggd: vincula jogos, grava o tempo por dia e confere status e nota.</summary>
    public class SyncEngine
    {
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        private readonly object _lock = new object();

        private readonly IPlayniteAPI _api;
        private readonly PluginSettings _settings;
        private readonly ILogger _logger;

        public SyncEngine(IPlayniteAPI api, PluginSettings settings, ILogger logger)
        {
            _api = api;
            _settings = settings;
            _logger = logger;
        }

        public int PendingCount { get { lock (_lock) return _settings.Pending.Count; } }

        /// <summary>Definidos pelo plugin: abrir a janela de login / o vínculo de um jogo (usados pelas notificações clicáveis).</summary>
        public Action LoginRequested;
        public Action<Guid> LinkRequested;

        /// <summary>Motivo do último envio que não andou: "", "login", "unknown", "blocked", "stuck" ou "site".</summary>
        public string LastProblem { get; private set; } = "";

        private int _failStreak;

        /// <summary>Linha da lista de pendências nas configurações.</summary>
        public class PendingView
        {
            public PendingItem Item;
            public Guid GameId;
            public string GameName;
            public string Detail;
            public string Reason;
            public bool NeedsLink;
        }

        public List<PendingView> PendingViews()
        {
            var list = new List<PendingView>();
            List<PendingItem> items;
            lock (_lock) items = _settings.Pending.ToList();

            foreach (var p in items)
            {
                Guid gid;
                Guid.TryParse(p.GameId, out gid);
                var game = gid == Guid.Empty ? null : _api.Database.Games.Get(gid);

                var parts = new List<string>();
                if (p.AddMinutes > 0 || p.Target.HasValue)
                    parts.Add((string.IsNullOrEmpty(p.Day) ? "" : p.Day + ": ") + (p.Target.HasValue ? p.Target.Value : p.AddMinutes) + " min");
                else parts.Add(L.T("pend.statusonly"));

                string reason;
                var link = gid == Guid.Empty ? null : GetLink(gid);
                bool unlinked = link == null || link.BackloggdId <= 0;
                if (p.NeedsLink && unlinked) reason = L.T("reason.needlink");
                else if (!string.IsNullOrEmpty(p.LastError)) reason = L.T("reason.error", p.LastError);
                else if (LastProblem == "login" || LastProblem == "unknown") reason = L.T("reason.login");
                else if (LastProblem == "blocked") reason = L.T("reason.blocked");
                else if (LastProblem == "stuck") reason = L.T("reason.stuck");
                else if (LastProblem == "site") reason = L.T("reason.site");
                else reason = L.T("reason.waiting");

                list.Add(new PendingView
                {
                    Item = p,
                    GameId = gid,
                    GameName = game != null ? game.Name : "?",
                    Detail = string.Join(" ", parts),
                    Reason = reason,
                    NeedsLink = p.NeedsLink && unlinked
                });
            }

            return list.OrderByDescending(x => x.NeedsLink).ThenBy(x => x.GameName).ToList();
        }

        public void Discard(PendingItem item)
        {
            lock (_lock)
            {
                _settings.Pending.Remove(item);
                _settings.Save();
            }
        }

        // ---------------------------------------------------------------- fila

        public void Enqueue(Guid gameId, DateTime? day, int minutes)
        {
            var id = gameId.ToString();
            var dayKey = day.HasValue ? day.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";

            lock (_lock)
            {
                var existing = _settings.Pending.FirstOrDefault(p => p.GameId == id && p.Day == dayKey && p.Target == null);
                _logger.Info("[BLS] Queued: game " + id + " day '" + dayKey + "' +" + minutes + " min");
                if (existing != null) existing.AddMinutes += minutes;
                else _settings.Pending.Add(new PendingItem { GameId = id, Day = dayKey, AddMinutes = minutes });
                _settings.Save();
            }
        }

        /// <summary>Libera os itens parados por falta de vínculo para uma nova tentativa automática (ação manual do usuário).</summary>
        public void RetryUnlinked()
        {
            lock (_lock)
            {
                foreach (var p in _settings.Pending) p.NeedsLink = false;
                _settings.Save();
            }
        }

        public GameLink GetLink(Guid gameId)
        {
            GameLink l;
            return _settings.Links.TryGetValue(gameId.ToString(), out l) ? l : null;
        }

        public void SetLink(Guid gameId, GameHit hit)
        {
            lock (_lock)
            {
                GameLink l;
                if (!_settings.Links.TryGetValue(gameId.ToString(), out l)) l = new GameLink();
                if (l.BackloggdId != hit.Id) { l.LastStatus = ""; l.LastRating = 0; }
                l.BackloggdId = hit.Id;
                l.Slug = hit.Slug ?? "";
                l.Title = hit.Title ?? "";
                l.Ignored = false;
                _settings.Links[gameId.ToString()] = l;
                foreach (var p in _settings.Pending.Where(x => x.GameId == gameId.ToString())) p.NeedsLink = false;
                _settings.Save();
            }
        }

        public void SetIgnored(Guid gameId, bool ignored)
        {
            lock (_lock)
            {
                GameLink l;
                if (!_settings.Links.TryGetValue(gameId.ToString(), out l)) l = new GameLink();
                l.Ignored = ignored;
                _settings.Links[gameId.ToString()] = l;
                if (ignored) _settings.Pending.RemoveAll(p => p.GameId == gameId.ToString());
                _settings.Save();
            }
        }

        // ---------------------------------------------------------------- regras

        /// <summary>Nota do Playnite (0-100) para a escala do site (1-10), ou 0 quando não há nota.</summary>
        public static int RatingFor(Game g)
        {
            if (g == null || !g.UserScore.HasValue || g.UserScore.Value <= 0) return 0;
            var r = (int)Math.Round(g.UserScore.Value / 10.0, MidpointRounding.AwayFromZero);
            return Math.Max(1, Math.Min(10, r));
        }

        private static string StripAccents(string s)
        {
            var d = (s ?? "").Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (var c in d)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            }
            return sb.ToString().ToLowerInvariant();
        }

        private static bool HasAny(string text, params string[] parts)
        {
            foreach (var p in parts) if (text.Contains(p)) return true;
            return false;
        }

        /// <summary>
        /// Status de conclusão do Playnite -> botão do Backloggd ("play", "playing" ou "backlog"). Null = não mexer.
        /// O reconhecimento é por palavras (pt/en/es), pois o nome do status muda com o idioma do Playnite.
        /// </summary>
        public string MapStatus(Game g)
        {
            try
            {
                if (g == null || g.CompletionStatusId == Guid.Empty) return null;
                var cs = _api.Database.CompletionStatuses.Get(g.CompletionStatusId);
                if (cs == null || string.IsNullOrWhiteSpace(cs.Name)) return null;

                var n = StripAccents(cs.Name);
                if (HasAny(n, "not played", "nao jogado", "no jugado", "sin jugar", "unplayed")) return null;
                if (HasAny(n, "plan", "backlog", "pendente", "quero", "want", "por jugar")) return "backlog";
                if (HasAny(n, "playing", "jogando", "jugando")) return "playing";
                if (HasAny(n, "complet", "100%", "platin", "beaten", "zerad", "finaliz", "termin", "conclu", "finished", "passad",
                    "abandon", "desist", "dropped", "hold", "espera", "paus", "suspen", "played", "jogado", "jugado")) return "play";

                _logger.Debug("[BLS] Status '" + cs.Name + "' not recognized; the site status will not be changed.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.Debug("[BLS] Could not read the completion status: " + ex.Message);
                return null;
            }
        }

        // ---------------------------------------------------------------- plataforma

        public class PlatformRule
        {
            public int Id;
            public string Name;
            public string[] Contains = new string[0];
            public string[] Exact = new string[0];
        }

        private static PlatformRule R(int id, string name, string[] contains, string[] equals)
        {
            return new PlatformRule { Id = id, Name = name, Contains = contains ?? new string[0], Exact = equals ?? new string[0] };
        }

        /// <summary>
        /// Plataformas do Backloggd (ids do IGDB). Só o 6 (Windows PC) foi confirmado na captura; os demais seguem o IGDB e são
        /// conferidos no log depois de cada envio. A ordem importa: as mais específicas vêm primeiro.
        /// </summary>
        public static readonly PlatformRule[] PlatformRules =
        {
            R(167, "PlayStation 5", new[] { "playstation 5" }, new[] { "ps5" }),
            R(48, "PlayStation 4", new[] { "playstation 4" }, new[] { "ps4" }),
            R(9, "PlayStation 3", new[] { "playstation 3" }, new[] { "ps3" }),
            R(8, "PlayStation 2", new[] { "playstation 2" }, new[] { "ps2" }),
            R(46, "PlayStation Vita", new[] { "vita" }, null),
            R(38, "PlayStation Portable", new[] { "playstation portable" }, new[] { "psp" }),
            R(7, "PlayStation", new[] { "playstation" }, new[] { "psx", "ps1", "psone" }),
            R(169, "Xbox Series X|S", new[] { "xbox series" }, null),
            R(49, "Xbox One", new[] { "xbox one" }, null),
            R(12, "Xbox 360", new[] { "xbox 360" }, null),
            R(11, "Xbox", new[] { "xbox" }, null),
            R(508, "Nintendo Switch 2", new[] { "switch 2" }, null),
            R(130, "Nintendo Switch", new[] { "switch" }, null),
            R(41, "Wii U", new[] { "wii u" }, null),
            R(5, "Wii", new[] { "wii" }, null),
            R(37, "Nintendo 3DS", new[] { "3ds" }, null),
            R(20, "Nintendo DS", new[] { "nintendo ds" }, new[] { "nds", "ds" }),
            R(21, "GameCube", new[] { "gamecube" }, null),
            R(4, "Nintendo 64", new[] { "nintendo 64" }, new[] { "n64" }),
            R(19, "Super Nintendo", new[] { "super nintendo", "super nes" }, new[] { "snes" }),
            R(18, "NES", new[] { "nintendo entertainment" }, new[] { "nes" }),
            R(24, "Game Boy Advance", new[] { "game boy advance" }, new[] { "gba" }),
            R(22, "Game Boy Color", new[] { "game boy color" }, new[] { "gbc" }),
            R(33, "Game Boy", new[] { "game boy" }, new[] { "gb" }),
            R(29, "Sega Mega Drive/Genesis", new[] { "genesis", "mega drive", "megadrive" }, null),
            R(23, "Dreamcast", new[] { "dreamcast" }, null),
            R(32, "Sega Saturn", new[] { "saturn" }, null),
            R(52, "Arcade", new[] { "arcade" }, null),
            R(13, "DOS", new[] { "ms-dos", "msdos" }, new[] { "dos" }),
            R(14, "Mac", new[] { "macos", "os x", "macintosh" }, new[] { "mac" }),
            R(3, "Linux", new[] { "linux" }, null),
            R(34, "Android", new[] { "android" }, null),
            R(39, "iOS", new[] { "iphone", "ipad" }, new[] { "ios" }),
            R(6, "Windows PC", new[] { "windows", "pc (", "pc windows" }, new[] { "pc" })
        };

        /// <summary>Regra de plataforma para um nome de plataforma do Playnite (null = sem equivalente conhecido).</summary>
        public static PlatformRule RuleFor(string platformName)
        {
            var n = StripAccents(platformName).Trim();
            if (n.Length == 0) return null;

            foreach (var r in PlatformRules)
            {
                if (r.Exact.Contains(n)) return r;
                foreach (var c in r.Contains) if (n.Contains(c)) return r;
            }
            return null;
        }

        private static readonly string[] PcSources = { "steam", "epic", "gog", "ea app", "origin", "ubisoft", "battle.net", "itch", "amazon", "humble", "game pass", "xbox" };

        /// <summary>Plataforma do Backloggd para um jogo do Playnite, ou null para não definir.</summary>
        public PlatformRule PlatformRuleFor(Game g)
        {
            try
            {
                if (g.Platforms != null)
                {
                    PlatformRule first = null;
                    foreach (var p in g.Platforms)
                    {
                        var r = RuleFor(p.Name);
                        if (r == null) continue;
                        if (r.Id == 6) return r;   // você joga no PC: se o jogo tem PC entre as plataformas, vale Windows PC
                        if (first == null) first = r;
                    }

                    // Jogo de loja de PC (Steam, Epic...) listado só com plataforma de console (ex.: Xbox): você joga no PC.
                    if (first != null && IsXbox(first) && g.Source != null && !string.IsNullOrEmpty(g.Source.Name))
                    {
                        var src0 = StripAccents(g.Source.Name);
                        if (PcSources.Where(x => x != "xbox" && x != "game pass").Any(x => src0.Contains(x))) return PlatformRules.First(x => x.Id == 6);
                    }

                    if (first != null) return first;
                }

                // Sem plataforma útil no Playnite: bibliotecas de PC conhecidas viram Windows PC.
                if ((g.Platforms == null || g.Platforms.Count == 0) && g.Source != null && !string.IsNullOrEmpty(g.Source.Name))
                {
                    var src = StripAccents(g.Source.Name);
                    if (PcSources.Any(x => src.Contains(x))) return PlatformRules.First(x => x.Id == 6);
                }
            }
            catch (Exception ex)
            {
                _logger.Debug("[BLS] Platform mapping failed: " + ex.Message);
            }

            return null;
        }

        private static bool IsXbox(PlatformRule r)
        {
            return r.Id == 11 || r.Id == 12 || r.Id == 49 || r.Id == 169;
        }

        public int? PlatformIdFor(Game g)
        {
            var r = PlatformRuleFor(g);
            return r == null ? (int?)null : r.Id;
        }

        // ---------------------------------------------------------------- vínculo

        private static string StripSubtitleMarks(string name)
        {
            return (name ?? "").Replace(":", " ").Replace(" - ", " ").Replace("™", "").Replace("®", "").Trim();
        }

        /// <summary>
        /// Procura o jogo no Backloggd. Ordem: título exato; se houver vários, o ano de lançamento do Playnite (igual, depois ±1);
        /// se não houver exato, um único candidato cujo título contém o outro. Se continuar ambíguo, devolve null (vínculo manual).
        /// </summary>
        public async Task<GameHit> AutoMatchAsync(BackloggdApi api, Game g)
        {
            var hits = await api.SearchAsync(g.Name);
            if (hits.Count == 0)
            {
                var alt = StripSubtitleMarks(g.Name);
                if (alt != g.Name) hits = await api.SearchAsync(alt);
            }

            var key = BackloggdApi.Normalize(g.Name);
            var exact = hits.Where(h => BackloggdApi.Normalize(h.Title) == key).ToList();
            int year = g.ReleaseDate.HasValue ? g.ReleaseDate.Value.Year : 0;

            GameHit pick = null;
            if (exact.Count == 1) pick = exact[0];
            else if (exact.Count > 1 && year > 0)
            {
                var same = exact.Where(h => h.Year == year.ToString(CultureInfo.InvariantCulture)).ToList();
                if (same.Count == 1) pick = same[0];
                else
                {
                    var near = exact.Where(h => { int y; return int.TryParse(h.Year, out y) && Math.Abs(y - year) <= 1; }).ToList();
                    if (near.Count == 1) pick = near[0];
                }
            }
            else if (exact.Count == 0 && key.Length >= 4)
            {
                var like = hits.Where(h =>
                {
                    var n = BackloggdApi.Normalize(h.Title);
                    return n.Length > 0 && (n.Contains(key) || key.Contains(n));
                }).ToList();
                if (like.Count == 1) pick = like[0];
                else if (like.Count > 1 && year > 0)
                {
                    var same = like.Where(h => h.Year == year.ToString(CultureInfo.InvariantCulture)).ToList();
                    if (same.Count == 1) pick = same[0];
                }
            }

            if (pick == null)
                _logger.Info("[BLS] Match for '" + g.Name + "' (" + (year > 0 ? year.ToString(CultureInfo.InvariantCulture) : "no year") + ") failed. Site candidates: " +
                    string.Join(" | ", hits.Take(8).Select(h => h.ToString())));

            return pick;
        }

        public async Task<List<GameHit>> SearchAsync(string query)
        {
            using (var web = new BackloggdWeb(_api, _logger))
            {
                var api = new BackloggdApi(web, _settings, _logger);
                return await api.SearchAsync(query);
            }
        }

        public async Task<string> CheckLoginAsync()
        {
            using (var web = new BackloggdWeb(_api, _logger))
            {
                var api = new BackloggdApi(web, _settings, _logger);
                var user = await api.GetLoggedUserAsync();
                if (!string.IsNullOrEmpty(user)) { _settings.Username = user; _settings.Save(); web.SaveCookies(); LastProblem = ""; }
                return user;
            }
        }

        // ---------------------------------------------------------------- execução

        private void Notify(string id, string text, NotificationType type, Action click = null)
        {
            try
            {
                _api.Notifications.Add(click == null
                    ? new NotificationMessage("BLS-" + id, text, type)
                    : new NotificationMessage("BLS-" + id, text, type, click));
            }
            catch { }
        }

        private void NotifyLogin(string key)
        {
            Notify("login", L.T(key), NotificationType.Info, () => { var h = LoginRequested; if (h != null) h(); });
        }

        public bool IsRunning { get { return Gate.CurrentCount == 0; } }

        /// <summary>Processa a fila. Retorna quantos itens foram concluídos. Só um envio por vez.</summary>
        public async Task<int> RunAsync()
        {
            if (!await Gate.WaitAsync(0)) return 0;

            try
            {
                // Vigia: se o envio travar (navegador interno que não responde), a trava é liberada e você é avisado,
                // em vez de ficar "enviando..." para sempre.
                var core = RunCoreAsync();
                var finished = await Task.WhenAny(core, Task.Delay(TimeSpan.FromMinutes(4)));
                if (finished != core)
                {
                    _logger.Warn("[BLS] The send did not finish in 4 minutes; giving up on this run (it will be tried again next time).");
                    LastProblem = "stuck";
                    Notify("stuck", L.T("notify.stuck"), NotificationType.Error);
                    return 0;
                }
                return await core;
            }
            finally
            {
                Gate.Release();
            }
        }

        private async Task<int> RunCoreAsync()
        {
            int done = 0, errors = 0;
            LastProblem = "";
            {
                List<PendingItem> work;
                lock (_lock) work = _settings.Pending.ToList();
                if (work.Count == 0) { _logger.Info("[BLS] Queue is empty."); return 0; }
                _logger.Info("[BLS] Sending " + work.Count + " pending item(s).");

                using (var web = new BackloggdWeb(_api, _logger))
                {
                    var api = new BackloggdApi(web, _settings, _logger);

                    string user;
                    try { user = await api.GetLoggedUserAsync(); }
                    catch (BackloggdBlockedException ex)
                    {
                        _logger.Warn("[BLS] " + ex.Message);
                        LastProblem = "blocked";
                        Notify("blocked", L.T("notify.blocked"), NotificationType.Error);
                        return 0;
                    }

                    if (string.IsNullOrEmpty(user))
                    {
                        _logger.Info("[BLS] No signed-in user detected; the send was not attempted (see the lines above for the reason).");
                        if (api.LoginProblem == "unknownpage")
                        {
                            LastProblem = "unknown";
                            NotifyLogin("notify.unknownpage");
                        }
                        else
                        {
                            LastProblem = "login";
                            NotifyLogin("notify.login");
                        }
                        return 0;
                    }

                    _settings.Username = user;
                    web.SaveCookies();
                    _logger.Info("[BLS] Signed in as " + user + ".");

                    foreach (var item in work)
                    {
                        try
                        {
                            if (await ProcessAsync(api, item)) done++;
                        }
                        catch (BackloggdBlockedException ex)
                        {
                            _logger.Warn("[BLS] " + ex.Message);
                            LastProblem = "blocked";
                            Notify("blocked", L.T("notify.blocked"), NotificationType.Error);
                            break;
                        }
                        catch (Exception ex)
                        {
                            errors++;
                            _logger.Error(ex, "[BLS] Failed to send item for game " + item.GameId + ": " + ex.Message);
                            lock (_lock)
                            {
                                item.Attempts++;
                                item.LastError = ex.Message;
                                _settings.Save();
                            }
                        }
                    }
                }

                if (done > 0)
                {
                    _failStreak = 0;
                    Notify("ok", L.T("notify.synced", done), NotificationType.Info);
                }
                else if (errors > 0 && LastProblem != "blocked")
                {
                    // Várias rodadas seguidas só com erro, com login ok: o site pode ter mudado.
                    _failStreak++;
                    _logger.Warn("[BLS] Run with only failures (" + _failStreak + " in a row).");
                    if (_failStreak >= 3)
                    {
                        LastProblem = "site";
                        Notify("site", L.T("notify.site"), NotificationType.Error);
                    }
                }
            }

            return done;
        }

        private void Finish(PendingItem item)
        {
            lock (_lock)
            {
                _settings.Pending.Remove(item);
                _settings.Save();
            }
        }

        /// <summary>Retorna true quando o item foi concluído (ou descartado por não ter mais o que fazer).</summary>
        private async Task<bool> ProcessAsync(BackloggdApi api, PendingItem item)
        {
            Guid gid;
            Game game = Guid.TryParse(item.GameId, out gid) ? _api.Database.Games.Get(gid) : null;
            if (game == null) { Finish(item); return false; }

            var link = GetLink(gid);
            if (item.NeedsLink && (link == null || link.BackloggdId <= 0)) return false;
            _logger.Info("[BLS] Processing '" + game.Name + "' day '" + item.Day + "' +" + item.AddMinutes + " min (linked: " + (link != null && link.BackloggdId > 0) + ")");
            if (link != null && link.Ignored) { Finish(item); return false; }

            if (link == null || link.BackloggdId <= 0)
            {
                var hit = await AutoMatchAsync(api, game);
                if (hit == null)
                {
                    _logger.Info("[BLS] No unique automatic match for '" + game.Name + "'.");
                    item.LastError = "No unique match on Backloggd. Use the game menu: Backloggd > Link game.";
                    item.NeedsLink = true;
                    var gidCopy = gid;
                    Notify("link-" + item.GameId, L.T("notify.needlink", game.Name), NotificationType.Info, () => { var h = LinkRequested; if (h != null) h(gidCopy); });
                    lock (_lock) _settings.Save();
                    return false;
                }
                SetLink(gid, hit);
                link = GetLink(gid);
            }

            // 1) Tempo do dia
            if (!string.IsNullOrEmpty(item.Day) && _settings.SyncPlaytime && (item.AddMinutes > 0 || item.Target.HasValue))
            {
                var day = DateTime.ParseExact(item.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture);

                if (!item.Target.HasValue)
                {
                    var current = await api.GetLogAsync(link.BackloggdId);
                    item.Target = BackloggdApi.ReadDayMinutes(current, day) + item.AddMinutes;
                    lock (_lock) _settings.Save();
                }

                var res = await api.UpsertDayAsync(link.BackloggdId, link.Slug, day, 0, item.Target, PlatformIdFor(game));
                _logger.Info("[BLS] '" + game.Name + "' " + item.Day + ": site had " + res.BaseMinutes + " min, target " + res.TargetMinutes + ", sent=" + res.Changed + ", verified=" + res.Verified);
                if (!res.Verified) throw new InvalidOperationException("The site did not confirm the new time for " + item.Day + ".");
            }

            // 2) Status e nota (só quando mudaram no Playnite desde o último envio)
            if (_settings.SyncStatus)
            {
                var wanted = MapStatus(game);
                if (wanted != null && wanted != link.LastStatus)
                {
                    var sc = await api.ApplyStatusAsync(link.BackloggdId, wanted);
                    _logger.Info("[BLS] '" + game.Name + "' status '" + wanted + "': " + (sc ? "sent" : "already set on the site"));
                    lock (_lock) { link.LastStatus = wanted; _settings.Save(); }
                }
            }

            if (_settings.SyncRating)
            {
                var r = RatingFor(game);
                if (r > 0 && r != link.LastRating)
                {
                    var rc = await api.ApplyRatingAsync(link.BackloggdId, r);
                    _logger.Info("[BLS] '" + game.Name + "' rating " + r + ": " + (rc ? "sent" : "already set on the site"));
                    lock (_lock) { link.LastRating = r; _settings.Save(); }
                }
            }

            _logger.Info("[BLS] Done: '" + game.Name + "'.");
            Finish(item);
            return true;
        }
    }
}
