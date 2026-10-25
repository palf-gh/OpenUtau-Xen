using System.Linq;
using OpenUtau.Core.Microtonal;
using Xunit;

namespace OpenUtau.Test {
    /// <summary>Baseline cases for the current central-third MOS keyboard heuristic.</summary>
    public class XenKeyboardTest {
        static KeyRole[] Octave(int n) => KeyboardRegionizer.Regionize(KeyboardTheory.Octave(n));
        static KeyRole[] Tritave(int n) => KeyboardRegionizer.Regionize(KeyboardTheory.Tritave(n));
        static (int White, int Black, int Between) Count(KeyRole[] roles) => (
            roles.Count(r => r == KeyRole.White), roles.Count(r => r == KeyRole.Black), roles.Count(r => r == KeyRole.Between));
        const KeyRole W = KeyRole.White, K = KeyRole.Black, B = KeyRole.Between;

        [Fact]
        public void TwelveIsAConventionalPiano() {
            Assert.Equal(new[] { W, K, W, K, W, W, K, W, K, W, K, W }, Octave(12));
        }

        [Fact]
        public void TwentyTwoKeepsBetweenInsideTheBlackCluster() {
            var theory = KeyboardTheory.Octave(22);
            Assert.Equal((13, 4, 1, 3), (theory.Generator, theory.Large, theory.Small, theory.Chromatic));
            Assert.Equal(new[] { W, K, B, K, W }, Octave(22).Take(5));
        }

        [Fact]
        public void TwentyFourAndThirtyOneFollowTheThreeTierModel() {
            Assert.Equal((7, 5, 12), Count(Octave(24)));
            var roles = Octave(31);
            Assert.Equal((7, 10, 14), Count(roles));
            Assert.Equal(new[] { W, B, K, K, B, W }, roles.Take(6));
        }

        [Fact]
        public void FortyOneLeavesEToFAsBetween() {
            var roles = Octave(41);
            Assert.Equal(new[] { W, B, B, K, K, B, B, W }, roles.Take(8));
            var theory = KeyboardTheory.Octave(41);
            int e = theory.Naturals[2], f = theory.Naturals[3];
            Assert.Equal(new[] { W, B, B, W }, roles.Skip(e).Take(f - e + 1));
        }

        [Fact]
        public void FiftyThreePutsDFlatBelowCSharp() {
            var theory = KeyboardTheory.Octave(53);
            Assert.Equal(4, theory.Small);
            Assert.Equal(5, theory.Chromatic);
            var roles = Octave(53);
            Assert.Equal(K, roles[4]);
            Assert.Equal(K, roles[5]);
            Assert.Equal(new[] { W, W, B, K, K, K, K, B, W, W }, roles.Take(10));
            Assert.Equal((17, 20, 16), Count(roles));
        }

        [Fact]
        public void SeventyTwoPartitionsAnchorIntervalsButLeavesNaturalGapsBetween() {
            var roles = Octave(72);
            Assert.Equal(new[] { W, W, B, B, B, K, K, K, B, B, B, W, W }, roles.Take(13));
            Assert.Equal(B, roles[71]);
        }

        [Fact]
        public void SeventyThreeKeepsBetweenInsideTheAccidentalCluster() {
            Assert.Equal(new[] { W, W, B, K, K, K, B, B, K, K, K, B, W, W }, Octave(73).Take(14));
        }

        [Fact]
        public void ThirteenEdtIsBohlenPierceLambda() {
            var theory = KeyboardTheory.Tritave(13);
            Assert.Equal((3, 2, 1), (theory.Generator, theory.Large, theory.Small));
            Assert.Equal(new[] { 0, 2, 3, 4, 6, 7, 9, 10, 12 }, theory.Naturals);
            Assert.Equal((9, 4, 0), Count(Tritave(13)));
        }

        [Theory]
        [InlineData(31, 7, 4, 3)]
        [InlineData(53, 12, 7, 5)]
        [InlineData(73, 17, 12, 5)]
        public void LargerEdtsRecomputeTheLambdaStructure(int n, int g, int large, int small) {
            var theory = KeyboardTheory.Tritave(n);
            Assert.Equal((g, large, small), (theory.Generator, theory.Large, theory.Small));
            Assert.Equal(n, 4 * large + 5 * small);
            var roles = Tritave(n);
            Assert.All(theory.Naturals, step => Assert.Equal(W, roles[step]));
            foreach (int gap in theory.LargeGaps) {
                int x = theory.Naturals[gap];
                Assert.Equal(K, roles[x + theory.Chromatic]);
                Assert.Equal(K, roles[x + theory.Small]);
            }

        }

        [Fact]
        public void InvalidTheoriesFallBackAndExplicitClassesWin() {
            Assert.False(KeyboardTheory.Octave(7).IsValid);
            Assert.Null(KeyboardRegionizer.Roles(TuningPresets.EqualDivision(7)));
            var tuning = TuningPresets.EqualDivision(31);
            tuning.Keyboard.Classes[1] = "accidental";
            Assert.Equal(K, PitchNotation.Role(tuning, 1));
            Assert.Equal(B, PitchNotation.Role(tuning, 4));
        }
    }
}
