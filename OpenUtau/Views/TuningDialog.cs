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
using OpenUtau.App.Controls;
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
        readonly NotchedKeyboardPreview pitchPreview = new NotchedKeyboardPreview { Name = "TuningPitchPreview" };
        readonly RadioButton mosScale = new RadioButton { Content = XenStrings.Get("tab.generator"), GroupName = "ScaleKind", Name = "ScaleKindMos" };
        readonly RadioButton customScale = new RadioButton { Content = XenStrings.Get("scale.custom"), GroupName = "ScaleKind", Name = "ScaleKindCustom" };
        readonly StackPanel mosPanel = new StackPanel { Spacing = 10 };
        readonly StackPanel customPanel = new StackPanel { Spacing = 10 };
        readonly WrapPanel manualKeys = new WrapPanel { Name = "ManualKeyClasses" };
        bool loading;
        bool structureValid = true;
        bool mosAvailable = true;
        bool mosEditing;
        bool customGenerator;
        bool customCardinality;
        string? sessionScaleId;
        NotationProfile? seededNames;
        string? namesBeforeManual;
        HashSet<string> originalScaleIds = new HashSet<string>();
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
            divisions.Name = "TuningDivisions";
            explicitCount.Name = "TuningExplicitCount";
            period.Name = "TuningPeriod";
            referenceDegree.Name = "TuningReferenceDegree";
            frequency.Name = "TuningReferenceFrequency";
            scales.Name = "TuningScale";
            scaleResult.Name = "MosResult";
            presets.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<TuningDefinition>(
                (tuning, _) => new TextBlock { Text = tuning == null ? "" : XenStrings.PresetName(tuning) });
            Title = XenStrings.Get("tuning.title");
            Width = 960;
            Height = 760;
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
            applyDraft = Button(XenStrings.Get("action.applyTuning"), ApplyDraft);
            applyDraft.Name = "ApplyTuningDraft";
            commands.Children.Add(applyDraft);
            commands.Children.Add(Button(XenStrings.Get("action.close"), Close));
            footer.Children.Add(commands);
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            var tuningPage = Page("tab.tuning");
            tuningPage.Children.Add(Field(XenStrings.Get("field.preset"), presets));
            tuningPage.Children.Add(Parameters(("field.period", period), ("field.divisions", divisions),
                ("field.referenceDegree", referenceDegree), ("field.frequency", frequency)));
            tuningPage.Children.Add(Field(XenStrings.Get("field.explicitCount"), explicitCount));
            // One scale card: a MOS decides the keyboard, or a custom scale whose keys are set one by one.
            var scaleCard = new StackPanel { Spacing = 10 };
            scaleCard.Children.Add(new TextBlock { Text = XenStrings.Get("field.scale"), FontSize = 16,
                FontWeight = Avalonia.Media.FontWeight.SemiBold });
            var kinds = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            kinds.Children.Add(mosScale);
            kinds.Children.Add(customScale);
            scaleCard.Children.Add(kinds);
            mosPanel.Children.Add(Parameters(("field.generatorDegree", generator), ("field.cardinality", cardinality), ("field.mode", mode)));
            mosPanel.Children.Add(scaleResult);
            scaleCard.Children.Add(mosPanel);
            customPanel.Children.Add(Field(XenStrings.Get("field.scale"), scales));
            var addScale = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,Auto"), ColumnSpacing = 12 };
            addScale.Children.Add(Labelled("field.scaleName", customScaleName));
            var degreeList = Labelled("field.degrees", customScaleDegrees);
            Grid.SetColumn(degreeList, 1); addScale.Children.Add(degreeList);
            var add = Button(XenStrings.Get("action.addSubset"), AddDegreeSubset);
            add.VerticalAlignment = VerticalAlignment.Bottom;
            Grid.SetColumn(add, 2); addScale.Children.Add(add);
            customPanel.Children.Add(addScale);
            manualKeys.Children.Add(new TextBlock { Text = XenStrings.Get("field.selectedKey"), VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 6) });
            foreach (var (label, keyClass) in new[] { ("keyClass.white", "natural"), ("keyClass.black", "accidental"), ("keyClass.between", "between") }) {
                var button = Button(XenStrings.Get(label), () => SetSelectedKeyClass(keyClass));
                button.Name = "KeyClass-" + keyClass;
                manualKeys.Children.Add(button);
            }
            customPanel.Children.Add(manualKeys);
            scaleCard.Children.Add(customPanel);
            scaleCard.Children.Add(KeyboardViewport(pitchPreview));
            tuningPage.Children.Add(Card(scaleCard));

            degrees.Columns.Add(new DataGridTextColumn { Header = XenStrings.Get("field.degree"), Binding = new Binding(nameof(DegreeEntry.Degree)), IsReadOnly = true });
            degrees.Columns.Add(new DataGridTextColumn { Header = XenStrings.Get("field.interval"), Binding = new Binding(nameof(DegreeEntry.Interval)) });
            degrees.Columns.Add(new DataGridTextColumn { Header = XenStrings.Get("field.name"), Binding = new Binding(nameof(DegreeEntry.Name)) });
            degrees.Columns.Add(new DataGridTextColumn { Header = XenStrings.Get("field.aliases"), Binding = new Binding(nameof(DegreeEntry.Aliases)) });
            degrees.Columns.Add(new DataGridTextColumn { Header = XenStrings.Get("field.keyClass"), Binding = new Binding(nameof(DegreeEntry.KeyClass)) });
            // Give the table a bounded viewport so its own scrolling and virtualisation work.
            degrees.Height = 320;
            var keyboardPage = Page("tab.names");
            keyboardPage.Children.Add(degrees);

            var conversionPage = Page("tab.conversion");
            conversionPage.Children.Add(preserve);
            conversionPage.Children.Add(Button(XenStrings.Get("action.preview"), Preview));
            conversionPage.Children.Add(Button(XenStrings.Get("action.inherit"), InheritProjectTuning));

            var advanced = Page("tab.definition");
            advanced.Children.Add(Field(XenStrings.Get("field.name"), name));
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

            var pages = new[] { tuningPage, keyboardPage, conversionPage, advanced }
                .Select(page => new ScrollViewer {
                    Content = page, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                }).ToArray();
            var navigation = new ListBox {
                Name = "TuningNavigation", ItemsSource = new[] {
                    XenStrings.Get("tab.tuning"),
                    XenStrings.Get("tab.names"), XenStrings.Get("tab.conversion"), XenStrings.Get("tab.definition"),
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
            generator.ValueChanged += (_, _) => { if (!loading) { customGenerator = true; LiveMosChanged(); } };
            cardinality.ValueChanged += (_, _) => { if (!loading) { customCardinality = true; LiveMosChanged(); } };
            mode.ValueChanged += (_, _) => { if (!loading) LiveMosChanged(); };
            mosScale.IsCheckedChanged += (_, _) => { if (!loading && mosScale.IsChecked == true) SetMosKeyboard(true); };
            customScale.IsCheckedChanged += (_, _) => { if (!loading && customScale.IsChecked == true) SetMosKeyboard(false); };
            divisions.ValueChanged += (_, _) => { if (!loading) LiveStructureChanged(); };
            explicitCount.ValueChanged += (_, _) => { if (!loading) LiveStructureChanged(); };
            period.PropertyChanged += (_, e) => { if (!loading && e.Property == TextBox.TextProperty) LiveStructureChanged(); };
            referenceDegree.ValueChanged += (_, _) => { if (!loading) LiveReferenceChanged(); };
            frequency.ValueChanged += (_, _) => { if (!loading) LiveReferenceChanged(); };
            scales.SelectionChanged += (_, _) => { if (!loading) SelectScale(); };
            presets.SelectionChanged += (_, _) => {
                if (!loading && presets.SelectedItem is TuningDefinition selected) {
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
            try {
                name.Text = draft.Name;
                divisions.Value = draft.Divisions;
                explicitCount.Value = draft.DegreeCount;
                period.Text = draft.Period.Ratio ?? draft.Period.Cents?.ToString("R", XenStrings.InputCulture);
                referenceDegree.Value = draft.Reference.Degree;
                frequency.Value = (decimal)draft.Reference.Frequency;
                // Unequal tunings saved before the MOS switch start off, keeping the keys their names imply.
                if (draft.Divisions == 0 && !Manual && KeyboardRegionizer.LetterScale(draft) == null
                    && ActiveScale()?.GeneratorDegree == null) draft.Keyboard.Classes = KeyboardRegionizer.ClassesFromRoles(draft);
                originalScaleIds = new HashSet<string>(draft.Scales.Select(scale => scale.Id));
                sessionScaleId = null;
                seededNames = null;
                RefreshScaleChoices();
                LoadEntries();
                yaml.Text = TunxenFormat.Serialize(draft);
                if (explicitCount.Parent is Control row) row.IsVisible = draft.Divisions == 0;
                var active = ActiveScale();
                var theory = KeyboardTheory.For(draft);
                int defaultCount = Math.Min(theory?.ReferenceDivision == 13 ? 9 : 7, draft.DegreeCount);
                generator.Value = active?.GeneratorDegree ?? KeyboardRegionizer.DefaultGenerator(draft);
                cardinality.Maximum = draft.DegreeCount;
                cardinality.Value = active?.GeneratorDegree != null ? active.Degrees.Count : defaultCount;
                mode.Maximum = cardinality.Value ?? 1;
                mode.Value = (active?.Mode ?? 0) + 1;
                mosEditing = active?.GeneratorDegree != null;
                customGenerator = mosEditing && active!.GeneratorDegree != KeyboardRegionizer.DefaultGenerator(draft);
                customCardinality = mosEditing && active!.Degrees.Count != defaultCount;
                structureValid = true;
                RefreshPresetChoice();
            } finally { loading = false; }
            RefreshMosMode();
            RefreshLiveMos(false);
        }

        bool Manual => KeyboardRegionizer.IsManual(draft);

        void RefreshMosMode() {
            bool wasLoading = loading;
            loading = true;
            try {
                bool manual = Manual;
                mosScale.IsChecked = !manual;
                customScale.IsChecked = manual;
                mosPanel.IsVisible = !manual;
                customPanel.IsVisible = manual;
            } finally { loading = wasLoading; }
        }

        /// <summary>MOS on derives every key from the MOS; off starts from the current keys and edits them one by one.</summary>
        void SetMosKeyboard(bool on) {
            if (!structureValid) { RefreshMosMode(); return; }
            if (on) {
                draft.Keyboard.Classes.Clear();
                RemoveSeededNames();
            } else {
                SeedManualNames();
                var cells = pitchPreview.Cells;
                draft.Keyboard.Classes = Enumerable.Range(0, draft.DegreeCount).ToDictionary(degree => degree,
                    degree => degree >= cells.Count || pitchPreview.IsWhiteKeysOnly || cells[degree].Role == KeyRole.White ? "natural"
                        : cells[degree].Role == KeyRole.Black ? "accidental" : "between");
            }
            SyncEntryClasses();
            RefreshMosMode();
            if (on) LiveMosChanged();
            else RefreshLiveMos(false);
        }

        /// <summary>Copies the names shown with MOS on into custom names, so switching off keeps them editable key by key.</summary>
        void SeedManualNames() {
            seededNames = null;
            string primary = PitchNotation.ResolvePrimary(draft, null);
            if (pitchPreview.IsWhiteKeysOnly || primary == "degree" || primary == "custom") return;
            var source = PitchNotation.FindProfile(draft, primary);
            if (source == null) return;
            var names = source.Clone();
            names.Id = "custom";
            names.Name = XenStrings.Get("notation.custom");
            draft.Notations.RemoveAll(profile => profile.Id == "custom");
            draft.Notations.Add(names);
            namesBeforeManual = draft.Keyboard.PrimaryNotation;
            draft.Keyboard.PrimaryNotation = "custom";
            seededNames = names.Clone();
            SyncEntryNames();
        }

        /// <summary>Switching MOS back on drops seeded names the user has not edited.</summary>
        void RemoveSeededNames() {
            if (seededNames == null) return;
            var current = draft.Notations.FirstOrDefault(profile => profile.Id == "custom");
            bool edited = entries.Any(entry => entry.Name != (seededNames.Names.TryGetValue(entry.Degree, out var name) ? name : ""));
            if (current != null && !edited) {
                draft.Notations.Remove(current);
                draft.Keyboard.PrimaryNotation = namesBeforeManual ?? "degree";
                SyncEntryNames();
            }
            seededNames = null;
        }

        void SyncEntryNames() {
            var profile = draft.Notations.FirstOrDefault(n => n.Id == "custom");
            foreach (var entry in entries) {
                entry.Name = profile != null && profile.Names.TryGetValue(entry.Degree, out var label) ? label : "";
                entry.Aliases = profile != null && profile.Aliases.TryGetValue(entry.Degree, out var aliases) ? string.Join(", ", aliases) : "";
            }
        }

        void SetSelectedKeyClass(string keyClass) {
            if (!Manual || pitchPreview.SelectedDegree is not int degree) return;
            draft.Keyboard.Classes[degree] = keyClass;
            SyncEntryClasses();
            RefreshPitchPreview();
            RefreshApplyAction();
        }

        void SyncEntryClasses() {
            foreach (var entry in entries) {
                entry.KeyClass = draft.Keyboard.Classes.TryGetValue(entry.Degree, out var keyClass) ? keyClass : "";
            }
            degrees.ItemsSource = null;
            degrees.ItemsSource = entries;
        }

        ScaleDefinition? ActiveScale() => draft.Scales.FirstOrDefault(scale => scale.Id == draft.Keyboard.ActiveScaleId);

        void RefreshPresetChoice() {
            presets.SelectedItem = presets.ItemsSource!.Cast<TuningDefinition>().FirstOrDefault(preset => draft.Divisions > 0
                ? preset.Divisions == draft.Divisions && Math.Abs(preset.PeriodCents - draft.PeriodCents) < 1e-8
                : preset.Id == draft.Id);
        }

        void RefreshScaleChoices() {
            bool wasLoading = loading;
            loading = true;
            try {
                scales.ItemsSource = draft.Scales.Select(scale => scale.Id == "all" ? XenStrings.Get("scale.all")
                    : scale.Id == "rast" ? XenStrings.Get("preset.rast") : scale.Name).ToArray();
                scales.SelectedIndex = Math.Max(0, draft.Scales.FindIndex(scale => scale.Id == draft.Keyboard.ActiveScaleId));
            } finally { loading = wasLoading; }
        }

        void SelectScale() {
            if (scales.SelectedIndex < 0 || scales.SelectedIndex >= draft.Scales.Count) return;
            var selected = draft.Scales[scales.SelectedIndex];
            draft.Keyboard.ActiveScaleId = selected.Id;
            mosEditing = selected.GeneratorDegree.HasValue;
            if (mosEditing) {
                loading = true;
                try {
                    generator.Value = selected.GeneratorDegree;
                    cardinality.Value = selected.Degrees.Count;
                    mode.Maximum = cardinality.Value ?? 1;
                    mode.Value = selected.Mode + 1;
                    var theory = KeyboardTheory.For(draft);
                    customGenerator = selected.GeneratorDegree != KeyboardRegionizer.DefaultGenerator(draft);
                    customCardinality = selected.Degrees.Count != Math.Min(theory?.ReferenceDivision == 13 ? 9 : 7, draft.DegreeCount);
                } finally { loading = false; }
                RefreshLiveMos(false);
            } else {
                mosAvailable = true;
                scaleResult.Text = "";
                RefreshPitchPreview();
                RefreshApplyAction();
            }
        }

        void LiveStructureChanged() {
            loading = true;
            try {
                ReadStructure();
                name.Text = draft.Name;
                RefreshPresetChoice();
                if (draft.Divisions > 0) explicitCount.Value = draft.DegreeCount;
                if (explicitCount.Parent is Control row) row.IsVisible = draft.Divisions == 0;
                referenceDegree.Value = draft.Reference.Degree;
                var theory = KeyboardTheory.For(draft);
                if (!customGenerator) generator.Value = KeyboardRegionizer.DefaultGenerator(draft);
                cardinality.Maximum = draft.DegreeCount;
                if (!customCardinality) cardinality.Value = Math.Min(theory?.ReferenceDivision == 13 ? 9 : 7, draft.DegreeCount);
                mode.Maximum = cardinality.Value ?? 1;
                structureValid = true;
                status.Text = "";
            } catch (Exception error) {
                structureValid = false;
                pitchPreview.SetTuning(null);
                status.Text = XenStrings.Get("error.invalid");
                ToolTip.SetTip(status, error.Message);
            } finally { loading = false; }
            RefreshMosMode();
            if (structureValid) LiveMosChanged();
            else RefreshApplyAction();
        }

        void LiveReferenceChanged() => Guard(() => {
            draft.Reference.Degree = ReadInteger(referenceDegree, draft.Reference.Degree);
            draft.Reference.Frequency = (double)(frequency.Value ?? (decimal)draft.Reference.Frequency);
            draft.Validate();
            RefreshPitchPreview();
            RefreshApplyAction();
        });

        void LiveMosChanged() {
            mosEditing = true;
            RefreshLiveMos(true);
        }

        void RemoveSessionScale() {
            if (sessionScaleId != null && !originalScaleIds.Contains(sessionScaleId)) draft.Scales.RemoveAll(scale => scale.Id == sessionScaleId);
            sessionScaleId = null;
        }

        void UseAllDegrees() {
            var all = draft.Scales.FirstOrDefault(scale => scale.Id == "all");
            if (all == null) {
                all = new ScaleDefinition { Id = "all", Name = XenStrings.Get("scale.all") };
                draft.Scales.Insert(0, all);
            }
            all.Degrees = Enumerable.Range(0, draft.DegreeCount).ToList();
            draft.Keyboard.ActiveScaleId = all.Id;
        }

        void RefreshLiveMos(bool activate) {
            if (loading || !structureValid) return;
            if (Manual) {
                mosAvailable = true;
                scaleResult.Text = "";
                RefreshPitchPreview();
                RefreshApplyAction();
                return;
            }
            loading = true;
            try {
                mode.Maximum = cardinality.Value ?? 1;
                var result = MosGenerator.Generate(draft, ReadInteger(generator, 0), ReadInteger(cardinality, 7), ReadInteger(mode, 1) - 1);
                if (!result.IsMos && !result.IsEqualStep) throw new ArgumentException("The generator cardinality is not a MOS.");
                mosAvailable = true;
                scaleResult.Text = activate || mosEditing ? result.Pattern.Length <= 80 ? result.Pattern : result.Pattern[..80] + "…" : "";
                ToolTip.SetTip(scaleResult, null);
                if (activate) {
                    RemoveSessionScale();
                    var existing = draft.Scales.FirstOrDefault(scale => scale.Id == result.Scale.Id);
                    if (existing != null) result.Scale.Name = existing.Name;
                    draft.Scales.RemoveAll(scale => scale.Id == result.Scale.Id);
                    draft.Scales.Add(result.Scale);
                    sessionScaleId = result.Scale.Id;
                    draft.Keyboard.ActiveScaleId = result.Scale.Id;
                    RefreshScaleChoices();
                }
            } catch (Exception) {
                mosAvailable = false;
                scaleResult.Text = XenStrings.Get("status.mosUnavailable");
                ToolTip.SetTip(scaleResult, null);
                if (activate || mosEditing) {
                    RemoveSessionScale();
                    UseAllDegrees();
                    RefreshScaleChoices();
                }
            } finally { loading = false; }
            RefreshPitchPreview();
            RefreshApplyAction();
        }

        void RefreshPitchPreview() {
            if (!structureValid) return;
            pitchPreview.SetTuning(draft, ActiveScale()?.Degrees, whiteKeysOnly: !mosAvailable && !Manual);
        }

        bool MatchesAppliedTuning() {
            var applied = Project.GetTuning(scope.SelectedIndex == 1 ? null : CurrentTrack);
            return applied.DegreeCount == draft.DegreeCount && Math.Abs(applied.PeriodCents - draft.PeriodCents) < 1e-8
                && Enumerable.Range(0, draft.DegreeCount).All(d => Math.Abs(applied.GetCents(d) - draft.GetCents(d)) < 1e-8);
        }

        bool ReferenceChanged() {
            var reference = Project.GetTuning(scope.SelectedIndex == 1 ? null : CurrentTrack).Reference;
            return reference.Degree != draft.Reference.Degree || Math.Abs(reference.Frequency - draft.Reference.Frequency) > 1e-9;
        }

        bool UsesDisplayApply() => (selectedPage == 0 || selectedPage == 1) && MatchesAppliedTuning() && !ReferenceChanged();

        void RefreshApplyAction() {
            applyDraft.IsEnabled = structureValid;
            applyDraft.Content = XenStrings.Get(UsesDisplayApply() ? "action.applyDisplay"
                : MatchesAppliedTuning() && ReferenceChanged() ? "action.changeReference" : "action.applyTuning");
        }

        static StackPanel Page(string heading) {
            var page = new StackPanel { Spacing = 12, Margin = new Thickness(0, 0, 12, 12) };
            page.Children.Add(new TextBlock { Text = XenStrings.Get(heading), FontSize = 20, FontWeight = Avalonia.Media.FontWeight.SemiBold });
            return page;
        }

        static Border Card(Control child) {
            var card = new Border { Child = child, Padding = new Thickness(12), CornerRadius = new CornerRadius(6) };
            card.Bind(Border.BackgroundProperty, new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("WorkspaceCardBrush"));
            return card;
        }

        static Grid Parameters(params (string Label, Control Control)[] fields) {
            int columns = fields.Length;
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", fields.Select(_ => "*"))),
                RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 12, RowSpacing = 6 };
            var labels = fields.Select(field => new TextBlock {
                Text = XenStrings.Get(field.Label), TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinHeight = 32,
            }).ToArray();
            for (int i = 0; i < fields.Length; i++) {
                Grid.SetColumn(labels[i], i); grid.Children.Add(labels[i]);
                Grid.SetColumn(fields[i].Control, i); Grid.SetRow(fields[i].Control, 1); grid.Children.Add(fields[i].Control);
            }
            if (fields.Length == 4) grid.SizeChanged += (_, _) => {
                int count = grid.Bounds.Width < 600 ? 2 : 4;
                if (count == columns) return;
                columns = count;
                grid.ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", count)));
                grid.RowDefinitions = new RowDefinitions(count == 2 ? "Auto,Auto,Auto,Auto" : "Auto,Auto");
                for (int i = 0; i < fields.Length; i++) {
                    Grid.SetColumn(labels[i], i % count); Grid.SetRow(labels[i], i / count * 2);
                    Grid.SetColumn(fields[i].Control, i % count); Grid.SetRow(fields[i].Control, i / count * 2 + 1);
                }
            };
            return grid;
        }

        static ScrollViewer KeyboardViewport(NotchedKeyboardPreview preview) => new ScrollViewer {
            Content = preview, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Height = 218,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };

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
            if (reset) LoadEntries();
        }
        void ReadFields() {
            ReadStructure();
            draft.Keyboard.ActiveScaleId = scales.SelectedIndex >= 0 && scales.SelectedIndex < draft.Scales.Count
                ? draft.Scales[scales.SelectedIndex].Id : null;
            if (draft.Divisions == 0) draft.Degrees = entries.Select(e => ParseInterval(e.Interval)).ToList();
            var custom = new NotationProfile { Id = "custom", Name = XenStrings.Get("notation.custom") };
            var previous = draft.Notations.FirstOrDefault(n => n.Id == "custom");
            foreach (var entry in entries) {
                if (!string.IsNullOrWhiteSpace(entry.Name)) {
                    custom.Names[entry.Degree] = entry.Name;
                    // Unchanged names keep their accidental glyph and octave offset.
                    if (previous != null && previous.Names.TryGetValue(entry.Degree, out var before) && before == entry.Name) {
                        if (previous.AccidentalIds.TryGetValue(entry.Degree, out var glyph)) custom.AccidentalIds[entry.Degree] = glyph;
                        if (previous.PeriodOffsets.TryGetValue(entry.Degree, out int offset)) custom.PeriodOffsets[entry.Degree] = offset;
                    }
                }
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
            var track = scope.SelectedIndex == 1 ? null : CurrentTrack;
            var command = new SetTuningCommand(Project, track, target, preserve.IsChecked == true,
                referenceOnly: MatchesAppliedTuning() && ReferenceChanged(), displayOnly: UsesDisplayApply());
            double max = command.Preview.Select(change => Math.Abs(change.After - change.Before) * 100).DefaultIfEmpty().Max();
            return XenStrings.Get("preview.changes", command.Preview.Count, max);
        }
        void ApplyDraft() => Guard(() => {
            if (!structureValid) return;
            ReadFields();
            var track = scope.SelectedIndex == 1 ? null : CurrentTrack;
            bool referenceOnly = MatchesAppliedTuning() && ReferenceChanged();
            bool displayOnly = UsesDisplayApply();
            string preview = DescribeChanges(draft);
            var target = draft.Clone();
            DocManager.Inst.StartUndoGroup("xen.action.applyTuning");
            try {
                if (referenceOnly) {
                    // First retain addresses while moving the reference, then apply all detached metadata.
                    DocManager.Inst.ExecuteCmd(new SetTuningCommand(Project, track, target, referenceOnly: true));
                }
                DocManager.Inst.ExecuteCmd(new SetTuningCommand(Project, track, target, preserve.IsChecked == true,
                    displayOnly: displayOnly));
            } finally { DocManager.Inst.EndUndoGroup(); }
            notes?.RefreshPitchRows();
            RefreshPitchPreview();
            RefreshApplyAction();
            status.Text = displayOnly ? XenStrings.Get("status.displayApplied")
                : referenceOnly ? XenStrings.Get("status.referenceChanged") : preview;
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
        static StackPanel Labelled(string label, Control control) {
            var panel = new StackPanel { Spacing = 6 };
            panel.Children.Add(new TextBlock { Text = XenStrings.Get(label), TextWrapping = Avalonia.Media.TextWrapping.Wrap });
            panel.Children.Add(control);
            return panel;
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
