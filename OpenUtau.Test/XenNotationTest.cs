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
            var naturals = Enumerable.Range(0, 31).Where(d => PitchNotation.Role(tuning, d) == KeyRole.White);
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
            Assert.Equal(KeyRole.Black, PitchNotation.Role(tuning, 2));
            Assert.Equal(KeyRole.Between, PitchNotation.Role(tuning, 1));
            Assert.Equal(KeyRole.White, PitchNotation.Role(tuning, 18));
            Assert.Empty(tuning.Notations);
        }

        [Fact]
        public void DegreeLabelsUseIndexWithinPeriod() {
            var tuning = TuningPresets.EqualDivision(13, PitchInterval.FromRatio("5/2"));
            // Without a family keyboard the default MOS names the notes; degree numbers remain the secondary label.
            Assert.Equal(PitchNotation.Mos, PitchNotation.ResolvePrimary(tuning, PitchNotation.Auto));
            Assert.Equal("degree", PitchNotation.ResolveSecondary(tuning, PitchNotation.Mos, PitchNotation.Auto));
            Assert.Null(PitchNotation.ResolveSecondary(tuning, "degree", PitchNotation.Auto));
            Assert.Equal("5·5", PitchNotation.Format(tuning, 18, "degree"));
            Assert.Equal("5", PitchNotation.Format(tuning, 18, "degree", false));
            Assert.Equal("12·3", PitchNotation.Format(tuning, -1, "degree"));
        }

        [Fact]
        public void BohlenPierceUsesLambdaNames() {
            var bp = TuningPresets.All.Single(t => t.Id == "bohlen-pierce-13edt");
            Assert.Equal("bp", PitchNotation.ResolvePrimary(bp, PitchNotation.Auto));
            var names = Enumerable.Range(0, 13).Select(d => PitchNotation.Format(bp, d, "bp", false)).ToArray();
            Assert.Equal(new[] { "C", "C♯", "D", "E", "F", "F♯", "G", "H", "H♯", "J", "A", "A♯", "B" }, names);
            Assert.True(PitchNameInput.TryParse(bp, "J♭4", 0, out int jFlat), "J♭4");
            Assert.Equal(8, jFlat);
            Assert.Equal("H4", PitchNameInput.Format(bp, 7));
            var edt = TuningPresets.EqualDivision(31, PitchInterval.FromRatio("3/1"));
            Assert.Equal("bp", PitchNotation.ResolvePrimary(edt, PitchNotation.Auto));
            for (int degree = -31; degree <= 62; degree++) {
                string text = PitchNameInput.Format(edt, degree);
                Assert.True(PitchNameInput.TryParse(edt, text, 0, out int parsed), text);
                Assert.Equal(degree, parsed);
            }
        }

        [Theory]
        [InlineData(440)]
        [InlineData(415)]
        public void TwelveNotePresetsColourKeysByDegree(double frequency) {
            foreach (var tuning in TuningPresets.All.Where(t => t.DegreeCount == 12)) {
                tuning.Reference.Frequency = frequency;
                var accidentals = Enumerable.Range(0, 12).Where(d => PitchNotation.Role(tuning, d) == KeyRole.Black).ToArray();
                Assert.True(new[] { 1, 3, 6, 8, 10 }.SequenceEqual(accidentals), tuning.Id);
                Assert.NotEqual("degree", PitchNotation.ResolvePrimary(tuning, PitchNotation.Auto));
            }
        }
    }
}
