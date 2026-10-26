using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpenUtau.App.Controls;
using OpenUtau.App.ViewModels;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Microtonal;
using OpenUtau.Core.Ustx;

namespace OpenUtau.App {
    /// <summary>
    /// README screenshots: the tuning settings page in English and Japanese and a 31-EDO piano roll.
    /// Written only during an opt-in capture run (OPENUTAU_KEYBOARD_CAPTURES), into its "readme" folder.
    /// </summary>
    internal static class ReadmeCaptures {
        public static void Capture(string directory) {
            directory = Path.Combine(directory, "readme");
            Directory.CreateDirectory(directory);
            var manager = DocManager.Inst;
            manager.PostOnUIThread = action => Dispatcher.UIThread.Post(action);
            manager.Initialize(Thread.CurrentThread, TaskScheduler.Default);
            var previousProject = manager.Project;
            try {
                var project = Project();
                manager.ExecuteCmd(new LoadProjectNotification(project));
                foreach (string language in new[] { "en-US", "ja-JP" }) {
                    App.SetLanguage(language);
                    CaptureTuning(directory, language);
                }
                App.SetLanguage("en-US");
                CapturePianoRoll(directory, project);
            } finally {
                App.SetLanguage("en-US");
                manager.ExecuteCmd(new LoadProjectNotification(previousProject));
            }
        }

        /// <summary>A short 31-EDO phrase that uses neutral and septimal steps between the white keys.</summary>
        static UProject Project() {
            var project = Ustx.Create();
            project.pitchSystem = TuningPresets.EqualDivision(31);
            var part = new UVoicePart { trackNo = 0, position = 0, Duration = 1920 * 4 };
            // Degrees of 31-EDO from C4: E half-flat (9), F half-sharp (14), B half-flat (27) and so on.
            var phrase = new (int Degree, int Length, string Lyric)[] {
                (0, 480, "a"), (5, 480, "ka"), (9, 960, "sa"), (13, 480, "ta"), (14, 480, "na"),
                (18, 960, "ha"), (23, 480, "ma"), (27, 480, "ya"), (31, 960, "ra"),
                (27, 480, "wa"), (23, 480, "ka"), (18, 480, "sa"), (14, 480, "ta"), (9, 960, "na"),
            };
            int position = 0;
            foreach (var (degree, length, lyric) in phrase) {
                var note = project.CreateNote(60, position, length);
                note.lyric = lyric;
                note.pitchAddress = new PitchAddress { Degree = degree };
                note.RefreshPitch(project, project.tracks[0]);
                part.notes.Add(note);
                position += length;
            }
            project.parts.Add(part);
            part.Validate(new ValidateOptions(), project, project.tracks[0]);
            return project;
        }

        static void CaptureTuning(string directory, string language) {
            var dialog = new Views.TuningDialog(projectDefault: true) { Width = 960, Height = 760 };
            try {
                dialog.Show();
                Dispatcher.UIThread.RunJobs();
                var presets = dialog.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "TuningPresets");
                presets.SelectedItem = presets.ItemsSource!.Cast<TuningDefinition>()
                    .Single(t => t.Divisions == 31 && Math.Abs(t.PeriodCents - 1200) < 0.001);
                Dispatcher.UIThread.RunJobs();
                using var frame = dialog.CaptureRenderedFrame();
                frame?.Save(Path.Combine(directory, $"tuning-settings-{language}.png"));
            } finally { dialog.Close(); }
        }

        static void CapturePianoRoll(string directory, UProject project) {
            var notes = new NotesViewModel();
            var model = new PianoRollViewModel { NotesViewModel = notes };
            var roll = new PianoRoll(model);
            var window = new Window { Width = 1280, Height = 800, Content = roll };
            try {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                var part = (UVoicePart)project.parts[0];
                // Notes of a track without a loaded singer draw as errors. Loading the project resolves singers
                // again, so the stand-in singer is set afterwards and the part validated with it.
                var singer = new CaptureSinger();
                project.tracks[0].Singer = singer;
                project.tracks[0].Validate(new ValidateOptions(), project);
                part.Validate(new ValidateOptions(), project, project.tracks[0]);
                DocManager.Inst.ExecuteCmd(new LoadPartNotification(part, project, 0));
                // The phonemiser runs on a worker; wait until every note has valid phonemes.
                for (int i = 0; i < 100; i++) {
                    Dispatcher.UIThread.RunJobs();
                    if (part.notes.All(n => part.phonemes.Any(p => p.Parent == n)) && part.phonemes.All(p => !p.Error)) break;
                    Thread.Sleep(50);
                    part.Validate(new ValidateOptions(), project, project.tracks[0]);
                }
                notes.TickWidth = 0.12;
                notes.TrackHeight = 14;
                notes.TickOffset = 0;
                // Centre the phrase: rows count up from the lowest pitch, offsets down from the highest.
                double low = notes.RowMapper.RowAtMidi(part.notes.Min(n => n.ResolvedTone));
                double high = notes.RowMapper.RowAtMidi(part.notes.Max(n => n.ResolvedTone));
                notes.TrackOffset = Math.Max(0, notes.TrackCount - 1 - (low + high) / 2 - notes.ViewportTracks / 2);
                Dispatcher.UIThread.RunJobs();
                using var frame = window.CaptureRenderedFrame();
                frame?.Save(Path.Combine(directory, "piano-roll-31edo.png"));
            } finally { window.Close(); }
        }

        /// <summary>A loaded singer without voice data, used only so that notes draw as valid.</summary>
        sealed class CaptureSinger : USinger {
            public CaptureSinger() { found = true; loaded = true; }
            public override string Id => "xen-readme-capture";
            public override string Name => "README capture";
            public override USingerType SingerType => USingerType.Classic;
            // One default voice colour, as every loaded bank has at least one subbank.
            public override IList<USubbank> Subbanks { get; } = new List<USubbank> { new USubbank(new Classic.Subbank()) };
            public override bool TryGetOto(string phoneme, out UOto oto) {
                oto = UOto.OfDummy(phoneme);
                return true;
            }
        }
    }
}
