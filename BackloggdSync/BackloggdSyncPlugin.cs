using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace BackloggdSync
{
    public partial class BackloggdSyncPlugin : GenericPlugin
    {
        private static readonly ILogger Logger = LogManager.GetLogger("BackloggdSync");

        private PluginSettings _settings;

        /// <summary>Estado do jogo selecionado, lido pelo tema (ver ThemeState).</summary>
        public ThemeState Theme { get; } = new ThemeState();

        public override Guid Id => Guid.Parse("59963214-572c-4467-9a24-625b20d7f53d");

        public SyncEngine Engine { get; private set; }
        public UpdateChecker Updates { get; private set; }

        public BackloggdSyncPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new GenericPluginProperties { HasSettings = true };

            // Tema: {PluginSettings Plugin=BackloggdSync, Path=HasData | Linked | Ignored | Title | PendingGame | PendingTotal | Account}
            AddSettingsSupport(new AddSettingsSupportArgs
            {
                SourceName = "BackloggdSync",
                SettingsRoot = "Theme"
            });

            _settings = LoadPluginSettings<PluginSettings>() ?? new PluginSettings();
            _settings.AttachOwner(this);
            L.Configure(_settings.Language, GetPlayniteLanguage());

            Engine = new SyncEngine(api, _settings, Logger);
            Updates = new UpdateChecker(api, Logger, _settings);
            BackloggdWeb.CookieFile = Path.Combine(GetPluginUserDataPath(), "session-cookies.json");
            Engine.LoginRequested = () => OnUi(OpenLoginWindow);
            Engine.LinkRequested = id => OnUi(() =>
            {
                var g = PlayniteApi.Database.Games.Get(id);
                if (g != null) LinkGame(g);
            });
            Logger.Info("[BLS] Backloggd Sync " + Version + " loaded.");
        }

        public string Version
        {
            get { return typeof(BackloggdSyncPlugin).Assembly.GetName().Version.ToString(3); }
        }

        private string GetPlayniteLanguage()
        {
            try { return PlayniteApi.ApplicationSettings.Language; }
            catch { return ""; }
        }

        // ---------------------------------------------------------------- integração com temas

        public void RefreshTheme(Game game)
        {
            try
            {
                if (game == null) { Theme.Clear(); return; }
                var link = Engine.GetLink(game.Id);
                var gid = game.Id.ToString();
                int pendingGame = 0, pendingTotal = 0;
                var items = _settings.Pending.ToArray();
                pendingTotal = items.Length;
                foreach (var p in items) if (p.GameId == gid) pendingGame++;
                var linked = link != null && link.BackloggdId > 0;
                Theme.Update(!string.IsNullOrEmpty(_settings.Username), linked, link != null && link.Ignored,
                    linked ? link.Title : "", pendingGame, pendingTotal, _settings.Username);
            }
            catch (Exception ex)
            {
                Logger.Warn("[BLS] Falha ao atualizar o estado do tema: " + ex.Message);
            }
        }

        public override void OnGameSelected(OnGameSelectedEventArgs args)
        {
            var list = args == null ? null : args.NewValue;
            RefreshTheme(list != null && list.Count == 1 ? list[0] : null);
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            _settings.AttachOwner(this);
            return _settings;
        }

        public override UserControl GetSettingsView(bool firstRunView)
        {
            _settings.AttachOwner(this);
            return new SyncSettingsControl(this, _settings);
        }

        // ---------------------------------------------------------------- eventos

        public void RunQueueInBackground(int delayMs)
        {
            Task.Run(async () =>
            {
                try
                {
                    if (delayMs > 0) await Task.Delay(delayMs);
                    await Engine.RunAsync();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "[BLS] Queue run failed: " + ex.Message);
                }
            });
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            if (Engine.PendingCount > 0 && !string.IsNullOrEmpty(_settings.Username)) RunQueueInBackground(15000);

            Task.Run(async () =>
            {
                await Task.Delay(20000);
                Updates.CheckForUpdates(false);
            });
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            try
            {
                var game = args == null ? null : args.Game;
                if (game == null || !_settings.AutoSyncOnStop) return;

                var link = Engine.GetLink(game.Id);
                if (link != null && link.Ignored) return;

                bool queued = false;
                if (_settings.SyncPlaytime)
                {
                    double secs = (double)args.ElapsedSeconds;
                    int minutes = (int)Math.Round(secs / 60.0);
                    if (minutes >= 1)
                    {
                        var day = DateTime.Now.AddSeconds(-secs).Date;
                        Engine.Enqueue(game.Id, day, minutes);
                        queued = true;
                    }
                }

                if (!queued && (_settings.SyncStatus || _settings.SyncRating))
                {
                    Engine.Enqueue(game.Id, null, 0);
                    queued = true;
                }

                if (queued) RunQueueInBackground(3000);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[BLS] OnGameStopped failed: " + ex.Message);
            }
        }

        // ---------------------------------------------------------------- menus

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            var games = args.Games == null ? new List<Game>() : args.Games.ToList();
            var section = "Backloggd Sync";

            if (games.Count == 1)
            {
                var g = games[0];
                yield return new GameMenuItem
                {
                    MenuSection = section,
                    Description = L.T("menu.link"),
                    Action = a => LinkGame(g)
                };
            }

            yield return new GameMenuItem
            {
                MenuSection = section,
                Description = L.T("menu.syncstatus"),
                Action = a =>
                {
                    foreach (var g in games) Engine.Enqueue(g.Id, null, 0);
                    Engine.RetryUnlinked();
                    RunQueueInBackground(0);
                }
            };

#if PERSONAL
            foreach (var m in PersonalGameMenu(games)) yield return m;
            foreach (var m in PersonalHoursGameMenu(games)) yield return m;
#endif

            var ignoreAll = games.All(g => { var l = Engine.GetLink(g.Id); return l != null && l.Ignored; });
            yield return new GameMenuItem
            {
                MenuSection = section,
                Description = ignoreAll ? L.T("menu.unignore") : L.T("menu.ignore"),
                Action = a => { foreach (var g in games) Engine.SetIgnored(g.Id, !ignoreAll); }
            };
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem
            {
                MenuSection = "@Backloggd Sync",
                Description = L.T("menu.sendpending"),
                Action = a => { Engine.RetryUnlinked(); RunQueueInBackground(0); }
            };

#if PERSONAL
            foreach (var m in PersonalMainMenu()) yield return m;
            foreach (var m in PersonalHoursMenu()) yield return m;
            foreach (var m in PersonalYgpMenu()) yield return m;
            foreach (var m in PersonalPlatformMenu()) yield return m;
#endif
        }

        private static void OnUi(Action action)
        {
            var d = Application.Current == null ? null : Application.Current.Dispatcher;
            if (d != null && !d.CheckAccess()) d.BeginInvoke(action);
            else action();
        }

        /// <summary>Abre a janela de login do Backloggd; ao fechar, confere a sessão (guardando os cookies) e envia as pendências.</summary>
        public void OpenLoginWindow()
        {
            try
            {
                var view = PlayniteApi.WebViews.CreateView(980, 760);

                // Fecha a janela sozinha quando o login der certo (a cada 2 s confere, na thread da interface, se o site já mostra você logado).
                bool closing = false, checking = false;
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                timer.Tick += async (s, e) =>
                {
                    if (closing || checking) return;
                    checking = true;
                    try
                    {
                        var address = (view.GetCurrentAddress() ?? "").ToLowerInvariant();
                        if (!address.Contains("backloggd.com")) return;
                        if (address.Contains("/login") || address.Contains("/signup") || address.Contains("/sign_up") ||
                            address.Contains("/password") || address.Contains("/users/")) return;

                        dynamic res = await view.EvaluateScriptAsync(
                            "(function(){var h=document.documentElement?document.documentElement.innerHTML:'';" +
                            "return (/>\\s*Log In\\s*</i.test(h)||h.indexOf('nav-bar-search')<0)?'out':'in';})()");
                        if ((res == null ? null : res.Result as string) == "in")
                        {
                            closing = true;
                            timer.Stop();
                            Logger.Info("[BLS] Login detected; closing the login window.");
                            view.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug("[BLS] Login window check failed: " + ex.Message);
                    }
                    finally { checking = false; }
                };

                view.Navigate(BackloggdWeb.BaseUrl + "/login/");
                timer.Start();
                view.OpenDialog();
                timer.Stop();
                view.Dispose();
            }
            catch (Exception ex)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(L.T("window.err", ex.Message), "Backloggd Sync");
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    var user = await Engine.CheckLoginAsync();
                    if (!string.IsNullOrEmpty(user))
                    {
                        OnUi(() => PlayniteApi.Dialogs.ShowMessage(L.T("login.ok", user), "Backloggd Sync"));
                        Engine.RetryUnlinked();
                        await Engine.RunAsync();
                    }
                    else
                    {
                        OnUi(() => PlayniteApi.Dialogs.ShowMessage(L.T("login.fail"), "Backloggd Sync"));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "[BLS] Check after login failed: " + ex.Message);
                }
            });
        }

        private List<GameHit> SearchWithProgress(string query, out string error)
        {
            List<GameHit> hits = null;
            string err = null;
            PlayniteApi.Dialogs.ActivateGlobalProgress(p =>
            {
                try { hits = Engine.SearchAsync(query).GetAwaiter().GetResult(); }
                catch (Exception ex) { err = ex.Message; }
            }, new GlobalProgressOptions(L.T("link.searching")));
            error = err;
            return hits ?? new List<GameHit>();
        }

        /// <summary>
        /// Vínculo manual. Já busca pelo nome do jogo e mostra as versões encontradas (com ano) para você escolher;
        /// dá para refazer a busca com outro nome.
        /// </summary>
        public void LinkGame(Game game)
        {
            try
            {
                var query = game.Name;
                GameHit hit = null;

                while (hit == null)
                {
                    string error;
                    var hits = SearchWithProgress(query, out error);
                    if (error != null) { PlayniteApi.Dialogs.ShowErrorMessage(error, "Backloggd Sync"); return; }

                    if (hits.Count == 0)
                    {
                        var ask = PlayniteApi.Dialogs.SelectString(L.T("link.none") + "\n\n" + L.T("link.prompt"), L.T("link.title"), query);
                        if (ask == null || !ask.Result || string.IsNullOrWhiteSpace(ask.SelectedString)) return;
                        query = ask.SelectedString;
                        continue;
                    }

                    var options = hits.Select(h => new GenericItemOption(h.ToString(), "backloggd.com/games/" + h.Slug)).ToList();
                    var other = new GenericItemOption(L.T("link.other"), "");
                    options.Add(other);

                    var year = game.ReleaseDate.HasValue ? " (" + game.ReleaseDate.Value.Year + ")" : "";
                    var chosen = PlayniteApi.Dialogs.ChooseItemWithSearch(options, q => options, query, L.T("link.pick", game.Name + year));
                    if (chosen == null) return;

                    if (chosen == other)
                    {
                        var ask = PlayniteApi.Dialogs.SelectString(L.T("link.prompt"), L.T("link.title"), query);
                        if (ask == null || !ask.Result || string.IsNullOrWhiteSpace(ask.SelectedString)) return;
                        query = ask.SelectedString;
                        continue;
                    }

                    hit = hits[options.IndexOf(chosen)];
                }

                Engine.SetLink(game.Id, hit);
                RefreshTheme(game);

                // Vinculou: envia na hora tudo do jogo (itens que estavam parados, status e nota).
                Engine.Enqueue(game.Id, null, 0);
#if PERSONAL
                PersonalAfterLink(game);
#else
                RunQueueInBackground(0);
#endif
                PlayniteApi.Dialogs.ShowMessage(L.T("link.done", game.Name, hit.ToString()), "Backloggd Sync");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[BLS] Link failed: " + ex.Message);
            }
        }
    }
}
