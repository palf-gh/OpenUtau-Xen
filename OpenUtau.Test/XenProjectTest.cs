using System;
using System.IO;
using System.Linq;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Microtonal;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Test {
    public class XenProjectTest {
        static (UProject project, UVoicePart part, UNote note) Create() {
            var project = Ustx.Create();
            project.pitchSystem = TuningPresets.All.Single(t => t.Id == "53edo").Clone();
            var part = new UVoicePart { trackNo = 0, Duration = 1920 };
            var note = project.CreateNote(60, 0, 480);
            note.pitchAddress = new PitchAddress { Degree = 5, ResidualCents = 0.123456789, Spelling = "C#4" };
            note.RefreshPitch(project, project.tracks[0]);
            part.notes.Add(note);
            project.parts.Add(part);
            return (project, part, note);
        }

        [Fact]
        public void NativeRoundTripEmbedsTuningAndExactAddress() {
            var (project, _, note) = Create();
            double before = note.ResolvedTone;
            var text = XenProjectFormat.Serialize(project);
            Assert.Contains("format: openutau-xen-project", text);
            var restored = XenProjectFormat.Parse(text);
            var actual = Assert.Single(Assert.IsType<UVoicePart>(Assert.Single(restored.parts)).notes);
            Assert.Equal("53edo", restored.pitchSystem.Id);
            Assert.Equal(5, actual.pitchAddress!.Degree);
            Assert.Equal("C#4", actual.pitchAddress.Spelling);
            Assert.Equal(0.123456789, actual.pitchAddress.ResidualCents);
            Assert.Equal(before, actual.ResolvedTone, 11);
            Assert.Null(project.voiceParts);
        }

        [Fact]
        public void NativeRejectsUnknownVersionsAndIdentifiers() {
            var (project, _, _) = Create();
            string text = XenProjectFormat.Serialize(project);
            Assert.Throws<FileFormatException>(() => XenProjectFormat.Parse(text.Replace("version: 1", "version: 999")));
            Assert.Throws<FileFormatException>(() => XenProjectFormat.Parse(text.Replace(XenProjectFormat.Identifier, "other-project")));
        }

        [Theory]
        [InlineData("")]
        [InlineData("{}")]
        [InlineData("format: openutau-xen-project\nversion: 1")]
        [InlineData("format: openutau-xen-project\nversion: 1\nproject: null")]
        [InlineData("version: 1\nproject: {}")]
        [InlineData("format: openutau-xen-project\nproject: {}")]
        public void NativeRejectsIncompleteEnvelope(string text) {
            Assert.Throws<FileFormatException>(() => XenProjectFormat.Parse(text));
        }

        [Fact]
        public void RenamingLegacyProjectDoesNotSilentlyLoadAnEmptyNativeSong() {
            var (project, _, _) = Create();
            project.BeforeSave();
            string legacy;
            try { legacy = Yaml.DefaultSerializer.Serialize(project); }
            finally { project.AfterSave(); }
            Assert.Throws<FileFormatException>(() => XenProjectFormat.Parse(legacy, "renamed.ustxen"));
        }

        [Fact]
        public void CompatibilityExportPreservesPitchWithinHalfCentWithoutMutatingSource() {
            var (project, _, note) = Create();
            note.tuning = 13;
            double before = note.ResolvedTone + note.tuning / 100.0;
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ustx");
            try {
                XenProjectFormat.ExportUstx(path, project);
                string text = File.ReadAllText(path);
                Assert.DoesNotContain("pitch_system", text);
                Assert.DoesNotContain("pitch_address", text);
                Assert.DoesNotContain(XenProjectFormat.Identifier, text);
                var legacy = Ustx.Load(path);
                var actual = Assert.Single(Assert.IsType<UVoicePart>(Assert.Single(legacy.parts)).notes);
                Assert.InRange(Math.Abs(actual.tone + actual.tuning / 100.0 - before) * 100, 0, 0.5000001);
                Assert.Equal(5, note.pitchAddress!.Degree);
                Assert.Equal(before, note.ResolvedTone + note.tuning / 100.0);
                Assert.Equal(string.Empty, project.FilePath);
            } finally { File.Delete(path); }
        }

        [Theory]
        [InlineData(442)]
        [InlineData(432)]
        [InlineData(415)]
        public void ReferenceCommandKeepsDegreeAndUndoRestoresExactPitch(double frequency) {
            var (project, _, note) = Create();
            double before = note.ResolvedTone;
            var target = project.pitchSystem.Clone();
            target.Reference.Frequency = frequency;
            var command = new SetTuningCommand(project, null, target, referenceOnly: true);
            command.Execute();
            Assert.Equal(5, note.pitchAddress!.Degree);
            Assert.Equal(before + 12 * Math.Log2(frequency / 440), note.ResolvedTone, 10);
            command.Unexecute();
            Assert.Equal(before, note.ResolvedTone, 10);
            command.Execute();
            Assert.Equal(frequency, project.pitchSystem.Reference.Frequency);
        }

        [Fact]
        public void ProjectChangeLeavesTrackOverrideUntouched() {
            var (project, _, note) = Create();
            project.tracks[0].pitchSystem = project.pitchSystem.Clone();
            double before = note.ResolvedTone;
            var target = TuningPresets.Default;
            target.Reference.Frequency = 432;
            var command = new SetTuningCommand(project, null, target);
            command.Execute();
            note.RefreshPitch(project, project.tracks[0]);
            Assert.Equal(before, note.ResolvedTone, 10);
            Assert.Equal("53edo", project.tracks[0].pitchSystem!.Id);
        }

        [Fact]
        public void DisplayChangeAndNoteClonePreservePitch() {
            var (project, _, note) = Create();
            double before = note.ResolvedTone;
            var target = project.pitchSystem.Clone();
            target.Keyboard.Mode = PitchDisplayMode.Compact;
            var command = new SetTuningCommand(project, project.tracks[0], target, displayOnly: true);
            command.Execute();
            Assert.Equal(before, note.ResolvedTone);
            var cloned = note.Clone();
            Assert.NotSame(note.pitchAddress, cloned.pitchAddress);
            Assert.Equal(before, cloned.ResolvedTone);
            command.Unexecute();
            Assert.Null(project.tracks[0].pitchSystem);
            Assert.Equal(before, note.ResolvedTone);
        }

        [Fact]
        public void PreserveFrequencyRetuningAndUndoAreExact() {
            var (project, _, note) = Create();
            double before = note.ResolvedTone;
            var command = new SetTuningCommand(project, project.tracks[0], TuningPresets.Default, preserveFrequency: true);
            command.Execute();
            Assert.Equal(before, note.ResolvedTone, 10);
            Assert.NotEqual(0, note.pitchAddress!.ResidualCents);
            command.Unexecute();
            Assert.Equal(5, note.pitchAddress!.Degree);
            Assert.Equal(before, note.ResolvedTone);
        }

        [Fact]
        public void DisplayOnlyAndReferenceOnlyCannotReplaceTheIntervalSystem() {
            var (project, _, note) = Create();
            double before = note.ResolvedTone;
            var draft = project.pitchSystem.Clone();
            draft.Divisions = 31;
            draft.Period = PitchInterval.FromRatio("3/1");
            draft.Reference.Frequency = 432;
            draft.Keyboard.Mode = PitchDisplayMode.Compact;
            var display = new SetTuningCommand(project, null, draft, displayOnly: true);
            display.Execute();
            Assert.Equal(53, project.pitchSystem.Divisions);
            Assert.Equal("2/1", project.pitchSystem.Period.Ratio);
            Assert.Equal(440, project.pitchSystem.Reference.Frequency);
            Assert.Equal(before, note.ResolvedTone);
            var reference = new SetTuningCommand(project, null, draft, referenceOnly: true);
            reference.Execute();
            Assert.Equal(53, project.pitchSystem.Divisions);
            Assert.Equal("2/1", project.pitchSystem.Period.Ratio);
            Assert.Equal(before + 12 * Math.Log2(432.0 / 440), note.ResolvedTone, 10);
        }

        [Fact]
        public void CommonnoteInterchangeCarriesExactPitchAcrossTunings() {
            var (project, _, note) = Create();
            string json = Commonnote.Dumps(new System.Collections.Generic.List<UNote> { note }, project);
            Assert.Contains("xen_midi", json);
            var target = Ustx.Create();
            var restored = Assert.Single(Commonnote.Loads(json, target));
            Assert.Equal(note.ResolvedTone, restored.ResolvedTone, 10);
            Assert.Equal(note.lyric, restored.lyric);
        }

        [Fact]
        public void UstExportCarriesMicrotonalBaseAndFineTuningInPitchCurve() {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var (project, part, note) = Create();
            note.tuning = 13;
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ust");
            try {
                OpenUtau.Classic.Ust.SavePart(project, part, path);
                var legacy = OpenUtau.Classic.Ust.Load(new[] { path });
                var actual = Assert.Single(Assert.IsType<UVoicePart>(Assert.Single(legacy.parts)).notes);
                Assert.InRange(Math.Abs(actual.tone + actual.pitch.data[0].Y / 10.0 - note.AdjustedTone) * 100, 0, 0.501);
            } finally { File.Delete(path); }
        }
    }
}
