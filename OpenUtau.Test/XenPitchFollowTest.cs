using System.Linq;
using OpenUtau.App.ViewModels;
using OpenUtau.Core.Format;
using OpenUtau.Core.Microtonal;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Test {
    public sealed class XenPitchFollowTest {
        static (UProject project, UVoicePart part) Part(TuningDefinition tuning, params int[] degrees) {
            var project = Ustx.Create();
            project.pitchSystem = tuning.Clone();
            var part = new UVoicePart { trackNo = 0, Duration = 480 * (degrees.Length + 1) };
            for (int i = 0; i < degrees.Length; i++) {
                var note = project.CreateNote(60, i * 480, 480);
                note.pitchAddress = new PitchAddress { Degree = degrees[i] };
                note.RefreshPitch(project, project.tracks[0]);
                part.notes.Add(note);
            }
            project.parts.Add(part);
            return (project, part);
        }

        [Fact]
        public void TwelveToneOffsetKeepsUpstreamRows() {
            var mapper = new PitchRowMapper(TuningPresets.Default);
            Assert.Equal(127 - 60 + 0.5 - 10, PlaybackPitchFollowPath.ToneToTrackOffset(60, mapper, 20, 1000, 0.5), 9);
        }

        [Fact]
        public void ThirtyOneEdoStepsBelowThresholdShareAZone() {
            var (_, part) = Part(TuningPresets.EqualDivision(31), 0, 1, 2, 8);
            var zones = PlaybackPitchFollowPath.GroupPitchZones(part.notes, 200);
            Assert.Equal(2, zones.Count);
            var notes = part.notes.OrderBy(n => n.position).ToArray();
            Assert.Equal(notes.Take(3).Average(n => n.ResolvedTone), zones[0].CenterTone, 9);
            Assert.Equal(notes[3].ResolvedTone, zones[1].CenterTone, 9);
            // Each 31-EDO step is about 38.7 cents: a 30-cent threshold separates every note.
            Assert.Equal(4, PlaybackPitchFollowPath.GroupPitchZones(part.notes, 30).Count);
        }

        [Theory]
        [InlineData("31edo")]
        [InlineData("bohlen-pierce-13edt")]
        public void PathTargetsRowsOfTheTuning(string tuningId) {
            var tuning = TuningPresets.All.Single(t => t.Id == tuningId);
            var (project, part) = Part(tuning, 0, 7);
            var mapper = new PitchRowMapper(tuning);
            var path = new PlaybackPitchFollowPath();
            path.Build(part, mapper, 24, mapper.Count, 200, 0.5, project.resolution);
            Assert.True(path.IsBuilt);
            foreach (var note in part.notes) {
                double expected = mapper.Count - 1 - mapper.RowAtMidi(note.ResolvedTone) + 0.5 - 12;
                Assert.Equal(expected, path.Evaluate(note.position + note.duration / 2.0), 6);
            }
        }
    }
}
