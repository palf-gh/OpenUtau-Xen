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
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
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
            foreach (int division in new[] { 31, 13 }) {
                var tuning = presets.ItemsSource!.Cast<OpenUtau.Core.Microtonal.TuningDefinition>().Single(t => t.Divisions == division
                    && Math.Abs(t.Period.ToCents() - (division == 31 ? 1200 : 1200 * Math.Log2(3))) < 0.001);
                presets.SelectedItem = tuning;
                Layout();
                var map = dialog.GetVisualDescendants().OfType<Grid>().Single(c => c.Name == "TuningDegreeMap");
                Assert.Equal(division, map.Children.Count);
                navigation.SelectedIndex = 1;
                Layout();
                var candidate = dialog.GetVisualDescendants().OfType<ListBox>().Single(c => c.Name == "GeneratorPitchPreview");
                Assert.Equal(division, candidate.ItemCount);
                var cardinality = dialog.GetVisualDescendants().OfType<NumericUpDown>().Single(c => c.Name == "MosCardinality");
                cardinality.Value = division == 31 ? 7 : 9;
                Layout();
                var generator = dialog.GetVisualDescendants().OfType<NumericUpDown>().Single(c => c.Name == "MosGenerator");
                decimal? previousGenerator = generator.Value;
                generator.Value = 0;
                Layout();
                Assert.Equal(0, candidate.ItemCount);
                generator.Value = previousGenerator;
                Layout();
                Assert.Equal(division, candidate.ItemCount);
                var applyCandidate = dialog.GetVisualDescendants().OfType<Button>().Single(c => c.Name == "ApplyTuningDraft");
                Assert.Equal(XenStrings.Get("action.applyTuning"), applyCandidate.Content);
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
        }
    }
}
