using System;
using System.Linq;
using OpenUtau.Core.Microtonal;
using Xunit;

namespace OpenUtau.Test {
    public class MosKeyboardTest {
        [Theory]
        [InlineData(12, 7, 7)]
        [InlineData(22, 13, 7)]
        [InlineData(31, 18, 7)]
        [InlineData(41, 24, 7)]
        [InlineData(53, 31, 7)]
        [InlineData(73, 43, 7)]
        [InlineData(25, 7, 7)]
        [InlineData(13, 3, 9)]
        [InlineData(31, 7, 9)]
        [InlineData(53, 12, 9)]
        [InlineData(73, 17, 9)]
        [InlineData(19, 7, 5)]
        [InlineData(19, 7, 8)]
        public void GeneralOrbitCoversEveryDegreeAndProtectsExactAnchors(int n, int g, int m) {
            var model = MosKeyboard.Generate(n, g, m);
            Assert.Equal(Enumerable.Range(0, n), model.Cells.Select(cell => cell.Step));
            Assert.Equal(m, model.Nominals.Count);
            Assert.Equal(n, model.Gaps.Sum());
            Assert.InRange(model.Gaps.Distinct().Count(), 1, 2);
            Assert.All(model.Nominals, step => {
                Assert.Equal(KeyRole.White, model.Cells[step].Role);
                Assert.True(model.Cells[step].IsAnchor);
            });
            var layout = NotchedKeyboardLayout.Create(model.Cells.Select(cell => new NotchedKeyStep(cell.Step, cell.Kind, cell.Owner)));
            Assert.Equal(n, layout.Keys.Count);
            Assert.All(layout.Columns, column => Assert.InRange(column.Degrees.Count, 1, 5));
        }

        [Fact]
        public void CentralThirdBoundariesAreBetweenAndBlackOwnersStaySeparate() {
            var model = MosKeyboard.Generate(73, 43, 7, 4);
            var kinds = model.Cells.Take(14).Select(cell => cell.Kind).ToArray();
            Assert.Equal(new[] {
                NotchedKeyKind.Natural, NotchedKeyKind.Natural, NotchedKeyKind.Between,
                NotchedKeyKind.Flat, NotchedKeyKind.Flat, NotchedKeyKind.Flat,
                NotchedKeyKind.Between, NotchedKeyKind.Between,
                NotchedKeyKind.Sharp, NotchedKeyKind.Sharp, NotchedKeyKind.Sharp,
                NotchedKeyKind.Between, NotchedKeyKind.Natural, NotchedKeyKind.Natural,
            }, kinds);
            Assert.Equal(4, model.Cells[3].AnchorStep);
            Assert.False(model.Cells[3].IsAnchor);
            Assert.Equal(9, model.Cells[10].AnchorStep);
            // N=72 has gaps of six between nominal and shared accidental anchors.
            var thirds = MosKeyboard.Generate(72, 42, 7, 4);
            Assert.Equal(KeyRole.Between, thirds.Cells[2].Role);
            Assert.Equal(KeyRole.Between, thirds.Cells[4].Role);
        }

        [Fact]
        public void NaturalToNaturalInteriorsNeverBecomeWhiteIncludingThePeriodBoundary() {
            var model = MosKeyboard.Generate(73, 43, 7, 4);
            Assert.All(model.Cells.Skip(26).Take(5).Where(cell => !cell.IsAnchor), cell => Assert.Equal(KeyRole.Between, cell.Role));
            Assert.All(model.Cells.Skip(70), cell => Assert.Equal(KeyRole.Between, cell.Role));
        }

        [Fact]
        public void EqualStepAndEnharmonicCoincidenceAreDistinctCases() {
            var equal = MosKeyboard.Generate(12, 2, 6);
            Assert.True(equal.IsEqualStep);
            Assert.DoesNotContain(equal.Cells, cell => cell.Role == KeyRole.Black);
            var piano = MosKeyboard.Generate(12, 7, 7, 4);
            Assert.Equal(NotchedKeyKind.Shared, piano.Cells[1].Kind);
            Assert.Equal(KeyRole.Black, piano.Cells[1].Role);
        }

        [Fact]
        public void RotationsAndNegativeGeneratorsUseCyclicGapOrder() {
            var model = MosKeyboard.Generate(31, 18, 7, 4);
            Assert.Equal(new[] { 0, 5, 10, 13, 18, 23, 28 }, model.Nominals);
            Assert.Equal(model.Nominals, MosKeyboard.Generate(31, -13, 7, -3).Nominals);
            var first = MosKeyboard.Generate(31, 18, 7);
            Assert.Equal(first.Gaps.Skip(4).Concat(first.Gaps.Take(4)), model.Gaps);
        }

        [Fact]
        public void InvalidOrRepeatingGeneratorConfigurationsDoNotBecomeMosKeyboards() {
            Assert.Throws<ArgumentException>(() => MosKeyboard.Generate(12, 2, 7));
            Assert.Throws<ArgumentException>(() => MosKeyboard.Generate(12, 5, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => MosKeyboard.Generate(0, 7, 7));
            Assert.Throws<ArgumentOutOfRangeException>(() => MosKeyboard.Generate(12, 7, 13));
        }

        [Fact]
        public void TheKeyboardMosOwnsTheKeysWhileTheScaleOnlyMarksNotes() {
            var tuning = TuningPresets.EqualDivision(73);
            var baseline = KeyboardRegionizer.Model(tuning);
            var result = MosGenerator.Generate(tuning, 17, 9);
            tuning.Scales.Add(result.Scale);
            tuning.Keyboard.ActiveScaleId = result.Scale.Id;
            Assert.Same(baseline, KeyboardRegionizer.Model(tuning));
            tuning.Keyboard.Mos = new KeyboardMos { Generator = 17, Cardinality = 9 };
            var letters = KeyboardRegionizer.Model(tuning);
            Assert.NotSame(baseline, letters);
            Assert.Equal(result.Scale.Degrees, letters!.Nominals);
            tuning.Keyboard.Classes[6] = "accidental";
            Assert.Equal(letters.Cells[6].Role, PitchNotation.Role(tuning, 6));
            tuning.Keyboard.Mos = null;
            Assert.Same(baseline, KeyboardRegionizer.Model(tuning));
            Assert.Equal(KeyRole.Between, PitchNotation.Role(tuning, 6));
        }
    }
}
