using Xunit;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Styling;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.VisualTree;
using OpenUtau.App;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

public class TestAppBuilder {
    public static AppBuilder BuildAvaloniaApp() {
        bool capture = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENUTAU_KEYBOARD_CAPTURES"));
        var builder = AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = !capture });
        return capture ? builder.UseSkia() : builder;
    }
}

namespace OpenUtau.App {
    public class AppTest {
        [Fact]
        public void BuildTest() {
            Assert.False(typeof(App).IsAbstract);
            Assert.False(typeof(Program).IsAbstract);
        }

        [Fact]
        public void StringsTest() {
            var appBuilder = TestAppBuilder.BuildAvaloniaApp()
                .SetupWithoutStarting();
            var app = appBuilder.Instance as App;
            Assert.NotNull(app);

            var languages = App.GetLanguages();
            Assert.True(languages.Count > 1);
            Assert.Contains("en-US", languages.Keys);
            Assert.Contains("zh-CN", languages.Keys);
            Assert.Contains("ja-JP", languages.Keys);
            foreach (var pair in languages) {
                Assert.NotNull(pair.Value);
            }
            Assert.Contains("ro-RO", languages.Keys);
            var master = XenStrings.GetResources("en-US");
            var keys = master.Keys.OfType<string>().OrderBy(k => k).ToArray();
            Assert.True(keys.Length >= 100);
            foreach (string language in languages.Keys) {
                var resource = XenStrings.GetResources(language);
                Assert.Equal(keys, resource.Keys.OfType<string>().OrderBy(k => k).ToArray());
                foreach (string key in keys) {
                    Assert.True(resource.TryGetResource(key, ThemeVariant.Default, out var value), $"{language}: {key}");
                    string translated = Assert.IsType<string>(value);
                    Assert.False(string.IsNullOrWhiteSpace(translated), $"{language}: {key}");
                    master.TryGetResource(key, ThemeVariant.Default, out var original);
                    string[] Placeholders(string text) => Regex.Matches(text, @"\{\d+(?:[^}]+)?\}")
                        .Select(m => m.Value).OrderBy(v => v).ToArray();
                    Assert.Equal(Placeholders((string)original!), Placeholders(translated));
                }
                App.SetLanguage(language);
                resource.TryGetResource("xen.field.frequency", ThemeVariant.Default, out var expected);
                Assert.Equal(expected, XenStrings.Get("field.frequency"));
            }
            App.SetLanguage("en-US");
            var dialog = new OpenUtau.App.Views.TuningDialog(projectDefault: true);
            dialog.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.InRange(dialog.Height, 320, 620);
            var numbers = dialog.GetVisualDescendants().OfType<NumericUpDown>().ToArray();
            Assert.NotEmpty(numbers);
            foreach (var number in numbers.Where(n => n.IsEffectivelyVisible)) {
                Assert.True(number.Bounds.Height >= 28);
            }
            VerifyTuningPages(dialog);
            dialog.Close();
        }

        static void VerifyTuningPages(OpenUtau.App.Views.TuningDialog dialog) {
            void Layout() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var navigation = dialog.GetVisualDescendants().OfType<ListBox>().Single(c => c.Name == "TuningNavigation");
            Assert.Equal(5, navigation.ItemCount);
            var presets = dialog.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "TuningPresets");
            foreach (int division in new[] { 12, 31, 13 }) {
                var tuning = presets.ItemsSource!.Cast<OpenUtau.Core.Microtonal.TuningDefinition>().Single(t => t.Divisions == division
                    && Math.Abs(t.Period.ToCents() - (division == 13 ? 1200 * Math.Log2(3) : 1200)) < 0.001);
                presets.SelectedItem = tuning;
                Layout();
                var map = dialog.GetVisualDescendants().OfType<Grid>().Single(c => c.Name == "TuningDegreeMap");
                Assert.Equal(division, map.Children.Count);
                navigation.SelectedIndex = 1;
                Layout();
                var candidate = dialog.GetVisualDescendants().OfType<OpenUtau.App.Controls.NotchedKeyboardPreview>()
                    .Single(c => c.Name == "GeneratorPitchPreview");
                Assert.Equal(division, candidate.ItemCount);
                Assert.Equal(division == 12 || division == 13, candidate.KeyLayout!.IsTraditionalKeyboard);
                var baselineKinds = candidate.Cells.Select(cell => cell.Kind).ToArray();
                var baselineGeometry = candidate.KeyLayout;
                var cardinality = dialog.GetVisualDescendants().OfType<NumericUpDown>().Single(c => c.Name == "MosCardinality");
                cardinality.Value = division == 31 ? 7 : 9;
                Layout();
                decimal? originalCardinality = cardinality.Value;
                cardinality.Value = originalCardinality + 1;
                Layout();
                Assert.Equal(baselineKinds, candidate.Cells.Select(cell => cell.Kind));
                Assert.Same(baselineGeometry, candidate.KeyLayout);
                cardinality.Value = originalCardinality;
                Layout();
                var notch = candidate.KeyLayout!.Keys.SelectMany(key => key.Rectangles.Select(rect => (key, rect)))
                    .First(pair => pair.rect.X < candidate.KeyLayout.Columns[pair.key.Column].X
                        || pair.rect.Right > candidate.KeyLayout.Columns[pair.key.Column].X + candidate.KeyLayout.Columns[pair.key.Column].Width);
                var sourceColumn = candidate.KeyLayout.Columns[notch.key.Column];
                double x = notch.rect.X < sourceColumn.X ? (notch.rect.X + sourceColumn.X) / 2
                    : (sourceColumn.X + sourceColumn.Width + notch.rect.Right) / 2;
                double scaleX = candidate.Bounds.Width / candidate.KeyLayout.Width;
                Assert.Equal(notch.key.Degree, candidate.SelectDegreeAt(new Point(x * scaleX,
                    (notch.rect.Y + notch.rect.Bottom) / 2 * candidate.Bounds.Height)));
                var peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(candidate);
                Assert.NotNull(peer);
                Assert.Equal(Avalonia.Automation.AutomationProperties.GetName(candidate), peer.GetName());
                Assert.Contains(XenStrings.Get("field.degree") + " " + notch.key.Degree, peer.GetName());
                var generator = dialog.GetVisualDescendants().OfType<NumericUpDown>().Single(c => c.Name == "MosGenerator");
                decimal? previousGenerator = generator.Value;
                generator.Value = 0;
                Layout();
                Assert.Equal(0, candidate.ItemCount);
                generator.Value = previousGenerator;
                Layout();
                Assert.Equal(division, candidate.ItemCount);
                var applyCandidate = dialog.GetVisualDescendants().OfType<Button>().Single(c => c.Name == "ApplyTuningDraft");
                Assert.Equal(XenStrings.Get(division == 12 ? "action.applyDisplay" : "action.applyTuning"), applyCandidate.Content);
                var generate = dialog.GetVisualDescendants().OfType<Button>().Single(c => Equals(c.Content, XenStrings.Get("action.generateSubset")));
                generate.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Layout();
                map = dialog.GetVisualDescendants().OfType<Grid>().Single(c => c.Name == "ScaleDegreeMap");
                Assert.Equal((int)cardinality.Value!, map.Children.OfType<Border>().Count(b => b.BorderThickness.Left == 2));
                navigation.SelectedIndex = 0;
                Layout();
            }
            presets.SelectedItem = presets.ItemsSource!.Cast<OpenUtau.Core.Microtonal.TuningDefinition>()
                .Single(t => t.Divisions == 12 && Math.Abs(t.Period.ToCents() - 1200) < 0.001);
            Layout();
            foreach (double width in new[] { 960.0, 650.0 }) {
                dialog.Width = width;
                dialog.Height = width == 650 ? 440 : 620;
                for (int page = 0; page < 5; page++) {
                    navigation.SelectedIndex = page;
                    Layout();
                    var viewport = dialog.GetVisualDescendants().OfType<ScrollViewer>()
                        .First(c => c.Content is StackPanel panel && panel.Children.FirstOrDefault() is TextBlock { FontSize: 20 });
                    Assert.True(viewport.Bounds.Width > 200);
                    Assert.True(viewport.Extent.Width <= viewport.Viewport.Width + 1,
                        $"Page {page} at {width}: {viewport.Extent.Width} / {viewport.Viewport.Width}");
                    var apply = dialog.GetVisualDescendants().OfType<Button>().Single(c => c.Name == "ApplyTuningDraft");
                    Assert.Equal(XenStrings.Get(page == 1 || page == 2 ? "action.applyDisplay" : "action.applyTuning"), apply.Content);
                }
            }
            VerifyTraditionalProfiles();
            CaptureNotchedKeyboards();
        }

        static void VerifyTraditionalProfiles() {
            var keyboard = new OpenUtau.App.Controls.NotchedKeyboardPreview();
            foreach (var (n, period, traditional) in new[] { (12, "2/1", true), (13, "3/1", true), (12, "3/1", false), (13, "2/1", false) }) {
                keyboard.SetTuning(OpenUtau.Core.Microtonal.TuningPresets.EqualDivision(n,
                    OpenUtau.Core.Microtonal.PitchInterval.FromRatio(period)));
                Assert.Equal(traditional, keyboard.KeyLayout!.IsTraditionalKeyboard);
            }
            var custom = OpenUtau.Core.Microtonal.TuningPresets.EqualDivision(12);
            custom.Keyboard.Classes[1] = "natural";
            keyboard.SetTuning(custom);
            Assert.False(keyboard.KeyLayout!.IsTraditionalKeyboard);
            Assert.NotEqual(keyboard.KeyLayout.Keys[1].RunIndex, keyboard.KeyLayout.Keys[2].RunIndex);
        }

        static void CaptureNotchedKeyboards() {
            string? directory = Environment.GetEnvironmentVariable("OPENUTAU_KEYBOARD_CAPTURES");
            if (string.IsNullOrEmpty(directory)) return;
            System.IO.Directory.CreateDirectory(directory);
            string previousTheme = OpenUtau.Core.Util.Preferences.Default.ThemeName;
            try {
                foreach (string theme in new[] { "Light", "Dark" }) {
                    OpenUtau.Core.Util.Preferences.Default.ThemeName = theme;
                    App.SetTheme();
                    foreach (var (n, period) in new[] { (12, "2/1"), (31, "2/1"), (53, "2/1"), (73, "2/1"), (13, "3/1") }) {
                        var keyboard = new OpenUtau.App.Controls.NotchedKeyboardPreview();
                        keyboard.SetTuning(OpenUtau.Core.Microtonal.TuningPresets.EqualDivision(n,
                            OpenUtau.Core.Microtonal.PitchInterval.FromRatio(period)));
                        keyboard.Measure(Size.Infinity);
                        var window = new Window {
                            Width = keyboard.DesiredSize.Width + 32, Height = 232,
                            Content = new Border { Padding = new Thickness(16), Child = keyboard },
                        };
                        window.Show();
                        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                        var notch = keyboard.KeyLayout!.Keys.SelectMany(key => key.Rectangles.Select(rect => (key, rect)))
                            .FirstOrDefault(pair => pair.rect.X < keyboard.KeyLayout.Columns[pair.key.Column].X
                                || pair.rect.Right > keyboard.KeyLayout.Columns[pair.key.Column].X + keyboard.KeyLayout.Columns[pair.key.Column].Width);
                        if (!keyboard.KeyLayout.IsTraditionalKeyboard && notch.key != null) keyboard.SelectDegreeAt(new Point((notch.rect.X + notch.rect.Right) / 2
                            * keyboard.Bounds.Width / keyboard.KeyLayout.Width,
                            (notch.rect.Y + notch.rect.Bottom) / 2 * keyboard.Bounds.Height));
                        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                        using (var frame = window.CaptureRenderedFrame()) {
                            frame?.Save(System.IO.Path.Combine(directory, $"keyboard-{n}-{(period == "2/1" ? "edo" : "edt")}-{theme}.png"));
                        }
                        window.Close();
                    }
                }
            } finally {
                OpenUtau.Core.Util.Preferences.Default.ThemeName = previousTheme;
                App.SetTheme();
            }
        }
    }
}
