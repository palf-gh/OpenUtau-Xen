using Xunit;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Styling;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;
using OpenUtau.App;
using ReactiveUI.Avalonia;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

public class TestAppBuilder {
    public static AppBuilder BuildAvaloniaApp() {
        bool capture = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENUTAU_KEYBOARD_CAPTURES"));
        var builder = AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = !capture })
            .With(OpenUtau.App.Program.FontOptions())
            .UseReactiveUI(_ => { });
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
            // Overlays for upstream keys that Crowdin has not translated: only upstream keys, with upstream placeholders.
            var upstreamEnglish = languages["en-US"];
            foreach (string language in languages.Keys) {
                var overlay = XenStrings.GetUpstreamOverlay(language);
                if (overlay == null) continue;
                foreach (string key in overlay.Keys.OfType<string>()) {
                    Assert.True(upstreamEnglish.TryGetResource(key, ThemeVariant.Default, out var english), $"{language}: {key}");
                    overlay.TryGetResource(key, ThemeVariant.Default, out var translated);
                    string[] Marks(string text) => Regex.Matches(text, @"\{\d+(?:[^}]+)?\}").Select(m => m.Value).OrderBy(v => v).ToArray();
                    Assert.Equal(Marks((string)english!), Marks((string)translated!));
                    // Tool tips keep their line layout; prefixes keep the spaces the code appends to.
                    string en = (string)english!, text = (string)translated!;
                    Assert.True(en.Split('\n').Length == text.Split('\n').Length, $"{language}: {key} lines");
                    Assert.True((!char.IsWhiteSpace(en[0]) || char.IsWhiteSpace(text[0]))
                        && (!char.IsWhiteSpace(en[^1]) || char.IsWhiteSpace(text[^1])), $"{language}: {key} edge spaces");
                }
            }
            App.SetLanguage("ja-JP");
            Assert.True(Application.Current!.TryGetResource("menu.tools.layout.reset", ThemeVariant.Default, out var resetLayout));
            Assert.Equal("レイアウトをリセット", resetLayout);
            App.SetLanguage("ko-KR");
            Assert.True(Application.Current!.TryGetResource("tracks.singerdefault", ThemeVariant.Default, out var singerDefault));
            Assert.Equal("(가수 기본값) ", singerDefault);
            Assert.True(Application.Current!.TryGetResource("pianoroll.tool.pitchline", ThemeVariant.Default, out var pitchLine));
            Assert.Contains("\n    ", (string)pitchLine!);
            App.SetLanguage("ja-JP");
            Assert.Equal("12平均律", XenStrings.PresetName(OpenUtau.Core.Microtonal.TuningPresets.EqualDivision(12)));
            Assert.Equal("3/2を13等分", XenStrings.PresetName(OpenUtau.Core.Microtonal.TuningPresets.EqualDivision(13,
                OpenUtau.Core.Microtonal.PitchInterval.FromRatio("3/2"))));
            Assert.Equal("ヴェルクマイスター III", XenStrings.PresetName(
                OpenUtau.Core.Microtonal.TuningPresets.All.Single(t => t.Id == "werckmeister-iii")));
            var renamed = OpenUtau.Core.Microtonal.TuningPresets.EqualDivision(19);
            renamed.Name = "My tuning";
            Assert.Equal("My tuning", XenStrings.PresetName(renamed));
            Assert.Equal("音律設定", XenStrings.Get("tuning.title"));
            App.SetLanguage("en-US");
            var dialog = new OpenUtau.App.Views.TuningDialog(projectDefault: true);
            dialog.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.InRange(dialog.Height, 320, 760);
            var numbers = dialog.GetVisualDescendants().OfType<NumericUpDown>().ToArray();
            Assert.NotEmpty(numbers);
            foreach (var number in numbers.Where(n => n.IsEffectivelyVisible)) {
                Assert.True(number.Bounds.Height >= 28);
            }
            VerifyTuningPages(dialog);
            dialog.Close();
            VerifyLoneAlt();
            DrawingGestureTest.Verify();
            RhythmMenuTest.Verify();
            GridPreferencesTest.Verify();
        }

        // A lone Alt, as left by IME switchers such as alt-ime-ahk whose masking key Avalonia drops,
        // must not open the main menu while text is edited, but still does elsewhere.
        static void VerifyLoneAlt() {
            var menu = new Menu { ItemsSource = new[] { new MenuItem { Header = "_File" } } };
            var lyricBox = new OpenUtau.App.Controls.LyricBox();
            var textBox = new TextBox();
            var button = new Button { Content = "Other" };
            var window = new Window { Content = new StackPanel { Children = { menu, lyricBox, textBox, button } } };
            void TapAlt() {
                window.KeyPress(Avalonia.Input.Key.LeftAlt, Avalonia.Input.RawInputModifiers.Alt, Avalonia.Input.PhysicalKey.AltLeft, null);
                window.KeyRelease(Avalonia.Input.Key.LeftAlt, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.AltLeft, null);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            }
            try {
                window.Show();
                lyricBox.DataContext!.GetType().GetProperty("IsVisible")!.SetValue(lyricBox.DataContext, true);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var box = lyricBox.FindControl<TextBox>("PART_Box")!;
                Assert.True(box.Focus());
                TapAlt();
                Assert.False(menu.IsOpen);
                Assert.True(box.IsFocused);
                Assert.True(textBox.Focus());
                TapAlt();
                Assert.False(menu.IsOpen);
                Assert.True(textBox.IsFocused);
                Assert.True(button.Focus());
                TapAlt();
                Assert.True(menu.IsOpen);
            } finally {
                window.Close();
            }
        }

        static void VerifyTuningPages(OpenUtau.App.Views.TuningDialog dialog) {
            void Layout() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var navigation = dialog.GetVisualDescendants().OfType<ListBox>().Single(c => c.Name == "TuningNavigation");
            Assert.Equal(4, navigation.ItemCount);
            Assert.DoesNotContain(dialog.GetVisualDescendants().OfType<Grid>(), c => c.Name == "TuningDegreeMap" || c.Name == "ScaleDegreeMap");
            Assert.DoesNotContain(dialog.GetVisualDescendants().OfType<Button>(), c => Equals(c.Content, XenStrings.Get("action.generateSubset")));
            var presets = dialog.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "TuningPresets");
            var keyboard = dialog.GetVisualDescendants().OfType<OpenUtau.App.Controls.NotchedKeyboardPreview>().Single();
            var generator = dialog.GetVisualDescendants().OfType<NumericUpDown>().Single(c => c.Name == "MosGenerator");
            var cardinality = dialog.GetVisualDescendants().OfType<NumericUpDown>().Single(c => c.Name == "MosCardinality");
            var mode = dialog.GetVisualDescendants().OfType<NumericUpDown>().Single(c => c.Name == "MosMode");
            var resultText = dialog.GetVisualDescendants().OfType<TextBlock>().Single(c => c.Name == "MosResult");
            string projectBefore = OpenUtau.Core.Microtonal.TunxenFormat.Serialize(OpenUtau.Core.DocManager.Inst.Project.pitchSystem);
            OpenUtau.Core.Microtonal.TuningDefinition Draft() {
                navigation.SelectedIndex = 3; Layout();
                dialog.GetVisualDescendants().OfType<Button>().Single(c => Equals(c.Content, XenStrings.Get("action.refreshDefinition")))
                    .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                var yaml = dialog.GetVisualDescendants().OfType<TextBox>().Single(c => c.Classes.Contains("definition-editor"));
                var draft = OpenUtau.Core.Microtonal.TunxenFormat.Parse(yaml.Text!);
                navigation.SelectedIndex = 0; Layout();
                return draft;
            }
            var scaleChoice = dialog.GetLogicalDescendants().OfType<ComboBox>().Single(c => c.Name == "TuningScale");
            var tonicChoice = dialog.GetLogicalDescendants().OfType<ComboBox>().Single(c => c.Name == "ScaleTonic");
            foreach (int division in new[] { 12, 31, 13 }) {
                var tuning = presets.ItemsSource!.Cast<OpenUtau.Core.Microtonal.TuningDefinition>().Single(t => t.Divisions == division
                    && Math.Abs(t.Period.ToCents() - (division == 13 ? 1200 * Math.Log2(3) : 1200)) < 0.001);
                presets.SelectedItem = tuning; Layout();
                Assert.Equal(division, keyboard.ItemCount);
                Assert.Equal(division == 12 || division == 13, keyboard.KeyLayout!.IsTraditionalKeyboard);
                var baselineKinds = keyboard.Cells.Select(cell => cell.Kind).ToArray();
                var geometry = keyboard.KeyLayout;
                // The scale does not change the keyboard.
                scaleChoice.SelectedIndex = 1; Layout();
                Assert.Equal(baselineKinds, keyboard.Cells.Select(cell => cell.Kind));
                var scaled = Draft();
                Assert.StartsWith(OpenUtau.Core.Microtonal.ScalePresets.Prefix, scaled.Keyboard.ActiveScaleId);
                Assert.Null(scaled.Keyboard.Mos);
                int nextCount = division == 31 ? 5 : division == 13 ? 10 : 8;
                cardinality.Value = nextCount; Layout();
                Assert.False(keyboard.IsWhiteKeysOnly);
                // A non-family MOS takes over the white keys and letter names; the scale stays as chosen.
                Assert.NotSame(geometry, keyboard.KeyLayout);
                Assert.False(keyboard.KeyLayout!.IsTraditionalKeyboard);
                var updated = Draft();
                Assert.Equal(scaled.Keyboard.ActiveScaleId, updated.Keyboard.ActiveScaleId);
                var notes = OpenUtau.Core.Microtonal.KeyboardRegionizer.MosDegrees(updated, updated.Keyboard.Mos!)!;
                Assert.Equal(nextCount, notes.Length);
                Assert.Equal(notes, keyboard.Cells.Where(cell => cell.Role == OpenUtau.Core.Microtonal.KeyRole.White).Select(cell => cell.Step));
                Assert.Equal("C", OpenUtau.Core.Microtonal.PitchNotation.Format(updated, 0, OpenUtau.Core.Microtonal.PitchNotation.Mos, false));
                mode.Value = 2; Layout();
                var expected = OpenUtau.Core.Microtonal.MosGenerator.Generate(tuning, (int)generator.Value!, nextCount, 1);
                Assert.Equal(expected.Scale.Degrees, keyboard.Cells.Where(cell => cell.Role == OpenUtau.Core.Microtonal.KeyRole.White).Select(cell => cell.Step));
                Assert.Contains("L", resultText.Text);
                mode.Value = 1; cardinality.Value = division == 13 ? 9 : 7; Layout();
                Assert.Equal(geometry.IsTraditionalKeyboard, keyboard.KeyLayout!.IsTraditionalKeyboard);
                Assert.Equal(baselineKinds, keyboard.Cells.Select(cell => cell.Kind));
                cardinality.Value = nextCount; Layout();
                decimal? oldGenerator = generator.Value;
                generator.Value = 0; Layout();
                Assert.Equal(XenStrings.Get("status.mosUnavailable"), resultText.Text);
                Assert.True(keyboard.IsWhiteKeysOnly);
                Assert.Equal(division, keyboard.ItemCount);
                Assert.All(keyboard.Cells, cell => Assert.Equal(OpenUtau.Core.Microtonal.KeyRole.White, cell.Role));
                Assert.Equal(division, keyboard.KeyLayout!.Columns.Count);
                Assert.Equal(scaled.Keyboard.ActiveScaleId, Draft().Keyboard.ActiveScaleId);
                generator.Value = oldGenerator; Layout();
                Assert.False(keyboard.IsWhiteKeysOnly);
                Assert.Equal(nextCount, keyboard.Cells.Count(cell => cell.Role == OpenUtau.Core.Microtonal.KeyRole.White));
            }
            // Rast keeps its own preset entry and the standard 24-EDO keyboard.
            var rast = presets.ItemsSource!.Cast<OpenUtau.Core.Microtonal.TuningDefinition>().Single(t => t.Id == "rast-24edo-approximation");
            presets.SelectedItem = rast; Layout();
            Assert.Same(rast, presets.SelectedItem);
            Assert.Equal(XenStrings.Get("scale.maqam-rast"), scaleChoice.SelectedItem);
            // Tonic and scale move the marked notes only.
            presets.SelectedItem = presets.ItemsSource!.Cast<OpenUtau.Core.Microtonal.TuningDefinition>()
                .Single(t => t.Id == "12edo");
            Layout();
            var twelveKinds = keyboard.Cells.Select(cell => cell.Kind).ToArray();
            scaleChoice.SelectedIndex = 1; Layout();
            tonicChoice.SelectedIndex = 2; Layout();
            var dMajor = Draft();
            Assert.Equal(2, dMajor.Keyboard.Tonic);
            Assert.Equal(new[] { 1, 2, 4, 6, 7, 9, 11 }, OpenUtau.Core.Microtonal.ScalePresets.ActiveDegrees(dMajor));
            Assert.Equal(twelveKinds, keyboard.Cells.Select(cell => cell.Kind));
            var divisions = dialog.GetVisualDescendants().OfType<NumericUpDown>().Single(c => c.Name == "TuningDivisions");
            var period = dialog.GetVisualDescendants().OfType<TextBox>().Single(c => c.Name == "TuningPeriod");
            var frequency = dialog.GetVisualDescendants().OfType<NumericUpDown>().Single(c => c.Name == "TuningReferenceFrequency");
            divisions.Value = 25; Layout();
            Assert.Equal(25, keyboard.ItemCount);
            generator.Value = 15; cardinality.Value = 7; Layout();
            Assert.True(keyboard.IsWhiteKeysOnly);
            generator.Value = 7; Layout();
            Assert.False(keyboard.IsWhiteKeysOnly);
            var validDraft = Draft();
            Assert.Equal(7, validDraft.Keyboard.Mos!.Generator);
            frequency.Value = 432; Layout();
            Assert.Equal(432, Draft().Reference.Frequency);
            period.Text = "3/"; Layout();
            Assert.Equal(0, keyboard.ItemCount);
            Assert.False(dialog.GetVisualDescendants().OfType<Button>().Single(c => c.Name == "ApplyTuningDraft").IsEnabled);
            period.Text = "3/1"; Layout();
            Assert.Equal(25, keyboard.ItemCount);
            Assert.Equal("3/1", Draft().Period.Ratio);
            VerifyMultiPeriodMos(dialog, presets, keyboard, Layout, Draft);
            VerifyKeyboardCycle(dialog, presets, keyboard, Layout, Draft);
            VerifyScaleEditing(dialog, presets, keyboard, Layout, Draft);
            Assert.Equal(projectBefore, OpenUtau.Core.Microtonal.TunxenFormat.Serialize(OpenUtau.Core.DocManager.Inst.Project.pitchSystem));
            foreach (double width in new[] { 960.0, 650.0 }) {
                dialog.Width = width; dialog.Height = width == 650 ? 440 : 760;
                for (int page = 0; page < 4; page++) {
                    navigation.SelectedIndex = page; Layout();
                    var viewport = dialog.GetVisualDescendants().OfType<ScrollViewer>()
                        .First(c => c.Content is StackPanel panel && panel.Children.FirstOrDefault() is TextBlock { FontSize: 20 });
                    Assert.True(viewport.Bounds.Width > 200);
                    Assert.True(viewport.Extent.Width <= viewport.Viewport.Width + 1);
                }
            }
            VerifyTraditionalProfiles();
            VerifyAudibleKeyboard();
            CaptureNotchedKeyboards();
        }

        static void VerifyMultiPeriodMos(OpenUtau.App.Views.TuningDialog dialog, ComboBox presets, OpenUtau.App.Controls.NotchedKeyboardPreview keyboard,
            Action layout, Func<OpenUtau.Core.Microtonal.TuningDefinition> draft) {
            presets.SelectedItem = presets.ItemsSource!.Cast<OpenUtau.Core.Microtonal.TuningDefinition>().Single(t => t.Id == "12edo");
            layout();
            NumericUpDown Number(string name) => dialog.GetVisualDescendants().OfType<NumericUpDown>().Single(c => c.Name == name);
            var generator = Number("MosGenerator");
            var cardinality = Number("MosCardinality");
            var periods = Number("MosPeriods");
            var mode = Number("MosMode");
            var resultText = dialog.GetVisualDescendants().OfType<TextBlock>().Single(c => c.Name == "MosResult");
            // 12-EDO diminished: two notes from a one-step generator, repeated in each minor third.
            generator.Value = 1; cardinality.Value = 8; periods.Value = 4; layout();
            Assert.Equal(2, mode.Maximum);
            Assert.StartsWith("4L4s", resultText.Text);
            Assert.False(keyboard.KeyLayout!.IsTraditionalKeyboard);
            Assert.Equal(new[] { 0, 2, 3, 5, 6, 8, 9, 11 },
                keyboard.Cells.Where(cell => cell.Role == OpenUtau.Core.Microtonal.KeyRole.White).Select(cell => cell.Step));
            Assert.Equal(4, draft().Keyboard.Mos!.Periods);
            periods.Value = 5; layout();
            Assert.Equal(XenStrings.Get("status.mosUnavailable"), resultText.Text);
            Assert.True(keyboard.IsWhiteKeysOnly);
            periods.Value = 1; layout();
            Assert.Equal(8, mode.Maximum);
            Assert.Null(draft().Keyboard.Mos!.Periods);
            generator.Value = 7; cardinality.Value = 7; layout();
            Assert.True(keyboard.KeyLayout!.IsTraditionalKeyboard);
            // A repeat count edited on its own survives a structure change, like generator and note count.
            var twelve = presets.SelectedItem;
            presets.SelectedItem = null; layout();
            presets.SelectedItem = twelve; layout();
            periods.Value = 4; layout();
            Number("TuningDivisions").Value = 24; layout();
            Assert.Equal(4, periods.Value);
            Assert.Equal(4, draft().Keyboard.Mos!.Periods);
            presets.SelectedItem = twelve; layout();
            Assert.Equal(1, periods.Value);
            // The MOS list selects the family diatonic and applies a listed MOS to the fields.
            var choices = dialog.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "MosChoices");
            // Improper MOS are hidden unless the preference allows them.
            var all = OpenUtau.Core.Microtonal.MosGenerator.Choices(12);
            var listed = all.Where(c => OpenUtau.Core.Util.Preferences.Default.XenAllowImproperMos || c.IsProper).ToList();
            Assert.Contains(all, c => !c.IsProper);
            // Each note count starts with a heading that cannot be chosen.
            int groups = listed.Select(c => c.Mos.Cardinality).Distinct().Count();
            Assert.Equal(listed.Count + groups, choices.ItemCount);
            var rows = choices.Items.Cast<ComboBoxItem>().ToArray();
            Assert.Equal(XenStrings.Get("mos.group", 4), rows[0].Content);
            Assert.False(rows[0].IsEnabled);
            Assert.Equal(groups, rows.Count(row => !row.IsEnabled));
            string Selected() => (string)((ComboBoxItem)choices.SelectedItem!).Content!;
            int Row(Func<OpenUtau.Core.Microtonal.MosChoice, bool> match) {
                int i = listed.FindIndex(c => match(c));
                return i + listed.Take(i + 1).Select(c => c.Mos.Cardinality).Distinct().Count();
            }
            Assert.StartsWith("5L2s", Selected());
            choices.SelectedIndex = 0; layout();
            Assert.Equal(7m, generator.Value);
            Assert.StartsWith("5L2s", Selected());
            choices.SelectedIndex = Row(c => c.Periods == 4 && c.Mos.Cardinality == 8); layout();
            Assert.Equal((1m, 8m, 4m, 1m), (generator.Value!.Value, cardinality.Value!.Value, periods.Value!.Value, mode.Value!.Value));
            Assert.StartsWith("4L4s", resultText.Text);
            Assert.Equal(4, draft().Keyboard.Mos!.Periods);
            choices.SelectedIndex = Row(c => c.Mos.Cardinality == 7 && c.LargeCount == 5); layout();
            Assert.Equal((7m, 7m, 1m), (generator.Value!.Value, cardinality.Value!.Value, periods.Value!.Value));
            Assert.True(keyboard.KeyLayout!.IsTraditionalKeyboard);
            // Fields that give no MOS leave the list on its placeholder.
            generator.Value = 0; layout();
            Assert.Equal(-1, choices.SelectedIndex);
            generator.Value = 7; layout();
            Assert.StartsWith("5L2s", Selected());
            bool allowed = OpenUtau.Core.Util.Preferences.Default.XenAllowImproperMos;
            try {
                OpenUtau.Core.Util.Preferences.Default.XenAllowImproperMos = !allowed;
                generator.Value = 5; layout(); generator.Value = 7; layout();
                var shown = all.Where(c => !allowed || c.IsProper).ToList();
                Assert.Equal(shown.Count + shown.Select(c => c.Mos.Cardinality).Distinct().Count(), choices.ItemCount);
            } finally {
                OpenUtau.Core.Util.Preferences.Default.XenAllowImproperMos = allowed;
                generator.Value = 5; layout(); generator.Value = 7; layout();
            }
            Assert.DoesNotContain(XenStrings.Get("mos.improper"), resultText.Text);
            // A 53-EDO diatonic keyboard stays available but its status says that it is not proper.
            presets.SelectedItem = presets.ItemsSource!.Cast<OpenUtau.Core.Microtonal.TuningDefinition>().Single(t => t.Id == "53edo");
            layout();
            Assert.False(keyboard.IsWhiteKeysOnly);
            Assert.EndsWith(XenStrings.Get("mos.improper"), resultText.Text);
        }

        static void VerifyKeyboardCycle(OpenUtau.App.Views.TuningDialog dialog, ComboBox presets, OpenUtau.App.Controls.NotchedKeyboardPreview keyboard,
            Action layout, Func<OpenUtau.Core.Microtonal.TuningDefinition> draft) {
            presets.SelectedItem = presets.ItemsSource!.Cast<OpenUtau.Core.Microtonal.TuningDefinition>().Single(t => t.Id == "carlos-alpha");
            layout();
            var cycle = dialog.GetLogicalDescendants().OfType<NumericUpDown>().Single(c => c.Name == "KeyboardCycle");
            Assert.True(cycle.IsEffectivelyVisible);
            Assert.Equal(9, keyboard.ItemCount);
            cycle.Value = 15; layout();
            var regrouped = draft();
            Assert.Equal(15, regrouped.Divisions);
            Assert.Equal(77.965, regrouped.GetCents(1), 6);
            presets.SelectedItem = presets.ItemsSource!.Cast<OpenUtau.Core.Microtonal.TuningDefinition>().Single(t => t.Id == "12edo");
            layout();
            Assert.False(cycle.IsEffectivelyVisible);
        }

        static void VerifyScaleEditing(OpenUtau.App.Views.TuningDialog dialog, ComboBox presets, OpenUtau.App.Controls.NotchedKeyboardPreview keyboard,
            Action layout, Func<OpenUtau.Core.Microtonal.TuningDefinition> draft) {
            presets.SelectedItem = presets.ItemsSource!.Cast<OpenUtau.Core.Microtonal.TuningDefinition>().Single(t => t.Id == "12edo");
            layout();
            var mosKind = dialog.GetVisualDescendants().OfType<RadioButton>().Single(c => c.Name == "KeyboardKindMos");
            var manualKind = dialog.GetLogicalDescendants().OfType<RadioButton>().Single(c => c.Name == "KeyboardKindManual");
            var manualKeys = dialog.GetLogicalDescendants().OfType<WrapPanel>().Single(c => c.Name == "ManualKeyClasses");
            var generator = dialog.GetVisualDescendants().OfType<NumericUpDown>().Single(c => c.Name == "MosGenerator");
            var cardinality = dialog.GetVisualDescendants().OfType<NumericUpDown>().Single(c => c.Name == "MosCardinality");
            var editScale = dialog.GetLogicalDescendants().OfType<CheckBox>().Single(c => c.Name == "EditScaleKeys");
            cardinality.Value = 5; layout();
            var pentatonic = keyboard.Cells.Select(cell => cell.Role).ToArray();
            Assert.True(mosKind.IsChecked);
            Assert.False(manualKeys.IsEffectivelyVisible);
            manualKind.IsChecked = true; layout();
            Assert.True(manualKeys.IsEffectivelyVisible);
            Assert.False(generator.IsEffectivelyVisible);
            // Key by key starts from the current keys and names.
            Assert.Equal(pentatonic, keyboard.Cells.Select(cell => cell.Role));
            var off = draft();
            Assert.True(OpenUtau.Core.Microtonal.KeyboardRegionizer.IsManual(off));
            Assert.Equal(12, off.Keyboard.Classes.Count);
            Assert.Equal("C", OpenUtau.Core.Microtonal.PitchNotation.Format(off, 0, OpenUtau.Core.Microtonal.PitchNotation.ResolvePrimary(off, null), false));
            keyboard.SelectDegreeAt(new Point(1, 1));
            int selected = keyboard.SelectedDegree!.Value;
            dialog.GetLogicalDescendants().OfType<Button>().Single(c => c.Name == "KeyClass-accidental")
                .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            layout();
            Assert.Equal(OpenUtau.Core.Microtonal.KeyRole.Black, keyboard.Cells[selected].Role);
            Assert.Equal("accidental", draft().Keyboard.Classes[selected]);
            mosKind.IsChecked = true; layout();
            Assert.False(manualKeys.IsEffectivelyVisible);
            var on = draft();
            Assert.Empty(on.Keyboard.Classes);
            Assert.DoesNotContain(on.Notations, profile => profile.Id == "custom");
            Assert.Equal(OpenUtau.Core.Microtonal.PitchNotation.Mos, OpenUtau.Core.Microtonal.PitchNotation.ResolvePrimary(on, null));
            Assert.Equal(pentatonic, keyboard.Cells.Select(cell => cell.Role));
            // Clicking keys while editing copies the scale into a user scale and toggles notes, keys unchanged.
            editScale.IsChecked = true; layout();
            var window = (Window)dialog;
            Point PointFor(int degree) {
                var rectangle = keyboard.KeyLayout!.Keys[degree].Rectangles.OrderByDescending(r => r.Width * r.Height).First();
                var point = new Point((rectangle.X + rectangle.Right) / 2 * keyboard.Bounds.Width / keyboard.KeyLayout.Width,
                    (rectangle.Y + rectangle.Bottom) / 2 * keyboard.Bounds.Height);
                return keyboard.TranslatePoint(point, window)!.Value;
            }
            keyboard.BringIntoView();
            layout();
            window.MouseDown(PointFor(3), Avalonia.Input.MouseButton.Left);
            window.MouseUp(PointFor(3), Avalonia.Input.MouseButton.Left);
            layout();
            var edited = draft();
            Assert.StartsWith("custom-", edited.Keyboard.ActiveScaleId);
            Assert.DoesNotContain(3, OpenUtau.Core.Microtonal.ScalePresets.ActiveDegrees(edited)!);
            Assert.Equal(pentatonic, keyboard.Cells.Select(cell => cell.Role));
            editScale.IsChecked = false;
        }

        static void VerifyAudibleKeyboard() {
            var events = new System.Collections.Generic.List<(bool Start, double Frequency)>();
            var keyboard = new OpenUtau.App.Controls.NotchedKeyboardPreview(
                frequency => events.Add((true, frequency)), frequency => events.Add((false, frequency)));
            var tuning = OpenUtau.Core.Microtonal.TuningPresets.EqualDivision(53);
            keyboard.SetTuning(tuning);
            var window = new Window { Width = 500, Height = 240, Content = new ScrollViewer {
                Content = keyboard, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            } };
            try {
                window.Show(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Point PointFor(int degree) {
                    var rectangle = keyboard.KeyLayout!.Keys[degree].Rectangles.OrderByDescending(r => r.Width * r.Height).First();
                    var point = new Point((rectangle.X + rectangle.Right) / 2 * keyboard.Bounds.Width / keyboard.KeyLayout.Width,
                        (rectangle.Y + rectangle.Bottom) / 2 * keyboard.Bounds.Height);
                    return keyboard.TranslatePoint(point, window)!.Value;
                }
                double Frequency(int degree) => OpenUtau.Core.Microtonal.PitchResolver.ResolveFrequency(tuning,
                    new OpenUtau.Core.Microtonal.PitchAddress { Degree = degree });
                window.MouseDown(PointFor(0), Avalonia.Input.MouseButton.Left);
                window.MouseMove(PointFor(1), Avalonia.Input.RawInputModifiers.LeftMouseButton);
                window.MouseUp(PointFor(1), Avalonia.Input.MouseButton.Left);
                Assert.Equal(new[] { (true, Frequency(0)), (false, Frequency(0)), (true, Frequency(1)), (false, Frequency(1)) }, events);
                window.MouseDown(PointFor(0), Avalonia.Input.MouseButton.Left);
                keyboard.SetTuning(tuning);
                window.MouseUp(PointFor(0), Avalonia.Input.MouseButton.Left);
                Assert.Equal(6, events.Count);
                Assert.Equal((false, Frequency(0)), events[5]);
                window.MouseDown(PointFor(0), Avalonia.Input.MouseButton.Left);
            } finally { window.Close(); }
            Assert.Equal(8, events.Count);
            Assert.Equal((false, OpenUtau.Core.Microtonal.PitchResolver.ResolveFrequency(tuning,
                new OpenUtau.Core.Microtonal.PitchAddress { Degree = 0 })), events[7]);
        }

        static void VerifyTraditionalProfiles() {
            var keyboard = new OpenUtau.App.Controls.NotchedKeyboardPreview();
            foreach (var (n, period, traditional) in new[] { (12, "2/1", true), (13, "3/1", true), (12, "3/1", false), (13, "2/1", false) }) {
                keyboard.SetTuning(OpenUtau.Core.Microtonal.TuningPresets.EqualDivision(n,
                    OpenUtau.Core.Microtonal.PitchInterval.FromRatio(period)));
                Assert.Equal(traditional, keyboard.KeyLayout!.IsTraditionalKeyboard);
            }
            var custom = OpenUtau.Core.Microtonal.TuningPresets.EqualDivision(12);
            custom.Keyboard.Classes = OpenUtau.Core.Microtonal.KeyboardRegionizer.ClassesFromRoles(custom);
            custom.Keyboard.Classes[1] = "natural";
            custom.Keyboard.Layout = OpenUtau.Core.Microtonal.KeyboardLayout.ManualLayout;
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
                    CaptureUnifiedTuning(directory, theme);
                    CaptureAccidentalFallback(directory, theme);
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

        static void CaptureAccidentalFallback(string directory, string theme) {
            var tuning = OpenUtau.Core.Microtonal.TuningPresets.EqualDivision(31);
            var names = Enumerable.Range(0, 4).Select(d => OpenUtau.Core.Microtonal.PitchNotation.Format(tuning, d, "szg", false)).ToArray();
            var panel = new StackPanel { Margin = new Thickness(8), Spacing = 6 };
            panel.Children.Add(new TextBlock { Text = string.Join("  ", names), FontSize = 16 });
            panel.Children.Add(new OpenUtau.App.Controls.PitchText { Pitch = string.Join("  ", names), FontSize = 16 });
            var quarter = OpenUtau.Core.Microtonal.TuningPresets.EqualDivision(24);
            panel.Children.Add(new OpenUtau.App.Controls.PitchText { FontSize = 12, Pitch = string.Join("  ",
                new[] { 3, 6, 7, 11, 21 }.Select(d => OpenUtau.Core.Microtonal.PitchNotation.Format(quarter, d, "szg"))) });
            panel.Children.Add(new ComboBox { ItemsSource = names, SelectedIndex = 1, Width = 160, FontSize = 16,
                ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((label, _) => new OpenUtau.App.Controls.PitchText { Pitch = label }) });
            var window = new Window { Width = 260, Height = 170, Content = panel };
            try {
                window.Show(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                using (var frame = window.CaptureRenderedFrame()) frame?.Save(System.IO.Path.Combine(directory, $"accidental-fallback-{theme}.png"));
            } finally { window.Close(); }
        }

        static void CaptureUnifiedTuning(string directory, string theme) {
            App.SetLanguage("ja-JP");
            var dialog = new OpenUtau.App.Views.TuningDialog(projectDefault: true);
            try {
                dialog.Show(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var presets = dialog.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "TuningPresets");
                var items = presets.ItemsSource!.Cast<OpenUtau.Core.Microtonal.TuningDefinition>().ToArray();
                presets.SelectedItem = items.Single(t => t.Divisions == 31 && Math.Abs(t.PeriodCents - 1200) < 0.001);
                dialog.GetVisualDescendants().OfType<NumericUpDown>().Single(c => c.Name == "MosMode").Value = 2;
                // C half-sharp: the closed tonic list shows a SMuFL accidental through the Xen Accidentals fallback.
                dialog.GetLogicalDescendants().OfType<ComboBox>().Single(c => c.Name == "ScaleTonic").SelectedIndex = 1;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                using (var frame = dialog.CaptureRenderedFrame()) frame?.Save(System.IO.Path.Combine(directory, $"unified-tuning-ja-{theme}.png"));
                presets.SelectedItem = items.Single(t => t.Divisions == 12 && Math.Abs(t.PeriodCents - 1200) < 0.001);
                dialog.GetVisualDescendants().OfType<NumericUpDown>().Single(c => c.Name == "TuningDivisions").Value = 25;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                using (var frame = dialog.CaptureRenderedFrame()) frame?.Save(System.IO.Path.Combine(directory, $"unified-no-mos-ja-{theme}.png"));
                presets.SelectedItem = items.Single(t => t.Divisions == 31 && Math.Abs(t.PeriodCents - 1200) < 0.001);
                dialog.GetVisualDescendants().OfType<NumericUpDown>().Single(c => c.Name == "MosCardinality").Value = 5;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                using (var frame = dialog.CaptureRenderedFrame()) frame?.Save(System.IO.Path.Combine(directory, $"unified-letters-ja-{theme}.png"));
                dialog.GetVisualDescendants().OfType<RadioButton>().Single(c => c.Name == "KeyboardKindManual").IsChecked = true;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                using (var frame = dialog.CaptureRenderedFrame()) frame?.Save(System.IO.Path.Combine(directory, $"unified-manual-ja-{theme}.png"));
                presets.SelectedItem = items.Single(t => t.Id == "ji-5-limit");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                using (var frame = dialog.CaptureRenderedFrame()) frame?.Save(System.IO.Path.Combine(directory, $"unified-ji-off-ja-{theme}.png"));
                dialog.GetVisualDescendants().OfType<RadioButton>().Single(c => c.Name == "KeyboardKindMos").IsChecked = true;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                using (var frame = dialog.CaptureRenderedFrame()) frame?.Save(System.IO.Path.Combine(directory, $"unified-ji-on-ja-{theme}.png"));
                presets.SelectedItem = items.Single(t => t.Id == "rast-24edo-approximation");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                using (var frame = dialog.CaptureRenderedFrame()) frame?.Save(System.IO.Path.Combine(directory, $"unified-rast-ja-{theme}.png"));
                presets.SelectedItem = items.Single(t => t.Id == OpenUtau.Core.Microtonal.TuningPresets.AeuId);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                using (var frame = dialog.CaptureRenderedFrame()) frame?.Save(System.IO.Path.Combine(directory, $"unified-aeu-ja-{theme}.png"));
            } finally { dialog.Close(); App.SetLanguage("en-US"); }
        }
    }
}
