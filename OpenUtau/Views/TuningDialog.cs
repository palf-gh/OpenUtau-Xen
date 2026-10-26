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
        readonly NumericUpDown periods = Number(1, 65536, 1, 0);
        readonly ComboBox mosChoices = new ComboBox {
            Name = "MosChoices", HorizontalAlignment = HorizontalAlignment.Stretch, PlaceholderText = XenStrings.Get("mos.custom"),
        };
        IReadOnlyList<MosChoice> mosChoiceList = Array.Empty<MosChoice>();
        /// <summary>The choice index of each list row; -1 for a note-count heading.</summary>
        List<int> mosChoiceRows = new List<int>();
        string? mosChoiceKey;
        readonly NumericUpDown intervalCardinality = Number(1, 65536, 7, 0);
        readonly TextBox ratioGenerator = new TextBox { Text = "3/2" };
        readonly TextBox customScaleName = new TextBox { Text = XenStrings.Get("scale.custom") };
        readonly TextBlock scaleResult = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        readonly NotchedKeyboardPreview pitchPreview = new NotchedKeyboardPreview { Name = "TuningPitchPreview" };
        readonly RadioButton mosKeyboard = new RadioButton { Content = XenStrings.Get("tab.generator"), GroupName = "KeyboardKind", Name = "KeyboardKindMos" };
        readonly RadioButton manualKeyboard = new RadioButton { Content = XenStrings.Get("keyboard.manual"), GroupName = "KeyboardKind", Name = "KeyboardKindManual" };
        readonly StackPanel mosPanel = new StackPanel { Spacing = 10 };
        readonly WrapPanel manualKeys = new WrapPanel { Name = "ManualKeyClasses" };
        readonly NumericUpDown keyboardCycle = Number(1, 65536, 9, 0);
        readonly ComboBox tonic = new ComboBox {
            Name = "ScaleTonic",
            ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((label, _) => new PitchText { Pitch = label }),
        };
        readonly CheckBox editScale = new CheckBox { Content = XenStrings.Get("action.editScale"), Name = "EditScaleKeys" };
        List<ScaleDefinition> scaleChoices = new List<ScaleDefinition>();
        bool loading;
        bool structureValid = true;
        bool customGenerator;
        bool customCardinality;
        bool customPeriods;
        string? editingScaleId;
        NotationProfile? seededNames;
        string? namesBeforeManual;
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
            periods.Name = "MosPeriods";
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
            keyboardCycle.Name = "KeyboardCycle";
            tuningPage.Children.Add(Field(XenStrings.Get("field.keyboardCycle"), keyboardCycle));
            // The keyboard (white/black keys and names) and the scale (tonic and notes) are set separately.
            var keyboardCard = new StackPanel { Spacing = 10 };
            keyboardCard.Children.Add(CardHeading("field.keyboard"));
            var kinds = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            kinds.Children.Add(mosKeyboard);
            kinds.Children.Add(manualKeyboard);
            keyboardCard.Children.Add(kinds);
            mosPanel.Children.Add(Labelled("field.mosChoice", mosChoices));
            mosPanel.Children.Add(Parameters(("field.generatorDegree", generator), ("field.cardinality", cardinality), ("field.periods", periods), ("field.mode", mode)));
            mosPanel.Children.Add(scaleResult);
            keyboardCard.Children.Add(mosPanel);
            manualKeys.Children.Add(new TextBlock { Text = XenStrings.Get("field.selectedKey"), VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 6) });
            foreach (var (label, keyClass) in new[] { ("keyClass.white", "natural"), ("keyClass.black", "accidental"), ("keyClass.between", "between") }) {
                var button = Button(XenStrings.Get(label), () => SetSelectedKeyClass(keyClass));
                button.Name = "KeyClass-" + keyClass;
                manualKeys.Children.Add(button);
            }
            keyboardCard.Children.Add(manualKeys);
            tuningPage.Children.Add(Card(keyboardCard));
            tuningPage.Children.Add(KeyboardViewport(pitchPreview));
            var scaleCard = new StackPanel { Spacing = 10 };
            scaleCard.Children.Add(CardHeading("field.scale"));
            var scaleChoice = new Grid { ColumnDefinitions = new ColumnDefinitions("*,2*"), ColumnSpacing = 12 };
            scaleChoice.Children.Add(Labelled("field.tonic", tonic));
            var scaleList = Labelled("field.scale", scales);
            Grid.SetColumn(scaleList, 1); scaleChoice.Children.Add(scaleList);
            scaleCard.Children.Add(scaleChoice);
            var saveScale = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };
            editScale.VerticalAlignment = VerticalAlignment.Center;
            saveScale.Children.Add(editScale);
            Grid.SetColumn(customScaleName, 1); saveScale.Children.Add(customScaleName);
            var save = Button(XenStrings.Get("action.saveScale"), SaveScale);
            save.Name = "SaveScale";
            Grid.SetColumn(save, 2); saveScale.Children.Add(save);
            scaleCard.Children.Add(saveScale);
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
            periods.ValueChanged += (_, _) => { if (!loading) { customPeriods = true; LiveMosChanged(); } };
            mosChoices.SelectionChanged += (_, _) => { if (!loading) SelectMosChoice(); };
            mosKeyboard.IsCheckedChanged += (_, _) => { if (!loading && mosKeyboard.IsChecked == true) SetMosKeyboard(true); };
            manualKeyboard.IsCheckedChanged += (_, _) => { if (!loading && manualKeyboard.IsChecked == true) SetMosKeyboard(false); };
            keyboardCycle.ValueChanged += (_, _) => { if (!loading) ChangeKeyboardCycle(); };
            tonic.SelectionChanged += (_, _) => { if (!loading) SelectTonic(); };
            pitchPreview.DegreePressed += degree => { if (editScale.IsChecked == true) ToggleScaleDegree(degree); };
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
                seededNames = null;
                editingScaleId = null;
                LoadEntries();
                yaml.Text = TunxenFormat.Serialize(draft);
                if (explicitCount.Parent is Control row) row.IsVisible = draft.Divisions == 0;
                LoadMosFields();
                structureValid = true;
                RefreshPresetChoice();
                RefreshKeyboardCycle();
            } finally { loading = false; }
            RefreshKeyboardMode();
            RefreshScaleChoices();
            RefreshMosStatus();
            RefreshPitchPreview();
            RefreshApplyAction();
        }

        int FamilyCount => Math.Min(KeyboardTheory.For(draft)?.ReferenceDivision == 13 ? 9 : 7, draft.DegreeCount);

        /// <summary>Shows the keyboard MOS, or the default (family) MOS when the tuning stores none.</summary>
        void LoadMosFields() {
            var mos = draft.Keyboard.Mos;
            var fallback = KeyboardRegionizer.DefaultMos(draft);
            generator.Value = mos?.Generator ?? fallback.Generator;
            cardinality.Maximum = draft.DegreeCount;
            cardinality.Value = mos?.Cardinality ?? fallback.Cardinality;
            periods.Maximum = draft.DegreeCount;
            periods.Value = mos?.Periods ?? 1;
            RefreshModeMaximum();
            mode.Value = (mos?.Mode ?? fallback.Mode) + 1;
            customGenerator = mos != null && mos.Generator != fallback.Generator;
            customCardinality = mos != null && mos.Cardinality != fallback.Cardinality;
            customPeriods = (mos?.Periods ?? 1) != 1;
        }

        bool Manual => KeyboardRegionizer.IsManual(draft);

        void RefreshKeyboardMode() {
            bool wasLoading = loading;
            loading = true;
            try {
                bool manual = Manual;
                mosKeyboard.IsChecked = !manual;
                manualKeyboard.IsChecked = manual;
                mosPanel.IsVisible = !manual;
                manualKeys.IsVisible = manual;
            } finally { loading = wasLoading; }
        }

        /// <summary>Non-octave equal steps group the keyboard in cycles; changing the cycle keeps the step size.</summary>
        void RefreshKeyboardCycle() {
            bool cycle = draft.Divisions > 0 && Math.Abs(draft.PeriodCents - 1200) > 1e-6
                && Math.Abs(draft.PeriodCents - 1200 * Math.Log2(3)) > 1e-6;
            if (keyboardCycle.Parent is Control row) row.IsVisible = cycle;
            keyboardCycle.Value = draft.Divisions > 0 ? draft.Divisions : 1;
        }

        void ChangeKeyboardCycle() {
            if (draft.Divisions <= 0) return;
            double step = draft.PeriodCents / draft.Divisions;
            int steps = ReadInteger(keyboardCycle, draft.Divisions);
            loading = true;
            try {
                divisions.Value = steps;
                period.Text = (step * steps).ToString("R", XenStrings.InputCulture);
            } finally { loading = false; }
            LiveStructureChanged();
        }

        /// <summary>MOS builds every key from the MOS; key by key starts from the current keys and names.</summary>
        void SetMosKeyboard(bool on) {
            if (!structureValid) { RefreshKeyboardMode(); return; }
            if (on) {
                draft.Keyboard.Layout = KeyboardLayout.MosLayout;
                draft.Keyboard.Classes.Clear();
                RemoveSeededNames();
                StoreMos();
            } else {
                SeedManualNames();
                draft.Keyboard.Classes = KeyboardRegionizer.ClassesFromRoles(draft);
                draft.Keyboard.Layout = KeyboardLayout.ManualLayout;
            }
            SyncEntryClasses();
            RefreshKeyboardMode();
            RefreshMosStatus();
            RefreshTonicChoices();
            RefreshPitchPreview();
            RefreshApplyAction();
        }

        /// <summary>Copies the names shown with MOS into custom names, so a key-by-key keyboard keeps them editable.</summary>
        void SeedManualNames() {
            seededNames = null;
            string primary = PitchNotation.ResolvePrimary(draft, null);
            if (primary == "degree" || primary == "custom") return;
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

        /// <summary>Switching back to MOS drops copied names the user has not edited.</summary>
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

        void RefreshPresetChoice() {
            var all = presets.ItemsSource!.Cast<TuningDefinition>().ToArray();
            // A named preset (such as Rast) wins over the plain equal division it is built on.
            presets.SelectedItem = all.FirstOrDefault(preset => preset.Id == draft.Id) ?? all.FirstOrDefault(preset => draft.Divisions > 0
                && preset.Divisions == draft.Divisions && Math.Abs(preset.PeriodCents - draft.PeriodCents) < 1e-8);
        }

        // Presets with the same notes share an entry, labelled with all their names.
        static string ScaleLabel(ScaleDefinition scale) => scale.Id == ScalePresets.All ? XenStrings.Get("scale.all")
            : scale.Id.StartsWith(ScalePresets.Prefix, StringComparison.Ordinal)
                ? string.Join(" / ", ScalePresets.Keys(scale.Id).Select(key => XenStrings.Get("scale." + key)))
            : scale.Id == "rast" ? XenStrings.Get("preset.rast") : scale.Name;

        string DegreeLabel(int degree) {
            string label = PitchNotation.Format(draft, degree, PitchNotation.ResolvePrimary(draft, null), false);
            return label == degree.ToString(CultureInfo.InvariantCulture) ? label : $"{label} ({degree})";
        }

        void RefreshTonicChoices() {
            bool wasLoading = loading;
            loading = true;
            try {
                tonic.ItemsSource = Enumerable.Range(0, draft.DegreeCount).Select(DegreeLabel).ToArray();
                tonic.SelectedIndex = Math.Clamp(draft.Keyboard.Tonic, 0, draft.DegreeCount - 1);
            } finally { loading = wasLoading; }
        }

        void RefreshScaleChoices() {
            bool wasLoading = loading;
            loading = true;
            try {
                scaleChoices = ScalePresets.For(draft).ToList();
                scales.ItemsSource = scaleChoices.Select(ScaleLabel).ToArray();
                string? active = ScalePresets.Find(draft, draft.Keyboard.ActiveScaleId)?.Id ?? draft.Keyboard.ActiveScaleId;
                int index = scaleChoices.FindIndex(scale => scale.Id == active);
                scales.SelectedIndex = Math.Max(0, index);
            } finally { loading = wasLoading; }
            RefreshTonicChoices();
        }

        void SelectScale() {
            if (scales.SelectedIndex < 0 || scales.SelectedIndex >= scaleChoices.Count) return;
            draft.Keyboard.ActiveScaleId = scaleChoices[scales.SelectedIndex].Id;
            editingScaleId = null;
            RefreshPitchPreview();
            RefreshApplyAction();
        }

        void SelectTonic() {
            if (tonic.SelectedIndex < 0) return;
            draft.Keyboard.Tonic = tonic.SelectedIndex;
            RefreshPitchPreview();
            RefreshApplyAction();
        }

        /// <summary>
        /// Clicking a key while editing adds it to or removes it from the scale. A preset is first copied into a
        /// user scale, so presets themselves never change.
        /// </summary>
        void ToggleScaleDegree(int degree) {
            int n = draft.DegreeCount;
            var active = ScalePresets.Find(draft, draft.Keyboard.ActiveScaleId);
            var scale = editingScaleId == null ? null : draft.Scales.FirstOrDefault(s => s.Id == editingScaleId);
            if (scale == null) {
                scale = new ScaleDefinition {
                    Id = "custom-" + Guid.NewGuid().ToString("N"),
                    Name = string.IsNullOrWhiteSpace(customScaleName.Text) ? XenStrings.Get("scale.custom") : customScaleName.Text.Trim(),
                    Degrees = (active?.Degrees ?? Enumerable.Range(0, n)).ToList(),
                };
                draft.Scales.Add(scale);
                editingScaleId = scale.Id;
                draft.Keyboard.ActiveScaleId = scale.Id;
            }
            int relative = ((degree - draft.Keyboard.Tonic) % n + n) % n;
            if (!scale.Degrees.Remove(relative)) scale.Degrees.Add(relative);
            scale.Degrees.Sort();
            RefreshScaleChoices();
            RefreshPitchPreview();
            RefreshApplyAction();
        }

        /// <summary>Saves the current scale under the typed name as a user scale.</summary>
        void SaveScale() {
            var active = ScalePresets.Find(draft, draft.Keyboard.ActiveScaleId);
            string label = string.IsNullOrWhiteSpace(customScaleName.Text) ? XenStrings.Get("scale.custom") : customScaleName.Text.Trim();
            var editing = editingScaleId == null ? null : draft.Scales.FirstOrDefault(s => s.Id == editingScaleId);
            if (editing != null) {
                editing.Name = label;
            } else {
                var copy = new ScaleDefinition {
                    Id = "custom-" + Guid.NewGuid().ToString("N"), Name = label,
                    Degrees = (active?.Degrees ?? Enumerable.Range(0, draft.DegreeCount)).ToList(),
                };
                draft.Scales.Add(copy);
                draft.Keyboard.ActiveScaleId = copy.Id;
            }
            editingScaleId = null;
            RefreshScaleChoices();
            RefreshPitchPreview();
            RefreshApplyAction();
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
                var fallback = KeyboardRegionizer.DefaultMos(draft);
                if (!customGenerator) generator.Value = fallback.Generator;
                cardinality.Maximum = draft.DegreeCount;
                if (!customCardinality) cardinality.Value = fallback.Cardinality;
                periods.Maximum = draft.DegreeCount;
                if (!customPeriods) periods.Value = 1;
                RefreshModeMaximum();
                RefreshKeyboardCycle();
                structureValid = true;
                status.Text = "";
            } catch (Exception error) {
                structureValid = false;
                pitchPreview.SetTuning(null);
                status.Text = XenStrings.Get("error.invalid");
                ToolTip.SetTip(status, error.Message);
            } finally { loading = false; }
            RefreshKeyboardMode();
            if (structureValid) {
                if (!Manual && (customGenerator || customCardinality || customPeriods)) StoreMos();
                editingScaleId = null;
                RefreshScaleChoices();
                RefreshMosStatus();
                RefreshPitchPreview();
            }
            RefreshApplyAction();
        }

        void LiveReferenceChanged() => Guard(() => {
            draft.Reference.Degree = ReadInteger(referenceDegree, draft.Reference.Degree);
            draft.Reference.Frequency = (double)(frequency.Value ?? (decimal)draft.Reference.Frequency);
            draft.Validate();
            RefreshPitchPreview();
            RefreshApplyAction();
        });

        /// <summary>MOS parameter edits change only the keyboard; the scale stays as chosen.</summary>
        void LiveMosChanged() {
            if (loading || !structureValid || Manual) return;
            RefreshModeMaximum();
            StoreMos();
            RefreshMosStatus();
            RefreshTonicChoices();
            RefreshPitchPreview();
            RefreshApplyAction();
        }

        /// <summary>Modes rotate within one period, so a multi-period MOS has fewer modes than notes.</summary>
        void RefreshModeMaximum() {
            int count = (int)(cardinality.Value ?? 1), repeats = (int)(periods.Value ?? 1);
            mode.Maximum = Math.Max(1, count % repeats == 0 ? count / repeats : count);
        }

        KeyboardMos ReadMos() {
            int repeats = ReadInteger(periods, 1);
            return new KeyboardMos {
                Generator = ReadInteger(generator, 0), Cardinality = ReadInteger(cardinality, FamilyCount), Mode = ReadInteger(mode, 1) - 1,
                Periods = repeats == 1 ? null : repeats,
            };
        }

        void StoreMos() {
            try {
                draft.Keyboard.Mos = ReadMos();
            } catch (Exception) {
                draft.Keyboard.Mos = new KeyboardMos { Generator = 0, Cardinality = FamilyCount };
            }
        }

        /// <summary>
        /// Lists the MOS that hold in the tuning under note-count headings and selects the one the fields give, if any.
        /// The list is rebuilt when the degrees change, since the labels show generator cents.
        /// </summary>
        void RefreshMosChoices() {
            bool wasLoading = loading;
            loading = true;
            try {
                int n = draft.DegreeCount;
                bool allowImproper = Core.Util.Preferences.Default.XenAllowImproperMos;
                string key = allowImproper + ":" + string.Join(",",
                    Enumerable.Range(0, n + 1).Select(d => draft.GetCents(d).ToString("F3", CultureInfo.InvariantCulture)));
                if (key != mosChoiceKey) {
                    mosChoiceKey = key;
                    // Improper MOS are offered only when the preference allows them; the keyboard accepts either.
                    mosChoiceList = MosGenerator.Choices(n).Where(choice => allowImproper || choice.IsProper).ToArray();
                    var items = new List<ComboBoxItem>();
                    mosChoiceRows = new List<int>();
                    for (int i = 0; i < mosChoiceList.Count; i++) {
                        int notes = mosChoiceList[i].Mos.Cardinality;
                        if (i == 0 || mosChoiceList[i - 1].Mos.Cardinality != notes) {
                            items.Add(new ComboBoxItem {
                                Content = XenStrings.Get("mos.group", notes), IsEnabled = false,
                                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                            });
                            mosChoiceRows.Add(-1);
                        }
                        items.Add(new ComboBoxItem { Content = MosChoiceLabel(mosChoiceList[i]), Padding = new Thickness(24, 4, 12, 4) });
                        mosChoiceRows.Add(i);
                    }
                    mosChoices.ItemsSource = items;
                }
                KeyboardMos current;
                try { current = draft.Keyboard.Mos ?? ReadMos(); } catch (Exception) { current = new KeyboardMos(); }
                int index = -1;
                for (int i = 0; i < mosChoiceList.Count && index < 0; i++) if (mosChoiceList[i].Matches(current, n)) index = i;
                mosChoices.SelectedIndex = index < 0 ? -1 : mosChoiceRows.IndexOf(index);
            } finally { loading = wasLoading; }
        }

        string MosChoiceLabel(MosChoice choice) {
            string pattern = $"{choice.LargeCount}L{choice.SmallCount}s";
            string cents = draft.GetCents(choice.Mos.Generator).ToString("F1", XenStrings.InputCulture);
            return choice.Periods == 1
                ? XenStrings.Get("mos.choice", pattern, choice.Mos.Generator, cents)
                : XenStrings.Get("mos.choiceRepeated", pattern, choice.Mos.Generator, cents, choice.Periods);
        }

        /// <summary>
        /// Applies a listed MOS to the fields. The default MOS keeps its own generator and mode when it is the same
        /// MOS (12-EDO 5L2s stays generator 7 and C major); other choices start at mode 1.
        /// </summary>
        void SelectMosChoice() {
            int row = mosChoices.SelectedIndex;
            if (row < 0 || row >= mosChoiceRows.Count) return;
            // A heading is not a choice: return to the row the fields give.
            if (mosChoiceRows[row] < 0) { RefreshMosChoices(); return; }
            var choice = mosChoiceList[mosChoiceRows[row]];
            var fallback = KeyboardRegionizer.DefaultMos(draft);
            var mos = choice.Matches(fallback, draft.DegreeCount) ? fallback.Clone() : choice.Mos.Clone();
            loading = true;
            try {
                generator.Value = mos.Generator;
                cardinality.Value = mos.Cardinality;
                periods.Value = mos.Periods ?? 1;
                RefreshModeMaximum();
                mode.Value = mos.Mode + 1;
                customGenerator = mos.Generator != fallback.Generator;
                customCardinality = mos.Cardinality != fallback.Cardinality;
                customPeriods = (mos.Periods ?? 1) != 1;
            } finally { loading = false; }
            LiveMosChanged();
        }

        void RefreshMosStatus() {
            if (!structureValid || Manual) { scaleResult.Text = ""; return; }
            RefreshMosChoices();
            var mos = draft.Keyboard.Mos ?? ReadMos();
            var degrees = KeyboardRegionizer.MosDegrees(draft, mos);
            if (degrees == null) {
                scaleResult.Text = XenStrings.Get("status.mosUnavailable");
                return;
            }
            int n = draft.DegreeCount;
            var steps = degrees.Select((d, i) => (i + 1 < degrees.Length ? degrees[i + 1] : n) - d).ToArray();
            int small = steps.Min(), large = steps.Max();
            string pattern = string.Concat(steps.Select(step => large == small ? "=" : step == large ? "L" : "s"));
            string counts = large == small ? "" : $"{steps.Count(step => step == large)}L{steps.Count(step => step == small)}s · ";
            string improper = large != small && !MosGenerator.IsProper(steps) ? " · " + XenStrings.Get("mos.improper") : "";
            scaleResult.Text = counts + (pattern.Length <= 80 ? pattern : pattern[..80] + "…") + improper;
        }

        void RefreshPitchPreview() {
            if (!structureValid) return;
            pitchPreview.SetTuning(draft, ScalePresets.ActiveDegrees(draft), whiteKeysOnly: KeyboardRegionizer.IsUnavailableMos(draft));
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

        static TextBlock CardHeading(string key) => new TextBlock {
            Text = XenStrings.Get(key), FontSize = 16, FontWeight = Avalonia.Media.FontWeight.SemiBold,
        };

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
                // Key classes only apply to a key-by-key keyboard.
                if (!Manual) continue;
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
