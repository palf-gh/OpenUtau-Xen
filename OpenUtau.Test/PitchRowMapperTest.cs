using System;
using System.Linq;
using OpenUtau.Core.Microtonal;
using Xunit;

namespace OpenUtau.Test {
    public sealed class PitchRowMapperTest {
        [Fact]
        public void TwelveToneRowsRetainExistingCoordinates() {
            var mapper = new PitchRowMapper(TuningPresets.Default);
            Assert.Equal(128, mapper.Count);
            for (int midi = 0; midi <= 127; midi++) {
                Assert.Equal(midi, mapper.MidiAtRow(midi), 9);
                Assert.Equal(midi, mapper.RowAtMidi(midi), 9);
            }
        }
        [Fact]
        public void NonUniformRowsInterpolatePhysicalPitchAndInvertExactly() {
            var tuning = TuningPresets.All.Single(t => t.Id == "ji-5-limit");
            var mapper = new PitchRowMapper(tuning);
            for (double row = -1.25; row < mapper.Count + 1; row += 0.137) {
                Assert.Equal(row, mapper.RowAtMidi(mapper.MidiAtRow(row)), 9);
            }
            Assert.NotEqual(mapper.MidiAtRow(60) - mapper.MidiAtRow(59),
                mapper.MidiAtRow(61) - mapper.MidiAtRow(60));
        }
        [Fact]
        public void CompactRowsKeepSupplementaryNotesAndEveryVisiblePitchUnchanged() {
            var tuning = TuningPresets.EqualDivision(53);
            int[] scale = { 0, 9, 18, 22, 31, 40, 49 };
            var full = new PitchRowMapper(tuning);
            var compact = new PitchRowMapper(tuning, PitchDisplayMode.Compact, scale);
            var withOffScale = new PitchRowMapper(tuning, PitchDisplayMode.Compact, scale, new[] { 1 });
            Assert.True(compact.Count < full.Count / 3);
            Assert.Equal(compact.Count + 1, withOffScale.Count);
            double hidden = PitchResolver.ResolveMidi(tuning, new PitchAddress { Degree = 1 });
            int row = (int)Math.Round(withOffScale.RowAtMidi(hidden));
            Assert.Equal(1, withOffScale.DegreeAtRow(row));
            Assert.False(withOffScale.IsScaleRow(row));
            for (int i = 0; i < withOffScale.Count; i++) {
                double physical = PitchResolver.ResolveMidi(tuning, new PitchAddress { Degree = withOffScale.DegreeAtRow(i) });
                Assert.Equal(physical, withOffScale.MidiAtRow(i), 10);
                Assert.Equal(physical, full.MidiAtRow(full.RowAtMidi(physical)), 10);
            }
        }
        [Fact]
        public void RowSnapshotDoesNotMoveDuringAGesture() {
            var tuning = TuningPresets.EqualDivision(53);
            var mapper = new PitchRowMapper(tuning);
            double before = mapper.MidiAtRow(100);
            tuning.Reference.Frequency = 432;
            Assert.Equal(before, mapper.MidiAtRow(100));
        }
        [Fact]
        public void EmptyCompactSubsetIsRejected() {
            Assert.Throws<ArgumentException>(() => new PitchRowMapper(TuningPresets.Default,
                PitchDisplayMode.Compact, Array.Empty<int>()));
        }
    }
}
