using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Playnite.SDK.Controls;

namespace BackloggdSync
{
    /// <summary>Página do plugin em Configurações > Extensões: conexão, opções de envio e (recolhido) o modo captura.</summary>
    public class SyncSettingsControl : PluginUserControl
    {
        private readonly BackloggdSyncPlugin _plugin;
        private readonly PluginSettings _settings;
        private readonly StackPanel _main;
        private TextBlock _status;
        private DispatcherTimer _timer;
        private StackPanel _pendPanel;
        private string _pendSig = "\u0001";

        public SyncSettingsControl(BackloggdSyncPlugin plugin, PluginSettings settings)
        {
            _plugin = plugin;
            _settings = settings;

            _main = new StackPanel { Margin = new Thickness(15) };
            Loaded += (s, e) => StartTimer();
            Unloaded += (s, e) => StopTimer();
            Content = new ScrollViewer { Content = _main, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Build();
        }

        private static TextBlock Text(string text, double size, FontWeight weight, double opacity, Thickness margin)
        {
            return new TextBlock { Text = text, FontSize = size, FontWeight = weight, Opacity = opacity, TextWrapping = TextWrapping.Wrap, Margin = margin };
        }

        private CheckBox Check(string key, bool value, Action<bool> set)
        {
            var cb = new CheckBox { Content = L.T(key), IsChecked = value, Margin = new Thickness(0, 6, 0, 0) };
            cb.Checked += (s, e) => { set(true); _settings.Save(); };
            cb.Unchecked += (s, e) => { set(false); _settings.Save(); };
            return cb;
        }

        private Button Btn(string key, Action click)
        {
            var b = new Button { Content = L.T(key), Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 8, 0) };
            b.Click += (s, e) => click();
            return b;
        }

        private void Build()
        {
            _main.Children.Add(Text("Backloggd Sync", 20, FontWeights.Bold, 1, new Thickness(0)));
            _main.Children.Add(Text(L.T("sync.intro"), 12, FontWeights.Normal, 0.7, new Thickness(0, 6, 0, 0)));

            _status = Text(StatusText(), 13, FontWeights.SemiBold, 1, new Thickness(0, 14, 0, 0));
            _main.Children.Add(_status);

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            row.Children.Add(Btn("btn.login", OpenLogin));
            row.Children.Add(Btn("btn.check", CheckLogin));
            row.Children.Add(Btn("btn.sendnow", () => { _plugin.Engine.RetryUnlinked(); _plugin.RunQueueInBackground(0); }));
            row.Children.Add(Btn("btn.checkupdate", () => _plugin.Updates.CheckForUpdates(true)));
            _main.Children.Add(row);

            _main.Children.Add(Check("opt.auto", _settings.AutoSyncOnStop, v => _settings.AutoSyncOnStop = v));
            _main.Children.Add(Check("opt.time", _settings.SyncPlaytime, v => _settings.SyncPlaytime = v));
            _main.Children.Add(Check("opt.status", _settings.SyncStatus, v => _settings.SyncStatus = v));
            _main.Children.Add(Check("opt.rating", _settings.SyncRating, v => _settings.SyncRating = v));
            _main.Children.Add(Text(L.T("opt.note"), 11, FontWeights.Normal, 0.6, new Thickness(0, 8, 0, 0)));

            _main.Children.Add(Text(L.T("pend.header"), 15, FontWeights.SemiBold, 1, new Thickness(0, 22, 0, 0)));
            _main.Children.Add(Text(L.T("pend.note"), 11, FontWeights.Normal, 0.6, new Thickness(0, 2, 0, 6)));
            _pendPanel = new StackPanel();
            _main.Children.Add(_pendPanel);
            RefreshPending(true);

            var capture = new Expander
            {
                Header = L.T("capture.header"),
                Margin = new Thickness(0, 20, 0, 0),
                Content = new CaptureSettingsControl(_plugin, _settings)
            };
            _main.Children.Add(capture);
        }

        private string StatusText()
        {
            var user = string.IsNullOrEmpty(_settings.Username) ? L.T("status.none") : L.T("status.user", _settings.Username);
            var text = user + "  |  " + L.T("status.pending", _plugin.Engine.PendingCount);
            if (_plugin.Engine.IsRunning) text += "  |  " + L.T("status.sending");
            else
            {
                var problem = _plugin.Engine.LastProblem;
                if (problem == "login" || problem == "unknown") text += "  |  " + L.T("reason.login");
                else if (problem == "blocked") text += "  |  " + L.T("reason.blocked");
                else if (problem == "stuck") text += "  |  " + L.T("reason.stuck");
                else if (problem == "site") text += "  |  " + L.T("reason.site");
            }
            return text;
        }

        /// <summary>Atualiza o contador de pendências sozinho enquanto a página está aberta (sem precisar verificar a conexão).</summary>
        private void StartTimer()
        {
            if (_timer != null) return;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => Refresh();
            _timer.Start();
        }

        private void StopTimer()
        {
            if (_timer == null) return;
            _timer.Stop();
            _timer = null;
        }

        private void Refresh()
        {
            if (_status != null) _status.Text = StatusText();
            RefreshPending(false);
        }

        /// <summary>Lista do que está na fila e por quê. Só é refeita quando algo muda.</summary>
        private void RefreshPending(bool force)
        {
            if (_pendPanel == null) return;
            var views = _plugin.Engine.PendingViews();
            var sig = string.Join("|", views.Select(v => v.GameId + "~" + v.Detail + "~" + v.Reason));
            if (!force && sig == _pendSig) return;
            _pendSig = sig;

            _pendPanel.Children.Clear();
            if (views.Count == 0)
            {
                _pendPanel.Children.Add(Text(L.T("pend.none"), 12, FontWeights.Normal, 0.7, new Thickness(0)));
                return;
            }

            foreach (var v in views.Take(60))
            {
                var view = v;
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8), LastChildFill = true };

                var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(buttons, Dock.Right);

                if (view.NeedsLink && view.GameId != Guid.Empty)
                {
                    buttons.Children.Add(SmallBtn("pend.link", () =>
                    {
                        var game = _plugin.PlayniteApi.Database.Games.Get(view.GameId);
                        if (game != null) _plugin.LinkGame(game);
                        RefreshPending(true);
                    }));
                    buttons.Children.Add(SmallBtn("menu.ignore", () =>
                    {
                        _plugin.Engine.SetIgnored(view.GameId, true);
                        RefreshPending(true);
                    }));
                }
                buttons.Children.Add(SmallBtn("pend.discard", () =>
                {
                    _plugin.Engine.Discard(view.Item);
                    RefreshPending(true);
                }));

                var info = new StackPanel();
                info.Children.Add(Text(view.GameName + "  -  " + view.Detail, 12, FontWeights.SemiBold, 1, new Thickness(0)));
                info.Children.Add(Text(view.Reason, 11, FontWeights.Normal, 0.65, new Thickness(0, 1, 0, 0)));

                row.Children.Add(buttons);
                row.Children.Add(info);
                _pendPanel.Children.Add(row);
            }

            if (views.Count > 60)
                _pendPanel.Children.Add(Text(L.T("pend.more", views.Count - 60), 11, FontWeights.Normal, 0.6, new Thickness(0)));
        }

        private Button SmallBtn(string key, Action click)
        {
            var b = new Button { Content = L.T(key), Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(6, 0, 0, 0) };
            b.Click += (s, e) => click();
            return b;
        }

        private void OpenLogin()
        {
            _plugin.OpenLoginWindow();
            Refresh();
        }

        private async void CheckLogin()
        {
            try
            {
                _status.Text = L.T("status.checking");
                var user = await _plugin.Engine.CheckLoginAsync();
                if (string.IsNullOrEmpty(user)) _status.Text = L.T("status.none");
                else Refresh();
            }
            catch (Exception ex)
            {
                _status.Text = ex.Message;
            }
        }
    }
}
