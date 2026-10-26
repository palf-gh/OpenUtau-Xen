using System;
using System.Linq;
using OpenUtau.Core.Format;
using OpenUtau.Core.Microtonal;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Test {
    public class MultiPeriodMosTest {
        static TuningDefinition WithMos(int divisions, int generator, int cardinality, int? periods, int mode = 0) {
            var tuning = TuningPresets.EqualDivision(divisions);
            tuning.Keyboard.Mos = new KeyboardMos { Generator = generator, Cardinality = cardinality, Mode = mode, Periods = periods };
            return tuning;
        }

        [Fact]
        public void TwelveEdoDiminishedRepeatsFourTimes() {
            var result = MosGenerator.Generate(TuningPresets.EqualDivision(12), 1, 8, 0, 4);
            Assert.True(result.IsMos);
            Assert.Equal(new[] { 0, 2, 3, 5, 6, 8, 9, 11 }, result.Scale.Degrees);
            Assert.Equal("LsLsLsLs", result.Pattern);
            Assert.Equal("4L4s (mode 1)", result.Scale.Name);
            Assert.Equal("mos-1-8-0-p4", result.Scale.Id);
            var second = MosGenerator.Generate(TuningPresets.EqualDivision(12), 1, 8, 1, 4);
            Assert.Equal(new[] { 0, 1, 3, 4, 6, 7, 9, 10 }, second.Scale.Degrees);
            Assert.Equal("4L4s (mode 2)", second.Scale.Name);
        }

        [Fact]
        public void ModesRotateWithinOneRepeat() {
            var tuning = TuningPresets.EqualDivision(12);
            var wrapped = MosGenerator.Generate(tuning, 1, 8, 2, 4);
            Assert.Equal(0, wrapped.Scale.Mode);
            Assert.Equal(MosGenerator.Generate(tuning, 1, 8, 0, 4).Scale.Degrees, wrapped.Scale.Degrees);
        }

        [Fact]
        public void FifteenEdoBlackwoodIsFiveLargeFiveSmall() {
            var result = MosGenerator.Generate(TuningPresets.EqualDivision(15), 1, 10, 0, 5);
            Assert.True(result.IsMos);
            Assert.Equal(5, result.LargeCount);
            Assert.Equal(5, result.SmallCount);
            Assert.Equal(new[] { 0, 2, 3, 5, 6, 8, 9, 11, 12, 14 }, result.Scale.Degrees);
        }

        [Fact]
        public void TwentyTwoEdoPajaraIsTwoLargeEightSmall() {
            var tuning = TuningPresets.EqualDivision(22);
            var result = MosGenerator.Generate(tuning, 2, 10, 0, 2);
            Assert.Equal("2L8s (mode 1)", result.Scale.Name);
            Assert.Equal(new[] { 0, 2, 4, 6, 9, 11, 13, 15, 17, 20 }, result.Scale.Degrees);
            // The generator is counted inside the half-octave period, so 13 steps are the same as 2.
            Assert.Equal(result.Scale.Degrees, MosGenerator.Generate(tuning, 13, 10, 0, 2).Scale.Degrees);
        }

        [Theory]
        [InlineData(12, 1, 8, 5)]
        [InlineData(12, 1, 7, 4)]
        [InlineData(12, 0, 8, 4)]
        public void RepeatCountsThatDoNotFitAreUnavailable(int divisions, int generator, int cardinality, int periods) {
            var tuning = WithMos(divisions, generator, cardinality, periods);
            Assert.ThrowsAny<ArgumentException>(() => MosGenerator.Generate(tuning, generator, cardinality, 0, periods));
            Assert.Null(KeyboardRegionizer.MosDegrees(tuning, tuning.Keyboard.Mos!));
        }

        [Fact]
        public void DiminishedKeyboardHasSharedBlackKeysAndLetters() {
            var tuning = WithMos(12, 1, 8, 4);
            var model = KeyboardRegionizer.Model(tuning)!;
            Assert.Equal(new[] { 0, 2, 3, 5, 6, 8, 9, 11 }, model.Nominals);
            foreach (int step in new[] { 1, 4, 7, 10 }) Assert.Equal(NotchedKeyKind.Shared, model.Cells[step].Kind);
            Assert.NotNull(KeyboardRegionizer.LetterNominals(tuning));
            Assert.True(PitchNotation.IsAvailable(tuning, PitchNotation.Mos));
            var names = Enumerable.Range(0, 12).Where(d => model.Cells[d].Role == KeyRole.White)
                .Select(d => PitchNotation.Format(tuning, d, PitchNotation.Mos, false)).ToArray();
            Assert.Equal(8, names.Distinct().Count());
        }

        [Fact]
        public void TheRepeatCountChangesTheCachedDegrees() {
            var tuning = TuningPresets.EqualDivision(12);
            var single = KeyboardRegionizer.MosDegrees(tuning, new KeyboardMos { Generator = 1, Cardinality = 8 });
            var repeated = KeyboardRegionizer.MosDegrees(tuning, new KeyboardMos { Generator = 1, Cardinality = 8, Periods = 4 });
            Assert.NotNull(single);
            Assert.NotEqual(single, repeated);
        }

        [Fact]
        public void MultiPeriodMosNeverTakesFamilyNames() {
            var tuning = TuningPresets.EqualDivision(12);
            Assert.True(KeyboardRegionizer.IsFamilyMos(tuning, new KeyboardMos { Generator = 7, Cardinality = 7 }));
            Assert.False(KeyboardRegionizer.IsFamilyMos(tuning, new KeyboardMos { Generator = 7, Cardinality = 7, Periods = 7 }));
        }

        [Fact]
        public void ValidationRejectsNonPositiveRepeats() {
            var tuning = WithMos(12, 1, 8, 0);
            Assert.Throws<FormatException>(() => tuning.Validate());
        }

        [Fact]
        public void TunxenVersion3RoundTripsTheRepeatCount() {
            var tuning = WithMos(12, 1, 8, 4, 1);
            var text = TunxenFormat.Serialize(tuning);
            Assert.Contains("version: 3", text);
            Assert.Contains("periods: 4", text);
            var read = TunxenFormat.Parse(text);
            Assert.Equal(4, read.Keyboard.Mos!.Periods);
            Assert.Equal(1, read.Keyboard.Mos.Mode);
            Assert.Equal(KeyboardRegionizer.Model(tuning)!.Nominals, KeyboardRegionizer.Model(read)!.Nominals);
            // A single-period MOS writes no repeat count, and version 2 files read as one repeat.
            var single = TunxenFormat.Serialize(WithMos(12, 7, 5, null));
            Assert.DoesNotContain("periods", single);
            Assert.Null(TunxenFormat.Parse(single.Replace("version: 3", "version: 2")).Keyboard.Mos!.Periods);
        }

        [Fact]
        public void UstxenVersion3RoundTripsTheRepeatCount() {
            var project = Ustx.Create();
            project.pitchSystem = WithMos(22, 2, 10, 2);
            var text = XenProjectFormat.Serialize(project);
            Assert.Contains("version: 4", text);
            var read = XenProjectFormat.Parse(text);
            Assert.Equal(2, read.pitchSystem.Keyboard.Mos!.Periods);
            Assert.Equal(2, XenProjectFormat.Parse(text.Replace("version: 4", "version: 2")).pitchSystem.Keyboard.Mos!.Periods);
            project.pitchSystem = WithMos(22, 13, 7, null);
            var v2 = XenProjectFormat.Serialize(project).Replace("version: 4", "version: 2");
            Assert.Null(XenProjectFormat.Parse(v2).pitchSystem.Keyboard.Mos!.Periods);
        }
    }
}
