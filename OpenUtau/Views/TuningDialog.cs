using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using OpenUtau.App.ViewModels;
using OpenUtau.Core;
using OpenUtau.Core.Microtonal;
using OpenUtau.Core.Ustx;

namespace OpenUtau.App.Views {
    /// <summary>Edits a detached tuning, then applies one undoable project or track operation.</summary>
    public sealed class TuningDialog : Window {
        readonly NotesViewModel? notes;
        UProject Project => DocManager.Inst.Project;
        UTrack? CurrentTrack => notes?.Part is UVoicePart part && part.trackNo >= 0 && part.trackNo < Project.tracks.Count
            ? Project.tracks[part.trackNo] : null;
        TuningDefinition EffectiveTuning => Project.GetTuning(CurrentTrack);
        TuningDefinition draft;
        readonly ComboBox scope = new ComboBox { ItemsSource = new[] { XenStrings.Get("scope.track"), XenStrings.Get("scope.project") }, SelectedIndex = 0 };
        readonly ComboBox presets = new ComboBox { ItemsSource = TuningPresets.All };
        readonly TextBox name = new TextBox();
        readonly NumericUpDown divisions = Number(0, 65536, 12, 0);
        readonly NumericUpDown explicitCount = Number(1, 65536, 12, 0);
        readonly TextBox period = new TextBox();
        readonly NumericUpDown referenceDegree = Number(-1000000, 1000000, 9, 0);
        readonly NumericUpDown frequency = Number(0.001m, 1000000000, 440, 6);
        readonly ComboBox scales = new ComboBox();
        readonly CheckBox preserve = new CheckBox { Content = XenStrings.Get("preserve") };
        readonly DataGrid degrees = new DataGrid { AutoGenerateColumns = false, MinHeight = 180 };
        readonly TextBox yaml = new TextBox { AcceptsReturn = true, AcceptsTab = true, FontFamily = new Avalonia.Media.FontFamily("Consolas"), MinHeight = 250, TextWrapping = Avalonia.Media.TextWrapping.NoWrap };
        readonly TextBlock status = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        readonly NumericUpDown generator = Number(-65536, 65536, 31, 0);
        readonly NumericUpDown cardinality = Number(1, 65536, 7, 0);
        readonly NumericUpDown mode = Number(1, 65536, 1, 0);
        readonly NumericUpDown intervalCardinality = Number(1, 65536, 7, 0);
        readonly TextBox ratioGenerator = new TextBox { Text = "3/2" };
        readonly TextBox customScaleName = new TextBox { Text = XenStrings.Get("scale.custom") };
        readonly TextBox customScaleDegrees = new TextBox { Text = "0" };
        readonly TextBlock scaleResult = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        readonly TextBlock tuningSummary = new TextBlock { FontSize = 18, FontWeight = Avalonia.Media.FontWeight.SemiBold };
        readonly TextBlock scaleSummary = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        readonly ListBox pitchPreview = new ListBox { Height = 220, Name = "TuningPitchPreview" };
        readonly ListBox scalePreview = new ListBox { Height = 220, Name = "ScalePitchPreview" };
        readonly ListBox generatorPreview = new ListBox { Height = 180, Name = "GeneratorPitchPreview" };
        readonly Grid degreeMap = new Grid { Name = "TuningDegreeMap", ColumnSpacing = 3, RowSpacing = 3 };
        readonly Grid subsetMap = new Grid { Name = "ScaleDegreeMap", ColumnSpacing = 3, RowSpacing = 3 };
        bool loading;
        int selectedPage;
        Button applyDraft = null!;
        List<DegreeEntry> entries = new List<DegreeEntry>();

        public TuningDialog(NotesViewModel? notes = null, bool projectDefault = false) {
            this.notes = notes;
            draft = EffectiveTuning.Clone();
            presets.Name = "TuningPresets";
            generator.Name = "MosGenerator";
            cardinality.Name = "MosCardinality";
            mode.Name = "MosMode";
            presets.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<TuningDefinition>(
                (tuning, _) => new TextBlock { Text = tuning == null ? "" : XenStrings.PresetName(tuning) });
            Title = XenStrings.Get("tuning.title");
            Width = 960;
            Height = 620;
            MinWidth = 650;
            MinHeight = 440;
            XenDialogStyles.Apply(this);
            yaml.Classes.Add("definition-editor");
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var root = new Grid {
                Margin = new Thickness(16), RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 12,
            };
            root.Children.Add(Field(XenStrings.Get("field.scope"), scope));
            var footer = new StackPanel { Spacing = 6 };
            footer.Children.Add(status);
            var commands = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            applyDraft = Button(XenStrings.Get("action.applyTuning"), () => Apply(false, UsesDisplayApply()));
            applyDraft.Name = "ApplyTuningDraft";
            commands.Children.Add(applyDraft);
            commands.Children.Add(Button(XenStrings.Get("action.close"), Close));
            footer.Children.Add(commands);
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            var tuningPage = Page("tab.tuning");
            tuningPage.Children.Add(Field(XenStrings.Get("field.preset"), presets));
            tuningPage.Children.Add(Field(XenStrings.Get("field.name"), name));
            tuningPage.Children.Add(Field(XenStrings.Get("field.period"), period));
            tuningPage.Children.Add(Field(XenStrings.Get("field.divisions"), divisions));
            tuningPage.Children.Add(Field(XenStrings.Get("field.explicitCount"), explicitCount));
            tuningPage.Children.Add(Button(XenStrings.Get("action.rebuild"), RebuildEntries));
            tuningPage.Children.Add(tuningSummary);
            tuningPage.Children.Add(degreeMap);
            tuningPage.Children.Add(pitchPreview);
            tuningPage.Children.Add(new Separator());
            tuningPage.Children.Add(Field(XenStrings.Get("field.referenceDegree"), referenceDegree));
            tuningPage.Children.Add(Field(XenStrings.Get("field.frequency"), frequency));
            tuningPage.Children.Add(Button(XenStrings.Get("action.changeReference"), () => Apply(true, false)));

            var scalePage = Page("field.scale");
            scalePage.Children.Add(scaleSummary);
            scalePage.Children.Add(Field(XenStrings.Get("field.scale"), scales));
            scalePage.Children.Add(subsetMap);
            scalePage.Children.Add(new Expander {
                Header = XenStrings.Get("field.scale"), Content = scalePreview, IsExpanded = false,
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            });
            var mos = Page("tab.generator");
            mos.Children.Add(Field(XenStrings.Get("field.generatorDegree"), generator));
            mos.Children.Add(Field(XenStrings.Get("field.cardinality"), cardinality));
            mos.Children.Add(Field(XenStrings.Get("field.mode"), mode));
            mos.Children.Add(scaleResult);
            mos.Children.Add(Button(XenStrings.Get("action.generateSubset"), GenerateScale));
            var generatorBody = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 12 };
            generatorBody.Children.Add(mos);
            Grid.SetColumn(generatorPreview, 1);
            generatorBody.Children.Add(generatorPreview);
            // Stack at narrow window widths; each page keeps its own vertical scroll area.
            generatorBody.SizeChanged += (_, _) => {
                bool narrow = generatorBody.Bounds.Width < 560;
                if (narrow == (generatorBody.ColumnDefinitions.Count == 1)) return;
                generatorBody.ColumnDefinitions = new ColumnDefinitions(narrow ? "*" : "*,*");
                generatorBody.RowDefinitions = new RowDefinitions(narrow ? "Auto,Auto" : "Auto");
                Grid.SetColumn(generatorPreview, narrow ? 0 : 1);
                Grid.SetRow(generatorPreview, narrow ? 1 : 0);
            };
            scalePage.Children.Add(Card(generatorBody));
            var custom = new StackPanel { Spacing = 10 };
            custom.Children.Add(Field(XenStrings.Get("field.scaleName"), customScaleName));
            custom.Children.Add(Field(XenStrings.Get("field.degrees"), customScaleDegrees));
            custom.Children.Add(Button(XenStrings.Get("action.addSubset"), AddDegreeSubset));
            scalePage.Children.Add(new Expander {
                Header = XenStrings.Get("scale.custom"), Content = Card(custom), IsExpanded = false,
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            });

            degrees.Columns.Add(new DataGridTextColumn { Header = XenStrings.Get("field.degree"), Binding = new Binding(nameof(DegreeEntry.Degree)), IsReadOnly = true });
            degrees.Columns.Add(new DataGridTextColumn { Header = XenStrings.Get("field.interval"), Binding = new Binding(nameof(DegreeEntry.Interval)) });
            degrees.Columns.Add(new DataGridTextColumn { Header = XenStrings.Get("field.name"), Binding = new Binding(nameof(DegreeEntry.Name)) });
            degrees.Columns.Add(new DataGridTextColumn { Header = XenStrings.Get("field.aliases"), Binding = new Binding(nameof(DegreeEntry.Aliases)) });
            degrees.Columns.Add(new DataGridTextColumn { Header = XenStrings.Get("field.keyClass"), Binding = new Binding(nameof(DegreeEntry.KeyClass)) });
            // Give the table a bounded viewport so its own scrolling and virtualisation work.
            degrees.Height = 320;
            var keyboardPage = Page("field.display");
            keyboardPage.Children.Add(degrees);

            var conversionPage = Page("tab.conversion");
            conversionPage.Children.Add(preserve);
            conversionPage.Children.Add(Button(XenStrings.Get("action.preview"), Preview));
            conversionPage.Children.Add(Button(XenStrings.Get("action.inherit"), InheritProjectTuning));

            var advanced = Page("tab.definition");
            var files = new WrapPanel();
            files.Children.Add(Button(XenStrings.Get("import.tuning"), ImportTuning));
            files.Children.Add(Button(XenStrings.Get("export.tuning"), ExportTuning));
            files.Children.Add(Button(XenStrings.Get("import.scala"), ImportScala));
            files.Children.Add(Button(XenStrings.Get("export.scala"), ExportScala));
            files.Children.Add(Button(XenStrings.Get("import.kbm"), ImportKbm));
            files.Children.Add(Button(XenStrings.Get("export.kbm"), ExportKbm));
            advanced.Children.Add(files);
            var interval = new StackPanel { Spacing = 10 };
            // Interval generation replaces the mother tuning; it is not an EDO subset.
            interval.Children.Add(Field(XenStrings.Get("field.generatorInterval"), ratioGenerator));
            interval.Children.Add(Field(XenStrings.Get("field.cardinality"), intervalCardinality));
            interval.Children.Add(Button(XenStrings.Get("action.generateTuning"), GenerateIntervalTuning));
            advanced.Children.Add(Card(interval));
            yaml.Height = 300;
            advanced.Children.Add(yaml);
            var yamlActions = new WrapPanel();
            yamlActions.Children.Add(Button(XenStrings.Get("action.loadDefinition"), ReadYaml));
            yamlActions.Children.Add(Button(XenStrings.Get("action.refreshDefinition"), () => Guard(() => {
                ReadFields(); yaml.Text = TunxenFormat.Serialize(draft);
            })));
            advanced.Children.Add(yamlActions);

            var pages = new[] { tuningPage, scalePage, keyboardPage, conversionPage, advanced }
                .Select(page => new ScrollViewer {
                    Content = page, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                }).ToArray();
            var navigation = new ListBox {
                Name = "TuningNavigation", ItemsSource = new[] {
                    XenStrings.Get("tab.tuning"), XenStrings.Get("field.scale"),
                    XenStrings.Get("field.display"), XenStrings.Get("tab.conversion"), XenStrings.Get("tab.definition"),
                },
            };
            var pageHost = new ContentControl { Content = pages[0], HorizontalContentAlignment = HorizontalAlignment.Stretch };
            navigation.SelectionChanged += (_, _) => {
                if (navigation.SelectedIndex < 0) return;
                selectedPage = navigation.SelectedIndex;
                pageHost.Content = pages[selectedPage];
                RefreshApplyAction();
            };
            navigation.SelectedIndex = 0;
            var body = new Grid { ColumnDefinitions = new ColumnDefinitions("150,*"), ColumnSpacing = 16 };
            body.Children.Add(navigation);
            Grid.SetColumn(pageHost, 1);
            body.Children.Add(pageHost);
            Grid.SetRow(body, 1);
            root.Children.Add(body);
            Content = root;
            ConfigurePitchPreview(pitchPreview);
            ConfigurePitchPreview(scalePreview);
            ConfigurePitchPreview(generatorPreview);
            generator.ValueChanged += (_, _) => RefreshGeneratorPreview();
            cardinality.ValueChanged += (_, _) => RefreshGeneratorPreview();
            mode.ValueChanged += (_, _) => RefreshGeneratorPreview();
            scales.SelectionChanged += (_, _) => { if (!loading) RefreshPitchPreviews(); };
            presets.SelectionChanged += (_, _) => {
                if (presets.SelectedItem is TuningDefinition selected) {
                    draft = selected.Clone();
                    draft.Name = XenStrings.PresetName(selected);
                    LoadFields();
                }
            };
            scope.SelectionChanged += (_, _) => {
                draft = (scope.SelectedIndex == 1 ? Project.pitchSystem : EffectiveTuning).Clone();
                LoadFields();
            };
            if (projectDefault || CurrentTrack == null) scope.SelectedIndex = 1;
            LoadFields();
        }

        void LoadFields() {
            loading = true;
            name.Text = draft.Name;
            divisions.Value = draft.Divisions;
            explicitCount.Value = draft.DegreeCount;
            period.Text = draft.Period.Ratio ?? draft.Period.Cents?.ToString("R", XenStrings.InputCulture);
            referenceDegree.Value = draft.Reference.Degree;
            frequency.Value = (decimal)draft.Reference.Frequency;
            scales.ItemsSource = draft.Scales.Select(s => s.Id == "all" ? XenStrings.Get("scale.all")
                : s.Id == "rast" ? XenStrings.Get("preset.rast") : s.Name).ToArray();
            scales.SelectedIndex = Math.Max(0, draft.Scales.FindIndex(s => s.Id == draft.Keyboard.ActiveScaleId));
            LoadEntries();
            yaml.Text = TunxenFormat.Serialize(draft);
            if (explicitCount.Parent is Control explicitRow) explicitRow.IsVisible = draft.Divisions == 0;
            var active = draft.Scales.FirstOrDefault(s => s.Id == draft.Keyboard.ActiveScaleId);
            var theory = KeyboardTheory.For(draft);
            generator.Value = active?.GeneratorDegree ?? theory?.Generator ?? 1;
            cardinality.Maximum = draft.DegreeCount;
            cardinality.Value = active?.GeneratorDegree != null ? active.Degrees.Count
                : Math.Min(theory?.ReferenceDivision == 13 ? 9 : 7, draft.DegreeCount);
            mode.Maximum = cardinality.Value ?? 1;
            mode.Value = (active?.Mode ?? 0) + 1;
            loading = false;
            RefreshPitchPreviews();
            RefreshGeneratorPreview();
            RefreshApplyAction();
        }

        bool MatchesAppliedTuning() {
            var applied = Project.GetTuning(scope.SelectedIndex == 1 ? null : CurrentTrack);
            return applied.DegreeCount == draft.DegreeCount && Math.Abs(applied.PeriodCents - draft.PeriodCents) < 1e-8
                && Enumerable.Range(0, draft.DegreeCount).All(d => Math.Abs(applied.GetCents(d) - draft.GetCents(d)) < 1e-8);
        }

        bool UsesDisplayApply() => (selectedPage == 1 || selectedPage == 2) && MatchesAppliedTuning();

        void RefreshApplyAction() => applyDraft.Content = XenStrings.Get(UsesDisplayApply() ? "action.applyDisplay" : "action.applyTuning");
        static StackPanel Page(string heading) {
            var page = new StackPanel { Spacing = 12, Margin = new Thickness(0, 0, 12, 12) };
            page.Children.Add(new TextBlock {
                Text = XenStrings.Get(heading), FontSize = 20, FontWeight = Avalonia.Media.FontWeight.SemiBold,
            });
            return page;
        }

        static Border Card(Control child) {
            var card = new Border { Child = child, Padding = new Thickness(12), CornerRadius = new CornerRadius(6) };
            card.Bind(Border.BackgroundProperty, new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("WorkspaceCardBrush"));
            return card;
        }

        sealed class PreviewPitch {
            public TuningDefinition Tuning { get; set; } = null!;
            public string Notation { get; set; } = "degree";
            public int Degree { get; set; }
            public string Label => PitchNotation.Format(Tuning, Degree, Notation, false);
            public string Cents => Tuning.GetCents(Degree).ToString("0.##", XenStrings.InputCulture) + " ¢";
            public KeyRole Role => PitchNotation.Role(Tuning, Degree);
            public bool Selected { get; set; }
        }

        sealed class PitchPreviewLabel : Control {
            public static readonly StyledProperty<Avalonia.Media.IBrush?> ForegroundProperty =
                TextBlock.ForegroundProperty.AddOwner<PitchPreviewLabel>();
            public Avalonia.Media.IBrush? Foreground => GetValue(ForegroundProperty);
            public string Text { get; set; } = "";
            static PitchPreviewLabel() => AffectsRender<PitchPreviewLabel>(ForegroundProperty);
            protected override Size MeasureOverride(Size availableSize) => new Size(80, 22);
            public override void Render(Avalonia.Media.DrawingContext context) {
                if (Foreground == null) return;
                using (context.PushClip(new Rect(Bounds.Size))) {
                    OpenUtau.App.Controls.PitchLabelLayout.Draw(context, Text ?? "", Foreground,
                        13, Bounds.Width - 4, 0, Bounds.Height);
                }
            }
        }

        static void ConfigurePitchPreview(ListBox preview) {
            preview.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<PreviewPitch>((pitch, _) => {
                if (pitch == null) return new TextBlock();
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("32,*,80"), ColumnSpacing = 8 };
                row.Children.Add(new TextBlock { Text = pitch.Degree.ToString(XenStrings.InputCulture) });
                var key = new Border { Padding = new Thickness(8, 2), CornerRadius = new CornerRadius(2) };
                string role = pitch.Role.ToString();
                key.Bind(Border.BackgroundProperty, new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(role + "KeyBrush"));
                var label = new PitchPreviewLabel { Text = pitch.Label, MinHeight = 22 };
                label.Bind(PitchPreviewLabel.ForegroundProperty, new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(role + "KeyNameBrush"));
                key.Child = label;
                Grid.SetColumn(key, 1);
                row.Children.Add(key);
                var cents = new TextBlock { Text = pitch.Cents, HorizontalAlignment = HorizontalAlignment.Right };
                Grid.SetColumn(cents, 2);
                row.Children.Add(cents);
                // Keep the three keyboard roles intact; scale membership uses a separate column.
                var result = new Grid { ColumnDefinitions = new ColumnDefinitions("20,*"), MinHeight = 26 };
                result.Children.Add(new TextBlock { Text = pitch.Selected ? "●" : "", VerticalAlignment = VerticalAlignment.Center });
                Grid.SetColumn(row, 1);
                result.Children.Add(row);
                return result;
            });
        }

        void RefreshPitchPreviews(ScaleDefinition? candidate = null) {
            var selected = candidate ?? (scales.SelectedIndex >= 0 && scales.SelectedIndex < draft.Scales.Count
                ? draft.Scales[scales.SelectedIndex] : null);
            var active = new HashSet<int>(selected?.Degrees ?? Enumerable.Range(0, draft.DegreeCount).ToList());
            string notation = PitchNotation.ResolvePrimary(draft, null);
            var rows = Enumerable.Range(0, draft.DegreeCount).Reverse().Select(degree => new PreviewPitch {
                Tuning = draft, Notation = notation, Degree = degree, Selected = active.Contains(degree),
            }).ToArray();
            if (candidate != null) {
                generatorPreview.ItemsSource = rows;
                return;
            }
            pitchPreview.ItemsSource = rows;
            scalePreview.ItemsSource = rows;
            FillDegreeMap(degreeMap, rows, false);
            FillDegreeMap(subsetMap, rows, true);
            tuningSummary.Text = $"{draft.DegreeCount} · {draft.Period.Ratio ?? draft.Period.ToCents().ToString("0.##", XenStrings.InputCulture) + " ¢"}";
            scaleSummary.Text = $"{draft.Name}    {active.Count} / {draft.DegreeCount}    {selected?.Name}";
        }

        static void FillDegreeMap(Grid map, PreviewPitch[] pitches, bool selection) {
            map.Children.Clear();
            map.ColumnDefinitions.Clear();
            map.RowDefinitions.Clear();
            // Large tunings retain the virtualised, exact degree list instead of tiny cells.
            map.IsVisible = pitches.Length <= 256;
            if (!map.IsVisible) return;
            int columns = Math.Min(16, pitches.Length);
            map.ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", columns)));
            map.RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("Auto", (pitches.Length + columns - 1) / columns)));
            foreach (var pitch in pitches) {
                string role = pitch.Role.ToString();
                var label = new TextBlock {
                    Text = pitch.Degree.ToString(XenStrings.InputCulture), FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                };
                label.Bind(TextBlock.ForegroundProperty, new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(role + "KeyNameBrush"));
                var cell = new Border {
                    Child = label, Height = 26, CornerRadius = new CornerRadius(3),
                    BorderThickness = new Thickness(selection && pitch.Selected ? 2 : 1),
                };
                cell.Bind(Border.BackgroundProperty, new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(role + "KeyBrush"));
                cell.Bind(Border.BorderBrushProperty, new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(selection && pitch.Selected ? "AccentBrush1" : "NeutralAccentBrushSemi"));
                ToolTip.SetTip(cell, $"{pitch.Label}  {pitch.Cents}");
                Grid.SetRow(cell, pitch.Degree / columns);
                Grid.SetColumn(cell, pitch.Degree % columns);
                map.Children.Add(cell);
            }
        }

        void RefreshGeneratorPreview() {
            if (loading) return;
            mode.Maximum = cardinality.Value ?? 1;
            try {
                var result = MosGenerator.Generate(draft, ReadInteger(generator, 0), ReadInteger(cardinality, 7), ReadInteger(mode, 1) - 1);
                scaleResult.Text = $"{result.Scale.Name}\n{result.Pattern}";
                ToolTip.SetTip(scaleResult, string.Join(", ", result.Scale.Degrees));
                // A candidate is previewed without replacing the chosen scale until generated.
                RefreshPitchPreviews(result.Scale);
            } catch (Exception error) {
                generatorPreview.ItemsSource = null;
                scaleResult.Text = XenStrings.Get("error.invalid");
                ToolTip.SetTip(scaleResult, error.Message);
                RefreshPitchPreviews();
            }
        }

        void LoadEntries() {
            var profile = draft.Notations.FirstOrDefault(n => n.Id == "custom");
            entries = Enumerable.Range(0, draft.DegreeCount).Select(i => new DegreeEntry {
                Degree = i,
                Interval = draft.Divisions > 0 ? draft.GetCents(i).ToString("R", XenStrings.InputCulture)
                    : draft.Degrees[i].Ratio ?? draft.Degrees[i].Cents?.ToString("R", XenStrings.InputCulture) ?? "0.0",
                Name = profile != null && profile.Names.TryGetValue(i, out var label) ? label : "",
                Aliases = profile != null && profile.Aliases.TryGetValue(i, out var aliases) ? string.Join(", ", aliases) : "",
                KeyClass = draft.Keyboard.Classes.TryGetValue(i, out var keyClass) ? keyClass : "",
            }).ToList();
            degrees.ItemsSource = entries;
        }
        void RebuildEntries() => Guard(() => {
            ReadStructure();
            LoadFields();
        });
        void ReadStructure() {
            var source = draft.Clone();
            source.Name = name.Text?.Trim() ?? "";
            source.Reference.Degree = ReadInteger(referenceDegree, 0);
            source.Reference.Frequency = (double)(frequency.Value ?? 440);
            if (source.Divisions == 0) source.Degrees = entries.Select(entry => ParseInterval(entry.Interval)).ToList();
            int nextDivisions = ReadInteger(divisions, 0);
            int nextCount = ReadInteger(explicitCount, 12);
            var nextPeriod = ParseInterval(period.Text ?? "2/1");
            bool reset = TuningEditorStructure.RequiresReset(source, nextDivisions, nextCount, nextPeriod);
            draft = TuningEditorStructure.Rebuild(source, nextDivisions, nextCount, nextPeriod);
            if (reset) LoadFields();
        }
        void ReadFields() {
            ReadStructure();
            draft.Keyboard.ActiveScaleId = scales.SelectedIndex >= 0 && scales.SelectedIndex < draft.Scales.Count
                ? draft.Scales[scales.SelectedIndex].Id : null;
            if (draft.Divisions == 0) draft.Degrees = entries.Select(e => ParseInterval(e.Interval)).ToList();
            var custom = new NotationProfile { Id = "custom", Name = XenStrings.Get("notation.custom") };
            foreach (var entry in entries) {
                if (!string.IsNullOrWhiteSpace(entry.Name)) custom.Names[entry.Degree] = entry.Name;
                var aliases = entry.Aliases.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (aliases.Length > 0) custom.Aliases[entry.Degree] = aliases.ToList();
                if (string.IsNullOrWhiteSpace(entry.KeyClass)) draft.Keyboard.Classes.Remove(entry.Degree);
                else draft.Keyboard.Classes[entry.Degree] = entry.KeyClass.Trim();
            }
            draft.Notations.RemoveAll(n => n.Id == "custom");
            if (custom.Names.Count > 0 || custom.Aliases.Count > 0) draft.Notations.Add(custom);
            draft.Validate();
        }
        void ReadYaml() => Guard(() => { draft = TunxenFormat.Parse(yaml.Text ?? ""); LoadFields(); status.Text = XenStrings.Get("status.definitionLoaded"); });
        void GenerateScale() => Guard(() => {
            ReadFields();
            var result = MosGenerator.Generate(draft, ReadInteger(generator, 0), ReadInteger(cardinality, 7), ReadInteger(mode, 1) - 1);
            draft.Scales.RemoveAll(s => s.Id == result.Scale.Id);
            draft.Scales.Add(result.Scale);
            draft.Keyboard.ActiveScaleId = result.Scale.Id;
            scaleResult.Text = $"{result.Scale.Name}\n{string.Join(", ", result.Scale.Degrees)}\n{result.Pattern}";
            LoadFields();
        });
        void GenerateIntervalTuning() => Guard(() => {
            ReadFields();
            draft = MosGenerator.CreateTuning(draft.Period, ParseInterval(ratioGenerator.Text ?? "3/2"), ReadInteger(intervalCardinality, 7));
            LoadFields();
        });
        void AddDegreeSubset() => Guard(() => {
            ReadFields();
            var selected = (customScaleDegrees.Text ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(value => int.Parse(value, CultureInfo.InvariantCulture))
                .Select(value => (value % draft.DegreeCount + draft.DegreeCount) % draft.DegreeCount).Distinct().OrderBy(value => value).ToList();
            if (selected.Count == 0) throw new FormatException(XenStrings.Get("error.degreeRequired"));
            var scale = new ScaleDefinition {
                Id = "custom-" + Guid.NewGuid().ToString("N"),
                Name = string.IsNullOrWhiteSpace(customScaleName.Text) ? XenStrings.Get("scale.custom") : customScaleName.Text.Trim(),
                Degrees = selected,
            };
            draft.Scales.Add(scale);
            draft.Keyboard.ActiveScaleId = scale.Id;
            LoadFields();
            scaleResult.Text = string.Join(", ", selected);
        });
        void Preview() => Guard(() => { ReadFields(); status.Text = DescribeChanges(draft); });
        string DescribeChanges(TuningDefinition target) {
            var project = Project;
            var track = scope.SelectedIndex == 1 ? null : CurrentTrack;
            var parts = project.parts.OfType<UVoicePart>().Where(p => track == null ? project.tracks[p.trackNo].pitchSystem == null : p.trackNo == track.TrackNo);
            int count = 0;
            double max = 0;
            foreach (var part in parts) foreach (var note in part.notes) {
                var address = PitchResolver.NearestAddress(target, note.ResolvedTone, preserve.IsChecked == true);
                max = Math.Max(max, Math.Abs(PitchResolver.ResolveMidi(target, address) - note.ResolvedTone) * 100);
                count++;
            }
            return XenStrings.Get("preview.changes", count, max);
        }
        void Apply(bool referenceOnly, bool displayOnly) => Guard(() => {
            ReadFields();
            if (displayOnly && !MatchesAppliedTuning()) {
                RefreshApplyAction();
                status.Text = XenStrings.Get("action.applyTuning");
                return;
            }
            var track = scope.SelectedIndex == 1 ? null : CurrentTrack;
            string preview = DescribeChanges(draft);
            var target = draft.Clone();
            if (referenceOnly || displayOnly) {
                target = Project.GetTuning(track).Clone();
                if (referenceOnly) target.Reference = draft.Reference.Clone();
                if (displayOnly) {
                    target.Keyboard = draft.Keyboard.Clone();
                    target.Scales = draft.Scales.Select(s => s.Clone()).ToList();
                    target.Notations = draft.Notations.Select(n => n.Clone()).ToList();
                }
            }
            DocManager.Inst.StartUndoGroup("xen.action.applyTuning");
            try {
                DocManager.Inst.ExecuteCmd(new SetTuningCommand(Project, track, target, preserve.IsChecked == true, referenceOnly, displayOnly));
            } finally { DocManager.Inst.EndUndoGroup(); }
            notes?.RefreshPitchRows();
            RefreshPitchPreviews();
            RefreshApplyAction();
            status.Text = displayOnly ? XenStrings.Get("status.displayApplied") : referenceOnly ? XenStrings.Get("status.referenceChanged") : preview;
        });
        void InheritProjectTuning() => Guard(() => {
            if (scope.SelectedIndex != 0 || CurrentTrack == null) return;
            var track = CurrentTrack;
            DocManager.Inst.StartUndoGroup("xen.action.inherit");
            try { DocManager.Inst.ExecuteCmd(new SetTuningCommand(Project, track, null, preserve.IsChecked == true)); }
            finally { DocManager.Inst.EndUndoGroup(); }
            draft = EffectiveTuning.Clone();
            notes?.RefreshPitchRows();
            LoadFields();
            status.Text = XenStrings.Get("status.inherited");
        });

        async void ImportTuning() {
            var path = await Open(XenStrings.Get("import.tuning"), "*.tunxen");
            if (path != null) Guard(() => { draft = TunxenFormat.Load(path); LoadFields(); });
        }
        async void ExportTuning() {
            var path = await Save(XenStrings.Get("export.tuning"), "tunxen");
            if (path != null) Guard(() => { ReadFields(); TunxenFormat.Save(path, draft); status.Text = XenStrings.Get("status.tuningExported"); });
        }
        async void ImportScala() {
            var path = await Open(XenStrings.Get("import.scala"), "*.scl");
            if (path != null) Guard(() => { draft = ScalaScl.Load(path); LoadFields(); });
        }
        async void ExportScala() {
            var path = await Save(XenStrings.Get("export.scala"), "scl");
            if (path != null) Guard(() => { ReadFields(); ScalaScl.Save(path, draft); status.Text = XenStrings.Get("status.scalaExported"); });
        }
        async void ImportKbm() {
            var path = await Open(XenStrings.Get("import.kbm"), "*.kbm");
            if (path != null) Guard(() => { ReadFields(); ScalaKbm.ApplyTo(draft, ScalaKbm.Load(path)); LoadFields(); });
        }
        async void ExportKbm() {
            var path = await Save(XenStrings.Get("export.kbm"), "kbm");
            if (path != null) Guard(() => { ReadFields(); ScalaKbm.Save(path, draft); status.Text = XenStrings.Get("status.kbmExported"); });
        }
        async System.Threading.Tasks.Task<string?> Open(string title, string pattern) {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
                Title = title, AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType(title) { Patterns = new[] { pattern } } },
            });
            return files.FirstOrDefault()?.TryGetLocalPath();
        }
        async System.Threading.Tasks.Task<string?> Save(string title, string extension) {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
                Title = title, DefaultExtension = extension, SuggestedFileName = draft.Id + "." + extension,
                ShowOverwritePrompt = true,
                FileTypeChoices = new[] { new FilePickerFileType(extension) { Patterns = new[] { "*." + extension } } },
            });
            return file?.TryGetLocalPath();
        }
        void Guard(Action action) { try { action(); } catch (Exception error) {
            status.Text = XenStrings.Get("error.invalid");
            ToolTip.SetTip(status, error.Message);
        } }
        static PitchInterval ParseInterval(string value) => value.Contains('/')
            ? PitchInterval.FromRatio(value.Trim()) : PitchInterval.FromCents(XenNumberInput.Parse(value, XenStrings.InputCulture));
        static int ReadInteger(NumericUpDown control, int fallback) {
            decimal value = control.Value ?? fallback;
            if (value != decimal.Truncate(value)) throw new FormatException("Enter a whole number.");
            return checked((int)value);
        }
        static NumericUpDown Number(decimal min, decimal max, decimal value, int decimals) => new NumericUpDown {
            Minimum = min, Maximum = max, Value = value, Increment = decimals == 0 ? 1 : 0.1m,
            FormatString = decimals == 0 ? "0" : "0." + new string('#', decimals),
            NumberFormat = XenStrings.InputCulture.NumberFormat,
            ParsingNumberStyle = decimals == 0 ? NumberStyles.Integer : NumberStyles.Float,
        };
        static Button Button(string text, Action action) {
            var button = new Button { Content = text, Margin = new Thickness(0, 0, 8, 6) };
            button.Click += (_, _) => action();
            return button;
        }
        static Grid Field(string label, Control control) {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 12 };
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 240 });
            Grid.SetColumn(control, 1); row.Children.Add(control); return row;
        }
        public sealed class DegreeEntry {
            public int Degree { get; set; }
            public string Interval { get; set; } = "0.0";
            public string Name { get; set; } = "";
            public string Aliases { get; set; } = "";
            public string KeyClass { get; set; } = "";
        }
    }
}
