using System.Linq;
using OpenUtau.Core.Microtonal;
using Xunit;

namespace OpenUtau.Test {
    public class MosChoiceTest {
        [Theory]
        [InlineData(12)]
        [InlineData(15)]
        [InlineData(22)]
        [InlineData(24)]
        [InlineData(31)]
        [InlineData(53)]
        public void EveryChoiceGeneratesItsMos(int divisions) {
            var tuning = TuningPresets.EqualDivision(divisions);
            var choices = MosGenerator.Choices(divisions);
            Assert.NotEmpty(choices);
            foreach (var choice in choices) {
                var result = MosGenerator.Generate(tuning, choice.Mos.Generator, choice.Mos.Cardinality, 0, choice.Periods);
                Assert.True(result.IsMos);
                Assert.Equal(choice.LargeCount, result.LargeCount);
                Assert.Equal(choice.SmallCount, result.SmallCount);
                Assert.InRange(choice.Mos.Cardinality, 4, 24);
            }
            // A generator and its inversion are listed once.
            Assert.Equal(choices.Count, choices.Select(c => (c.Mos.Generator, c.Mos.Cardinality, c.Periods)).Distinct().Count());
            Assert.All(choices, c => Assert.True(2 * c.Mos.Generator <= divisions / c.Periods));
            Assert.Equal(choices.OrderBy(c => c.Mos.Cardinality).Select(c => c.Mos.Cardinality), choices.Select(c => c.Mos.Cardinality));
        }

        [Fact]
        public void TwelveEdoListsDiatonicAndDiminished() {
            var choices = MosGenerator.Choices(12);
            var diatonic = Assert.Single(choices, c => c.Mos.Cardinality == 7 && c.LargeCount == 5);
            Assert.Equal((5, 2), (diatonic.LargeCount, diatonic.SmallCount));
            Assert.True(diatonic.Matches(new KeyboardMos { Generator = 7, Cardinality = 7 }, 12));
            Assert.False(diatonic.Matches(new KeyboardMos { Generator = 7, Cardinality = 5 }, 12));
            var diminished = Assert.Single(choices, c => c.Mos.Cardinality == 8 && c.Periods == 4);
            Assert.Equal((4, 4), (diminished.LargeCount, diminished.SmallCount));
            Assert.True(diminished.Matches(new KeyboardMos { Generator = 2, Cardinality = 8, Periods = 4 }, 12));
            Assert.False(diminished.Matches(new KeyboardMos { Generator = 1, Cardinality = 8 }, 12));
            // Eight fourths give two step sizes (sLLsssLL) but three sizes of third, so they are not a MOS.
            Assert.DoesNotContain(choices, c => c.Mos.Generator == 5 && c.Mos.Cardinality == 8);
            Assert.DoesNotContain(choices, c => c.Mos.Generator == 1 && c.Periods == 1);
            Assert.Equal(13, choices.Count);
        }

        [Fact]
        public void GeneratorsSharingAFactorWithThePeriodAreListed() {
            // 24-EDO's fifth is 14 steps, so the chain stays in the 12-note cycle; seven of them are still 5L2s.
            var diatonic = Assert.Single(MosGenerator.Choices(24), c => c.Mos.Cardinality == 7 && c.LargeCount == 5);
            Assert.Equal(10, diatonic.Mos.Generator);
            Assert.True(diatonic.Matches(new KeyboardMos { Generator = 14, Cardinality = 7 }, 24));
            Assert.True(diatonic.Matches(KeyboardRegionizer.DefaultMos(TuningPresets.EqualDivision(24)), 24));
        }

        [Fact]
        public void ThirtyOneEdoHasFourProperSevenNoteMos() {
            var seven = MosGenerator.Choices(31).Where(c => c.Mos.Cardinality == 7).ToArray();
            Assert.Equal(new[] { 4, 5, 9, 13 }, seven.Where(c => c.IsProper).Select(c => c.Mos.Generator));
            Assert.Equal(new[] { 2, 3, 8, 10, 14, 15 }, seven.Where(c => !c.IsProper).Select(c => c.Mos.Generator));
        }

        [Fact]
        public void ProprietyAllowsEqualSizesButNotCrossing() {
            // The 12-EDO tritone is both a fourth and a fifth; the 53-EDO augmented fourth exceeds the diminished fifth.
            Assert.True(MosGenerator.IsProper(new[] { 2, 2, 1, 2, 2, 2, 1 }));
            Assert.False(MosGenerator.IsProper(new[] { 9, 9, 4, 9, 9, 9, 4 }));
            Assert.True(MosGenerator.IsProper(new[] { 2, 1, 2, 1, 2, 1, 2, 1 }));
            Assert.True(MosGenerator.IsProper(Enumerable.Repeat(1, 4096).ToArray()));
        }

        [Fact]
        public void MultiPeriodExamplesAreListed() {
            Assert.Contains(MosGenerator.Choices(15), c => c.Periods == 5 && (c.LargeCount, c.SmallCount) == (5, 5));
            Assert.Contains(MosGenerator.Choices(22), c => c.Periods == 2 && c.Mos.Generator == 2 && (c.LargeCount, c.SmallCount) == (2, 8));
        }

        [Fact]
        public void LimitsAndLargeTuningsAreRespected() {
            Assert.All(MosGenerator.Choices(72, 5, 9), c => Assert.InRange(c.Mos.Cardinality, 5, 9));
            Assert.Empty(MosGenerator.Choices(5000));
            Assert.Empty(MosGenerator.Choices(1));
        }
    }
}
