using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Microtonal;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Test {
    public class SplitNotesAtTickTest {
        [Fact]
        public void SplitKeepsExactPitchAndUndoRestoresOriginalNote() {
            var project = Ustx.Create();
            var part = new UVoicePart { position = 480, trackNo = 0, Duration = 1920 };
            var note = project.CreateNote(60, 120, 480);
            note.pitchAddress = new PitchAddress { Degree = 17, ResidualCents = 3.25 };
            note.phonemeOverrides.Add(new UPhonemeOverride { index = 0, phoneme = "source" });
            note.phonemeIndexes = new[] { 0 };
            part.notes.Add(note);
            var command = new SplitNotesAtTickCommand(project, part, new[] { note }, 300, "-");
            command.Execute();
            Assert.Equal(2, part.notes.Count);
            Assert.Equal(180, note.duration);
            var right = System.Linq.Enumerable.Last(part.notes);
            Assert.Equal(300, right.position);
            Assert.Equal(300, right.duration);
            Assert.Equal(17, right.pitchAddress.Degree);
            Assert.Equal(3.25, right.pitchAddress.ResidualCents);
            Assert.NotSame(note.pitchAddress, right.pitchAddress);
            Assert.Equal("-", right.lyric);
            Assert.Empty(right.phonemeOverrides);
            Assert.Empty(right.phonemeIndexes);
            Assert.True(right.pitch.snapFirst);
            Assert.Single(note.phonemeOverrides);
            command.Unexecute();
            Assert.Single(part.notes);
            Assert.Equal(480, note.duration);
            command.Execute();
            Assert.Equal(2, part.notes.Count);
        }

        [Fact]
        public void ShortcutAndKnifeShareFreshPitchAndUndoableDragPath() {
            var project = Ustx.Create();
            var part = new UVoicePart { position = 960, trackNo = 0, Duration = 1920 };
            var note = project.CreateNote(60, 120, 960);
            note.pitch.data.Clear();
            note.pitch.AddPoint(new PitchPoint(-120, 30));
            note.pitch.AddPoint(new PitchPoint(650, -20));
            note.vibrato.length = 75;
            note.vibrato.@in = 15;
            note.vibrato.@out = 20;
            note.vibrato.shift = 30;
            var source = note.Clone();
            part.notes.Add(note);
            var initial = new SplitNotesAtTickCommand(project, part, new[] { note }, 300, "-");
            initial.Execute();
            var right = initial.RightNotes[0];
            var move = new SplitNotesAtTickCommand(project, part, note, right, source, 600);
            move.Execute();
            var expectedRight = project.CreateNote(60, 600, 480);
            Assert.Equal(expectedRight.pitch.data.Count, right.pitch.data.Count);
            for (int i = 0; i < expectedRight.pitch.data.Count; i++) {
                Assert.Equal(expectedRight.pitch.data[i].X, right.pitch.data[i].X);
                Assert.Equal(expectedRight.pitch.data[i].Y, right.pitch.data[i].Y);
            }
            Assert.Equal(-120, note.pitch.data[0].X);
            Assert.Equal(650, note.pitch.data[1].X);
            Assert.Equal(2, part.notes.Count);
            var shortcutPart = new UVoicePart { position = part.position, trackNo = 0, Duration = 1920 };
            var shortcutLeft = source.Clone();
            shortcutPart.notes.Add(shortcutLeft);
            var shortcut = new SplitNotesAtTickCommand(project, shortcutPart,
                new[] { shortcutLeft }, 600, "-");
            shortcut.Execute();
            var shortcutRight = shortcut.RightNotes[0];
            Assert.Equal(shortcutLeft.vibrato.length, note.vibrato.length);
            Assert.Equal(shortcutRight.vibrato.length, right.vibrato.length);
            Assert.Equal(shortcutRight.vibrato.shift, right.vibrato.shift);
            Assert.Equal(shortcutRight.pitch.data[0].X, right.pitch.data[0].X);
            Assert.Equal(shortcutRight.pitch.data[0].Y, right.pitch.data[0].Y);
            move.Unexecute();
            Assert.Equal(300, right.position);
            Assert.Equal(180, note.duration);
            initial.Unexecute();
            Assert.Single(part.notes);
            Assert.Equal(960, note.duration);
            Assert.Equal(75, note.vibrato.length);
            Assert.Equal(15, note.vibrato.@in);
            Assert.Equal(20, note.vibrato.@out);
            Assert.Equal(30, note.vibrato.shift);
            initial.Execute();
            move.Execute();
            Assert.Equal(600, right.position);
            Assert.Equal(480, note.duration);
            Assert.Equal(2, part.notes.Count);
        }

        [Theory]
        [InlineData(120)]
        [InlineData(600)]
        [InlineData(700)]
        public void BoundariesAndOutsideNotesAreNotSplit(int tick) {
            var project = Ustx.Create();
            var part = new UVoicePart { trackNo = 0, Duration = 1920 };
            var note = project.CreateNote(60, 120, 480);
            part.notes.Add(note);
            var command = new SplitNotesAtTickCommand(project, part, new[] { note }, tick, "-");
            command.Execute();
            Assert.Single(part.notes);
            Assert.Equal(480, note.duration);
        }
    }
}
