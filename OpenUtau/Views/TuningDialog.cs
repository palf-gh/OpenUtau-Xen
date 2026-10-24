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
        readonly NumericUpDown mode = Number(0, 65535, 0, 0);
        readonly TextBox ratioGenerator = new TextBox { Text = "3/2" };
        readonly TextBox customScaleName = new TextBox { Text = XenStrings.Get("scale.custom") };
        readonly TextBox customScaleDegrees = new TextBox { Text = "0" };
        readonly TextBlock scaleResult = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        List<DegreeEntry> entries = new List<DegreeEntry>();

        public TuningDialog(NotesViewModel? notes = null, bool projectDefault = false) {
            this.notes = notes;
            draft = EffectiveTuning.Clone();
            presets.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<TuningDefinition>(
                (tuning, _) => new TextBlock { Text = tuning == null ? "" : XenStrings.PresetName(tuning) });
            Title = XenStrings.Get("tuning.title");
            Width = 850; Height = 620; MinWidth = 650; MinHeight = 440;
            XenDialogStyles.Apply(this);
            yaml.Classes.Add("definition-editor");
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var root = new DockPanel { Margin = new Thickness(18) };
            var actions = new StackPanel { Spacing = 10 };
            var files = new WrapPanel { Orientation = Orientation.Horizontal };
            files.Children.Add(Button(XenStrings.Get("import.tuning"), ImportTuning));
            files.Children.Add(Button(XenStrings.Get("export.tuning"), ExportTuning));
            files.Children.Add(Button(XenStrings.Get("import.scala"), ImportScala));
            files.Children.Add(Button(XenStrings.Get("export.scala"), ExportScala));
            files.Children.Add(Button(XenStrings.Get("import.kbm"), ImportKbm));
            files.Children.Add(Button(XenStrings.Get("export.kbm"), ExportKbm));
            actions.Children.Add(files);
            actions.Children.Add(status);
            var commands = new WrapPanel { Orientation = Orientation.Horizontal };
            commands.Children.Add(Button(XenStrings.Get("action.preview"), Preview));
            commands.Children.Add(Button(XenStrings.Get("action.applyTuning"), () => Apply(false, false)));
            commands.Children.Add(Button(XenStrings.Get("action.changeReference"), () => Apply(true, false)));
            commands.Children.Add(Button(XenStrings.Get("action.applyDisplay"), () => Apply(false, true)));
            commands.Children.Add(Button(XenStrings.Get("action.inherit"), InheritProjectTuning));
            commands.Children.Add(Button(XenStrings.Get("action.close"), Close));
            actions.Children.Add(commands);
            DockPanel.SetDock(actions, Dock.Bottom);
            root.Children.Add(actions);
            var content = new StackPanel { Spacing = 9 };
            content.Children.Add(Field(XenStrings.Get("field.scope"), scope));
            content.Children.Add(Field(XenStrings.Get("field.preset"), presets));
            content.Children.Add(Field(XenStrings.Get("field.name"), name));
            content.Children.Add(Field(XenStrings.Get("field.divisions"), divisions));
            content.Children.Add(Field(XenStrings.Get("field.explicitCount"), explicitCount));
            content.Children.Add(Field(XenStrings.Get("field.period"), period));
            content.Children.Add(Field(XenStrings.Get("field.referenceDegree"), referenceDegree));
            content.Children.Add(Field(XenStrings.Get("field.frequency"), frequency));
            content.Children.Add(preserve);
            content.Children.Add(Field(XenStrings.Get("field.scale"), scales));
            content.Children.Add(Button(XenStrings.Get("action.rebuild"), RebuildEntries));
            degrees.Columns.Add(new DataGridTextColumn { Header = XenStrings.Get("field.degree"), Binding = new Binding(nameof(DegreeEntry.Degree)), IsReadOnly = true });
            degrees.Columns.Add(new DataGridTextColumn { Header = XenStrings.Get("field.interval"), Binding = new Binding(nameof(DegreeEntry.Interval)) });
            degrees.Columns.Add(new DataGridTextColumn { Header = XenStrings.Get("field.name"), Binding = new Binding(nameof(DegreeEntry.Name)) });
            degrees.Columns.Add(new DataGridTextColumn { Header = XenStrings.Get("field.aliases"), Binding = new Binding(nameof(DegreeEntry.Aliases)) });
            degrees.Columns.Add(new DataGridTextColumn { Header = XenStrings.Get("field.keyClass"), Binding = new Binding(nameof(DegreeEntry.KeyClass)) });
            content.Children.Add(degrees);
            var scaleContent = new StackPanel { Spacing = 12, Margin = new Thickness(8) };
            scaleContent.Children.Add(Field(XenStrings.Get("field.generatorDegree"), generator));
            scaleContent.Children.Add(Field(XenStrings.Get("field.cardinality"), cardinality));
            scaleContent.Children.Add(Field(XenStrings.Get("field.mode"), mode));
            scaleContent.Children.Add(Button(XenStrings.Get("action.generateSubset"), GenerateScale));
            scaleContent.Children.Add(scaleResult);
            scaleContent.Children.Add(new Separator());
            scaleContent.Children.Add(Field(XenStrings.Get("field.generatorInterval"), ratioGenerator));
            scaleContent.Children.Add(Button(XenStrings.Get("action.generateTuning"), GenerateIntervalTuning));
            scaleContent.Children.Add(new Separator());
            scaleContent.Children.Add(Field(XenStrings.Get("field.scaleName"), customScaleName));
            scaleContent.Children.Add(Field(XenStrings.Get("field.degrees"), customScaleDegrees));
            scaleContent.Children.Add(Button(XenStrings.Get("action.addSubset"), AddDegreeSubset));
            var advanced = new DockPanel { Margin = new Thickness(8) };
            var yamlActions = new WrapPanel();
            yamlActions.Children.Add(Button(XenStrings.Get("action.loadDefinition"), ReadYaml));
            yamlActions.Children.Add(Button(XenStrings.Get("action.refreshDefinition"), () => { ReadFields(); yaml.Text = TunxenFormat.Serialize(draft); }));
            DockPanel.SetDock(yamlActions, Dock.Bottom);
            advanced.Children.Add(yamlActions);
            advanced.Children.Add(yaml);
            root.Children.Add(new TabControl { ItemsSource = new[] {
                new TabItem { Header = XenStrings.Get("tab.tuning"), Content = new ScrollViewer { Content = content } },
                new TabItem { Header = XenStrings.Get("tab.generator"), Content = new ScrollViewer { Content = scaleContent } },
                new TabItem { Header = XenStrings.Get("tab.definition"), Content = advanced },
            } });
            Content = root;
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
            var result = MosGenerator.Generate(draft, ReadInteger(generator, 0), ReadInteger(cardinality, 7), ReadInteger(mode, 0));
            draft.Scales.RemoveAll(s => s.Id == result.Scale.Id);
            draft.Scales.Add(result.Scale);
            draft.Keyboard.ActiveScaleId = result.Scale.Id;
            scaleResult.Text = $"{result.Scale.Name}\n{string.Join(", ", result.Scale.Degrees)}\n{result.Pattern}";
            LoadFields();
        });
        void GenerateIntervalTuning() => Guard(() => {
            ReadFields();
            draft = MosGenerator.CreateTuning(draft.Period, ParseInterval(ratioGenerator.Text ?? "3/2"), ReadInteger(cardinality, 7));
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
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("240,*"), ColumnSpacing = 12 };
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
