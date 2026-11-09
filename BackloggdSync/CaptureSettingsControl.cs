using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Playnite.SDK.Controls;

namespace BackloggdSync
{
    /// <summary>Página do plugin em Configurações > Extensões: roteiro de captura passo a passo.</summary>
    public class CaptureSettingsControl : PluginUserControl
    {
        private readonly BackloggdSyncPlugin _plugin;
        private readonly PluginSettings _settings;
        private readonly StackPanel _main;

        public CaptureSettingsControl(BackloggdSyncPlugin plugin, PluginSettings settings)
        {
            _plugin = plugin;
            _settings = settings;

            _main = new StackPanel { Margin = new Thickness(15) };
            Content = new ScrollViewer { Content = _main, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Build();
        }

        private void Rebuild()
        {
            _main.Children.Clear();
            Build();
        }

        private static TextBlock Text(string text, double size, FontWeight weight, double opacity = 1.0, Thickness? margin = null)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = size,
                FontWeight = weight,
                Opacity = opacity,
                TextWrapping = TextWrapping.Wrap,
                Margin = margin ?? new Thickness(0)
            };
        }

        private void Build()
        {
            _main.Children.Add(Text(L.T("title"), 20, FontWeights.Bold));
            _main.Children.Add(Text(L.T("intro"), 13, FontWeights.Normal, 0.9, new Thickness(0, 8, 0, 0)));
            _main.Children.Add(Text(L.T("privacy"), 12, FontWeights.Normal, 0.65, new Thickness(0, 6, 0, 0)));

            int done = 0;
            var rows = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            for (int i = 0; i < CaptureSteps.All.Count; i++)
            {
                var info = _plugin.Store.GetInfo(i, CaptureSteps.All[i]);
                if (info.Done) done++;
                rows.Children.Add(BuildStepRow(i, CaptureSteps.All[i], info));
            }

            _main.Children.Add(Text(L.T("progress", done, CaptureSteps.All.Count), 14, FontWeights.SemiBold, 1.0, new Thickness(0, 14, 0, 0)));
            _main.Children.Add(rows);
            _main.Children.Add(BuildFooter());
        }

        private Border BuildStepRow(int index, CaptureStep step, StepInfo info)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel();
            left.Children.Add(Text(L.T("step.label", index + 1) + " - " + L.Pick(step.Title), 14, FontWeights.Bold));
            left.Children.Add(Text(L.Pick(step.Instruction), 12, FontWeights.Normal, 0.75, new Thickness(0, 4, 0, 0)));

            var status = info.Done
                ? Text(L.T("step.done", info.RequestCount, info.FinishedLocal), 12, FontWeights.SemiBold, 1.0, new Thickness(0, 6, 0, 0))
                : Text(L.T("step.pending"), 12, FontWeights.Normal, 0.55, new Thickness(0, 6, 0, 0));
            if (info.Done) status.Foreground = Brushes.LimeGreen;
            left.Children.Add(status);
            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            var button = new Button
            {
                Content = info.Done ? L.T("step.redo") : L.T("step.start"),
                Padding = new Thickness(16, 6, 16, 6),
                Margin = new Thickness(14, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            button.Click += (s, e) => StartStep(index);
            Grid.SetColumn(button, 1);
            grid.Children.Add(button);

            return new Border
            {
                Child = grid,
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 8),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(70, 128, 128, 128)),
                CornerRadius = new CornerRadius(6)
            };
        }

        private StackPanel BuildFooter()
        {
            var footer = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 20) };

            var export = new Button { Content = L.T("btn.export"), Padding = new Thickness(16, 7, 16, 7), Margin = new Thickness(0, 0, 10, 0) };
            export.Click += (s, e) => Export();
            footer.Children.Add(export);

            var folder = new Button { Content = L.T("btn.folder"), Padding = new Thickness(16, 7, 16, 7), Margin = new Thickness(0, 0, 10, 0) };
            folder.Click += (s, e) => OpenFolder();
            footer.Children.Add(folder);

            var clear = new Button { Content = L.T("btn.clear"), Padding = new Thickness(16, 7, 16, 7) };
            clear.Click += (s, e) => ClearAll();
            footer.Children.Add(clear);

            return footer;
        }

        private void StartStep(int index)
        {
            var step = CaptureSteps.All[index];
            try
            {
                var result = _plugin.Runner.RunStep(step, index, CaptureSteps.All.Count);
                var entries = result["entries"] as Newtonsoft.Json.Linq.JArray;
                if (entries == null || entries.Count == 0)
                {
                    MessageBox.Show(L.T("step.empty"), L.T("app.name"), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    _plugin.Store.Save(index, step, result);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(L.T("window.err", ex.Message), L.T("app.name"), MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Rebuild();
        }

        private void Export()
        {
            try
            {
                var path = _plugin.Store.Export(_plugin.Version);
                if (path == null)
                {
                    MessageBox.Show(L.T("export.none"), L.T("app.name"), MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                MessageBox.Show(L.T("export.ok", path), L.T("app.name"), MessageBoxButton.OK, MessageBoxImage.Information);
                Process.Start("explorer.exe", "/select,\"" + path + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(L.T("export.err", ex.Message), L.T("app.name"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenFolder()
        {
            try
            {
                Directory.CreateDirectory(_plugin.Store.Folder);
                Process.Start("explorer.exe", "\"" + _plugin.Store.Folder + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, L.T("app.name"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearAll()
        {
            if (MessageBox.Show(L.T("clear.confirm"), L.T("app.name"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            try { _plugin.Store.Clear(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, L.T("app.name")); }
            Rebuild();
        }
    }
}
