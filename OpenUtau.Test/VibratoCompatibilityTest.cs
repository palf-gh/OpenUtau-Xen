using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using OpenUtau.Classic;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;
using Xunit;

namespace OpenUtau.Test {
    public class VibratoCompatibilityTest {
        static (UProject project, UVoicePart part, UNote note) Create() {
            var project = Ustx.Create();
            var part = new UVoicePart { trackNo = 0, Duration = 960 };
            var note = project.CreateNote(60, 0, 960);
            note.tuning = 25;
            note.pitch.snapFirst = false;
            note.pitch.data = new List<PitchPoint> {
                new(-100, 0, PitchPointShape.io),
                new(0, 4, PitchPointShape.io),
                new(1000, 0, PitchPointShape.io),
            };
            note.vibrato.length = 50;
            note.vibrato.@in = 0;
            note.vibrato.@out = 0;
            note.vibrato.shift = 0;
            note.vibrato.drift = 0;
            note.vibrato.rateCurve = new List<VibratoPoint> { new(0, 2), new(1, 6) };
            note.vibrato.depthCurve = new List<VibratoPoint> { new(0, 20), new(1, 60) };
            part.notes.Add(note);
            project.parts.Add(part);
            project.timeAxis.BuildSegments(project);
            note.RefreshPitch(project, project.tracks[0]);
            note.PositionMs = 0;
            note.EndMs = project.timeAxis.TickPosToMsPos(note.End);
            return (project, part, note);
        }

        [Fact]
        public void NativeVersion4RoundTripKeepsEditableCurves() {
            var (project, _, note) = Create();
            string text = XenProjectFormat.Serialize(project);
            Assert.Contains("version: 4", text);
            var read = XenProjectFormat.Parse(text);
            var actual = Assert.Single(Assert.IsType<UVoicePart>(Assert.Single(read.parts)).notes);
            Assert.Equal(note.vibrato.length, actual.vibrato.length);
            Assert.Equal(new[] { (0f, 2f), (1f, 6f) }, actual.vibrato.rateCurve!.Select(point => (point.x, point.y)));
            Assert.Equal(new[] { (0f, 20f), (1f, 60f) }, actual.vibrato.depthCurve!.Select(point => (point.x, point.y)));
        }

        [Fact]
        public void Version3WithoutCurvesRetainsLegacyVibrato() {
            var (project, _, note) = Create();
            note.vibrato.rateCurve = null;
            note.vibrato.depthCurve = null;
            note.vibrato.period = 175;
            note.vibrato.depth = 37;
            string oldText = XenProjectFormat.Serialize(project).Replace("version: 4", "version: 3");
            var read = XenProjectFormat.Parse(oldText);
            var actual = Assert.Single(Assert.IsType<UVoicePart>(Assert.Single(read.parts)).notes);
            Assert.False(actual.vibrato.HasCurves);
            Assert.Equal(175, actual.vibrato.period);
            Assert.Equal(37, actual.vibrato.depth);
            Assert.Equal(50, actual.vibrato.length);
        }

        [Fact]
        public void BakingDoesNotReplaceOrMutateSourceCurves() {
            var (project, part, note) = Create();
            var rate = note.vibrato.rateCurve;
            var depth = note.vibrato.depthCurve;
            var pitch = note.pitch;
            var pitchPoints = pitch.data.Select(point => (point.X, point.Y, point.shape)).ToArray();
            var baked = VibratoCompatibility.Bake(project, part, note, pitch);
            Assert.Same(rate, note.vibrato.rateCurve);
            Assert.Same(depth, note.vibrato.depthCurve);
            Assert.Same(pitch, note.pitch);
            Assert.Equal(pitchPoints, note.pitch.data.Select(point => (point.X, point.Y, point.shape)));
            Assert.True(note.vibrato.HasCurves);
            Assert.NotSame(note.pitch, baked);
        }

        [Theory]
        [InlineData(PitchPointShape.io)]
        [InlineData(PitchPointShape.sp)]
        public void BakingKeepsCurvedPortamentoBeforeVibrato(PitchPointShape shape) {
            var (project, part, note) = Create();
            note.pitch.data[0].shape = shape;
            var baked = VibratoCompatibility.Bake(project, part, note, note.pitch);
            double expected = shape == PitchPointShape.sp
                ? new CubicSplineSegment(-100, 0, -100, 0, 0, 4, 1000, 0).GetY(-75)
                : MusicMath.InterpolateShape(-100, 0, 0, 4, -75, shape);
            Assert.InRange(Math.Abs(SampleLinear(baked, -75) - expected), 0, 0.005);
        }

        [Fact]
        public void BakingUsesTempoMapAndDoesNotDoubleApplyVibrato() {
            var (project, part, note) = Create();
            project.tempos.Add(new UTempo(480, 60));
            project.timeAxis.BuildSegments(project);
            note.EndMs = project.timeAxis.TickPosToMsPos(note.End);
            note.pitch.data.Clear();
            var baked = VibratoCompatibility.Bake(project, part, note, note.pitch);
            const double milliseconds = 725;
            double tick = project.timeAxis.MsPosToNonExactTickPos(milliseconds);
            double expected = (note.vibrato.EvaluateAtTick(project, part, note, tick).Y - note.AdjustedTone) * 10;
            Assert.InRange(Math.Abs(SampleLinear(baked, milliseconds) - expected), 0, 0.01);
            var ust = new UstNote(project, project.tracks[0], part, note);
            Assert.False(ust.vibrato.HasCurves);
            Assert.Equal(0, ust.vibrato.length);
            double baseOffset = (note.AdjustedTone - note.tone) * 10;
            Assert.InRange(Math.Abs(SampleLinear(ust.pitch, milliseconds) - expected - baseOffset), 0, 0.01);
        }

        [Fact]
        public void CompatibleUstxBakesOnlyTheCopy() {
            var (project, _, note) = Create();
            string path = Path.Combine(Path.GetTempPath(), "vibrato-" + Guid.NewGuid().ToString("N") + ".ustx");
            try {
                XenProjectFormat.ExportUstx(path, project);
                var read = Ustx.Load(path);
                var actual = Assert.Single(Assert.IsType<UVoicePart>(Assert.Single(read.parts)).notes);
                Assert.False(actual.vibrato.HasCurves);
                Assert.Equal(0, actual.vibrato.length);
                Assert.True(actual.pitch.data.Count > 100);
                Assert.True(note.vibrato.HasCurves);
                Assert.Equal(50, note.vibrato.length);
                Assert.Equal(3, note.pitch.data.Count);
                Assert.Equal(1, XenProjectFormat.GetCompatibilityReport(project).BakedVibratoNotes);
            } finally {
                File.Delete(path);
            }
        }

        [Fact]
        public void UnchangedUtauPluginKeepsEditableCurvesAndOriginalPitch() {
            var (project, part, note) = Create();
            var actual = ParsePlugin(project, part, note, "Lyric=next\nLength=1200");
            Assert.Equal("next", actual.lyric);
            Assert.Equal(1200, actual.duration);
            Assert.True(actual.vibrato.HasCurves);
            Assert.Equal(note.vibrato.rateCurve!.Select(point => (point.x, point.y)),
                actual.vibrato.rateCurve!.Select(point => (point.x, point.y)));
            Assert.Equal(note.pitch.data.Select(point => (point.X, point.Y, point.shape)),
                actual.pitch.data.Select(point => (point.X, point.Y, point.shape)));
            Assert.Equal(note.AdjustedTone, actual.AdjustedTone);
        }

        [Fact]
        public void EditedUtauPitchKeepsBakedCurveAndDisablesDuplicateVibrato() {
            var (project, part, note) = Create();
            var exported = new UstNote(project, project.tracks[0], part, note);
            var first = exported.pitch.data[0];
            string fields = "PBS=" + first.X.ToString(CultureInfo.InvariantCulture) + ";"
                + (first.Y + 1).ToString(CultureInfo.InvariantCulture);
            var actual = ParsePlugin(project, part, note, fields);
            Assert.False(actual.vibrato.HasCurves);
            Assert.Equal(0, actual.vibrato.length);
            Assert.True(actual.pitch.data.Count > 100);
            Assert.True(note.vibrato.HasCurves);
            Assert.Equal(3, note.pitch.data.Count);
        }

        static UNote ParsePlugin(UProject project, UVoicePart part, UNote note, string fields) {
            string path = Path.GetTempFileName();
            try {
                File.WriteAllText(path, "[#0000]\n" + fields + "\n[#TRACKEND]\n", new UTF8Encoding(false));
                var (removed, added) = Ust.ParsePlugin(project, part, note, note,
                    new List<UNote> { note }, path, "utf-8");
                Assert.Same(note, Assert.Single(removed));
                return Assert.Single(added);
            } finally {
                File.Delete(path);
            }
        }

        static double SampleLinear(UPitch pitch, double milliseconds) {
            var points = pitch.data;
            if (milliseconds <= points[0].X) return points[0].Y;
            if (milliseconds >= points[^1].X) return points[^1].Y;
            int i = 0;
            while (points[i + 1].X < milliseconds) ++i;
            var left = points[i];
            var right = points[i + 1];
            return left.Y + (right.Y - left.Y) * (milliseconds - left.X) / (right.X - left.X);
        }
    }
}
