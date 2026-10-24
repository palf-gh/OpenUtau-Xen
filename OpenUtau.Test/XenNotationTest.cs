using System.Linq;
using OpenUtau.Core.Microtonal;
using Xunit;

namespace OpenUtau.Test {
    public class XenNotationTest {
        [Fact]
        public void FiftyThreeStepNamesFollowNearestNatural() {
            var tuning = TuningPresets.EqualDivision(53);
            var expected = new[] { "C4", "C4+1", "C4+2", "C4+3", "C4+4", "D4-4", "D4-3", "D4-2", "D4-1", "D4" };
            Assert.Equal(expected, Enumerable.Range(0, 10).Select(d => PitchNameInput.Format(tuning, d)));
            Assert.Equal("C5", PitchNameInput.Format(tuning, 53));
            Assert.Equal("B3", PitchNameInput.Format(tuning, -4));
        }

        [Theory]
        [InlineData(12)]
        [InlineData(17)]
        [InlineData(19)]
        [InlineData(22)]
        [InlineData(24)]
        [InlineData(31)]
        [InlineData(41)]
        [InlineData(53)]
        [InlineData(72)]
        public void FormattedNamesParseBackToTheSameDegree(int divisions) {
            var tuning = TuningPresets.EqualDivision(divisions);
            for (int degree = -2 * divisions; degree <= 3 * divisions; degree++) {
                string text = PitchNameInput.Format(tuning, degree);
                Assert.True(PitchNameInput.TryParse(tuning, text, 0, out int parsed), text);
                Assert.Equal(degree, parsed);
            }
        }

        [Fact]
        public void SpelledAndRelativeInputs() {
            var twelve = TuningPresets.EqualDivision(12);
            Assert.Equal("C♯4", PitchNameInput.Format(twelve, 1));
            foreach (string text in new[] { "C#4", "C♯4", "Db4", "D♭4" }) {
                Assert.True(PitchNameInput.TryParse(twelve, text, 0, out int degree), text);
                Assert.Equal(1, degree);
            }
            Assert.True(PitchNameInput.TryParse(twelve, "+3", 5, out int up));
            Assert.Equal(8, up);
            Assert.True(PitchNameInput.TryParse(twelve, "-14", 5, out int down));
            Assert.Equal(-9, down);
            Assert.False(PitchNameInput.TryParse(twelve, "H4", 0, out _));
            Assert.False(PitchNameInput.TryParse(twelve, "", 0, out _));

            var thirtyOne = TuningPresets.EqualDivision(31);
            Assert.True(PitchNameInput.TryParse(thirtyOne, "F♭4", 0, out int fFlat));
            Assert.Equal(11, fFlat);
            Assert.True(PitchNameInput.TryParse(thirtyOne, "E#4", 0, out int eSharp));
            Assert.Equal(12, eSharp);
            Assert.True(PitchNameInput.TryParse(thirtyOne, "Cb5", 0, out int cFlat));
            Assert.Equal(29, cFlat);
            Assert.True(PitchNameInput.TryParse(thirtyOne, "B#4", 0, out int bSharp));
            Assert.Equal(30, bSharp);
            // Without an octave the name nearest the current degree is chosen.
            Assert.True(PitchNameInput.TryParse(thirtyOne, "D", 30, out int nearestD));
            Assert.Equal(36, nearestD);
        }

        [Fact]
        public void ThirtyOneUsesHalfAccidentals() {
            var tuning = TuningPresets.EqualDivision(31);
            Assert.Equal("szg", PitchNotation.ResolvePrimary(tuning, PitchNotation.Auto));
            Assert.Equal("C" + PitchNotation.AccidentalGlyph("accidentalQuarterToneSharpStein") + "4", PitchNotation.Format(tuning, 1, "szg"));
            Assert.Equal("C♯4", PitchNotation.Format(tuning, 2, "szg"));
            Assert.Equal("C" + PitchNotation.AccidentalGlyph("accidentalQuarterToneFlatStein") + "5", PitchNotation.Format(tuning, 30, "szg"));
            var naturals = Enumerable.Range(0, 31).Where(d => !PitchNotation.IsAccidental(tuning, d));
            Assert.Equal(new[] { 0, 5, 10, 13, 18, 23, 28 }, naturals);
        }

        [Fact]
        public void SnapshotsWithoutNamesStillResolveReadableLabels() {
            var tuning = TuningPresets.EqualDivision(31);
            tuning.Notations.Clear();
            tuning.Keyboard.PrimaryNotation = "degree";
            tuning.Keyboard.SecondaryNotation = null;
            string primary = PitchNotation.ResolvePrimary(tuning, PitchNotation.Auto);
            Assert.Equal("szg", primary);
            Assert.Equal("degree", PitchNotation.ResolveSecondary(tuning, primary, PitchNotation.Auto));
            Assert.Null(PitchNotation.ResolveSecondary(tuning, primary, PitchNotation.None));
            Assert.Equal("ups-downs", PitchNotation.ResolvePrimary(tuning, "ups-downs"));
            Assert.True(PitchNotation.IsAccidental(tuning, 1));
            Assert.False(PitchNotation.IsAccidental(tuning, 18));
            Assert.Empty(tuning.Notations);
        }

        [Fact]
        public void DegreeLabelsUseIndexWithinPeriod() {
            var bp = TuningPresets.All.Single(t => t.Id == "bohlen-pierce-13edt");
            Assert.Equal("degree", PitchNotation.ResolvePrimary(bp, PitchNotation.Auto));
            Assert.Null(PitchNotation.ResolveSecondary(bp, "degree", PitchNotation.Auto));
            Assert.Equal("5·5", PitchNotation.Format(bp, 18, "degree"));
            Assert.Equal("5", PitchNotation.Format(bp, 18, "degree", false));
            Assert.Equal("12·3", PitchNotation.Format(bp, -1, "degree"));
        }

        [Theory]
        [InlineData(440)]
        [InlineData(415)]
        public void TwelveNotePresetsColourKeysByDegree(double frequency) {
            foreach (var tuning in TuningPresets.All.Where(t => t.DegreeCount == 12)) {
                tuning.Reference.Frequency = frequency;
                var accidentals = Enumerable.Range(0, 12).Where(d => PitchNotation.IsAccidental(tuning, d)).ToArray();
                Assert.True(new[] { 1, 3, 6, 8, 10 }.SequenceEqual(accidentals), tuning.Id);
                Assert.NotEqual("degree", PitchNotation.ResolvePrimary(tuning, PitchNotation.Auto));
            }
        }
    }
}
