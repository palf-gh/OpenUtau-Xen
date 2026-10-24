using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenUtau.Classic;
using OpenUtau.Core.Format;
using OpenUtau.Core.Microtonal;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Test {
    public class XenPluginTest {
        static (UProject project, UVoicePart part, UNote note) Create(bool native) {
            var project = Ustx.Create();
            if (native) project.pitchSystem = TuningPresets.EqualDivision(53);
            var part = new UVoicePart { trackNo = 0, Duration = 1920 };
            var note = project.CreateNote(60, 0, 480);
            if (native) note.pitchAddress = new PitchAddress { Degree = 1, ResidualCents = 0.125 };
            else note.tuning = 25;
            note.pitch.data = new List<PitchPoint> {
                new PitchPoint(-40, 0), new PitchPoint(0, 1), new PitchPoint(100, -2),
            };
            note.pitch.snapFirst = false;
            project.parts.Add(part);
            part.notes.Add(note);
            project.timeAxis.BuildSegments(project);
            note.RefreshPitch(project, project.tracks[0]);
            return (project, part, note);
        }

        static UNote Parse(UProject project, UVoicePart part, UNote note, string fields) {
            string path = Path.GetTempFileName();
            try {
                File.WriteAllText(path, "[#0000]\n" + fields + "\n[#TRACKEND]\n", new UTF8Encoding(false));
                var (removed, added) = Ust.ParsePlugin(project, part, note, note, new List<UNote> { note }, path, "utf-8");
                Assert.Same(note, Assert.Single(removed));
                return Assert.Single(added);
            } finally { File.Delete(path); }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void LyricOnlyPluginKeepsRelativeCurveAndMicrotonalPitch(bool native) {
            var (project, part, note) = Create(native);
            double before = note.AdjustedTone;
            var sourcePoints = note.pitch.data.Select(p => (p.X, p.Y)).ToArray();
            var changed = Parse(project, part, note, "Lyric=edited vowel\nLength=720");
            Assert.Equal("edited vowel", changed.lyric);
            Assert.Equal(720, changed.duration);
            Assert.Equal(before, changed.AdjustedTone, 5);
            Assert.Equal(note.pitch.data.Count, changed.pitch.data.Count);
            for (int i = 0; i < sourcePoints.Length; i++) {
                Assert.Equal(sourcePoints[i].X, changed.pitch.data[i].X);
                Assert.Equal(sourcePoints[i].Y, changed.pitch.data[i].Y, 5);
            }
            Assert.Equal(sourcePoints, note.pitch.data.Select(p => (p.X, p.Y)).ToArray());
            Assert.Equal(note.tuning, changed.tuning);
            Assert.Equal(note.pitchAddress?.Degree, changed.pitchAddress?.Degree);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PartialPitchFieldsKeepUntouchedExportedCoordinates(bool native) {
            var (project, part, note) = Create(native);
            float offset = (note.AdjustedTone - note.tone) * 10;
            var changed = Parse(project, part, note, "PBY=4.5,6.5");
            Assert.Equal(3, changed.pitch.data.Count);
            Assert.Equal(note.pitch.data[0].Y, changed.pitch.data[0].Y, 5);
            Assert.Equal(4.5f - offset, changed.pitch.data[1].Y, 5);
            Assert.Equal(6.5f - offset, changed.pitch.data[2].Y, 5);
            Assert.Equal(note.AdjustedTone, changed.AdjustedTone);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReturnedAbsolutePitchFieldsConvertBackExactlyOnce(bool native) {
            var (project, part, note) = Create(native);
            float offset = (note.AdjustedTone - note.tone) * 10;
            var changed = Parse(project, part, note, "PBS=-30;3.5\nPBW=30,100\nPBY=6,8");
            Assert.Equal(3, changed.pitch.data.Count);
            Assert.Equal(-30, changed.pitch.data[0].X);
            Assert.Equal(0, changed.pitch.data[1].X);
            Assert.Equal(100, changed.pitch.data[2].X);
            Assert.Equal(3.5f - offset, changed.pitch.data[0].Y, 5);
            Assert.Equal(6f - offset, changed.pitch.data[1].Y, 5);
            Assert.Equal(8f - offset, changed.pitch.data[2].Y, 5);
            Assert.Equal(note.AdjustedTone, changed.AdjustedTone);
        }
    }
}
