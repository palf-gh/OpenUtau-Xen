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
            dialog.Close();
        }
    }
}
