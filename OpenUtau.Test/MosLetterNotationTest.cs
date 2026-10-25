using System.Linq;
using OpenUtau.Core.Microtonal;
using Xunit;

namespace OpenUtau.Test {
    public class MosLetterNotationTest {
        static TuningDefinition WithMos(int divisions, string period, int generator, int cardinality, int mode = 0) {
            var tuning = TuningPresets.EqualDivision(divisions, PitchInterval.FromRatio(period));
            var scale = MosGenerator.Generate(tuning, generator, cardinality, mode).Scale;
            tuning.Scales.Add(scale);
            tuning.Keyboard.ActiveScaleId = scale.Id;
            return tuning;
        }

        static System.Collections.Generic.List<int> Active(TuningDefinition tuning) =>
            tuning.Scales.Single(s => s.Id == tuning.Keyboard.ActiveScaleId).Degrees;

        static int[] Whites(TuningDefinition tuning) => Enumerable.Range(0, tuning.DegreeCount)
            .Where(d => PitchNotation.Role(tuning, d) == KeyRole.White).ToArray();

        [Theory]
        [InlineData(31, "2/1", 18, 7)]
        [InlineData(31, "2/1", 13, 7)]
        [InlineData(12, "2/1", 7, 7)]
        [InlineData(13, "3/1", 3, 9)]
        public void FamilyModesKeepTraditionalNamesAndKeys(int divisions, string period, int generator, int cardinality) {
            var tuning = WithMos(divisions, period, generator, cardinality, 1);
            Assert.Null(KeyboardRegionizer.LetterScale(tuning));
            Assert.False(PitchNotation.IsAvailable(tuning, PitchNotation.Mos));
            Assert.Equal("C", PitchNotation.Format(tuning, 0, PitchNotation.ResolvePrimary(tuning, null), false));
            Assert.Equal(cardinality, Whites(tuning).Length);
        }

        [Fact]
        public void PentatonicIn12EdoNamesItsFiveWhiteKeysAToE() {
            var tuning = WithMos(12, "2/1", 7, 5);
            var degrees = Active(tuning);
            // The 12-EDO preset's partial natural classes do not override a letter-named MOS.
            Assert.NotEmpty(tuning.Keyboard.Classes);
            Assert.Equal(degrees, Whites(tuning));
            Assert.Equal(PitchNotation.Mos, PitchNotation.ResolvePrimary(tuning, null));
            Assert.Equal(new[] { "A", "B", "C", "D", "E" },
                degrees.Select(d => PitchNotation.Format(tuning, d, PitchNotation.Mos, false)));
            Assert.All(Enumerable.Range(0, 12).Except(degrees), d => Assert.NotEqual(KeyRole.White, PitchNotation.Role(tuning, d)));
            Assert.Equal("A4", PitchNotation.Format(tuning, 0, PitchNotation.Mos));
            Assert.True(PitchNameInput.TryParse(tuning, "B4", 0, out int b));
            Assert.Equal(degrees[1], b);
            Assert.Equal("C4", PitchNameInput.Format(tuning, degrees[2]));
        }

        [Fact]
        public void ModesMoveTheLettersWithTheScale() {
            var first = WithMos(31, "2/1", 18, 5, 0);
            var second = WithMos(31, "2/1", 18, 5, 1);
            Assert.NotEqual(Active(first), Active(second));
            Assert.Equal(Active(first), Whites(first));
            Assert.Equal(Active(second), Whites(second));
            Assert.Equal("B", PitchNotation.Format(second, Active(second)[1], PitchNotation.Mos, false));
        }

        [Fact]
        public void LettersSkipI() {
            Assert.Equal("H", KeyboardRegionizer.LetterName(7));
            Assert.Equal("J", KeyboardRegionizer.LetterName(8));
            Assert.Equal("Z", KeyboardRegionizer.LetterName(24));
            Assert.Equal("AA", KeyboardRegionizer.LetterName(25));
            var tuning = WithMos(31, "2/1", 18, 12);
            var degrees = Active(tuning);
            Assert.Equal("ABCDEFGHJKLM", string.Concat(degrees.Select(d => PitchNotation.Format(tuning, d, PitchNotation.Mos, false))));
            Assert.True(PitchNameInput.TryParse(tuning, "K4", 0, out int k));
            Assert.Equal(degrees[9], k);
        }

        [Fact]
        public void TritaveAndOtherPeriodsUseLettersOutsideTheFamily() {
            var tritave = WithMos(13, "3/1", 3, 4);
            Assert.NotNull(KeyboardRegionizer.LetterScale(tritave));
            Assert.Equal(Active(tritave), Whites(tritave));
            var fifth = WithMos(9, "3/2", 2, 4);
            Assert.NotNull(KeyboardRegionizer.LetterScale(fifth));
            Assert.Equal(Active(fifth), Whites(fifth));
        }

        [Fact]
        public void ChromaSharpsNameTheBlackKeys() {
            // 12-EDO pentatonic: large 3, small 2, chroma 1.
            var tuning = WithMos(12, "2/1", 7, 5);
            var degrees = Active(tuning);
            int large = Enumerable.Range(0, degrees.Count).First(i => (i + 1 < degrees.Count ? degrees[i + 1] : 12) - degrees[i] == 3);
            Assert.Equal(KeyboardRegionizer.LetterName(large) + "♯", PitchNotation.Format(tuning, degrees[large] + 1, PitchNotation.Mos, false));
        }

        [Fact]
        public void EqualDivisionPresetsStartWithTheirFamilyMos() {
            var twelve = TuningPresets.EqualDivision(12);
            Assert.Equal(new[] { 0, 2, 4, 5, 7, 9, 11 }, Active(twelve));
            Assert.Equal(7, twelve.Scales.Single(s => s.Id == twelve.Keyboard.ActiveScaleId).GeneratorDegree);
            var lambda = TuningPresets.EqualDivision(13, PitchInterval.FromRatio("3/1"));
            Assert.Equal(new[] { 0, 2, 3, 4, 6, 7, 9, 10, 12 }, Active(lambda));
            Assert.Equal(1, TuningPresets.EqualDivision(53).Scales.Count(s => s.GeneratorDegree != null));
            // 25-EDO has no family MOS, so it keeps all degrees.
            var twentyFive = TuningPresets.EqualDivision(25);
            Assert.DoesNotContain(twentyFive.Scales, s => s.GeneratorDegree != null);
        }

        [Fact]
        public void PianoRollScaleRowsKeepTheChromaticKeys() {
            Assert.Equal(Enumerable.Range(0, 12), KeyboardRegionizer.DisplayDegrees(TuningPresets.EqualDivision(12)));
            var thirtyOne = KeyboardRegionizer.DisplayDegrees(TuningPresets.EqualDivision(31))!;
            Assert.Equal(17, thirtyOne.Count);
            Assert.Contains(2, thirtyOne);
            var pentatonic = WithMos(12, "2/1", 7, 5);
            var rows = KeyboardRegionizer.DisplayDegrees(pentatonic)!;
            Assert.True(rows.Count > 5);
            Assert.Equal(Enumerable.Range(0, 12).Where(d => PitchNotation.Role(pentatonic, d) != KeyRole.Between), rows);
            var rast = TuningPresets.All.Single(t => t.Id == "rast-24edo-approximation");
            Assert.Equal(new[] { 0, 4, 7, 10, 14, 18, 21 }, KeyboardRegionizer.DisplayDegrees(rast));
        }

        [Fact]
        public void NonGeneratorScalesKeepTheFamilyKeyboard() {
            var tuning = TuningPresets.EqualDivision(31);
            tuning.Keyboard.ActiveScaleId = "all";
            Assert.Null(KeyboardRegionizer.LetterScale(tuning));
            Assert.Equal(7, Whites(tuning).Length);
        }

        [Fact]
        public void MosOffUsesOnlyTheKeyByKeyClasses() {
            var tuning = WithMos(12, "2/1", 7, 5);
            tuning.Keyboard.Classes = Enumerable.Range(0, 12).ToDictionary(d => d, d => d % 3 == 0 ? "natural" : "accidental");
            Assert.True(KeyboardRegionizer.IsManual(tuning));
            Assert.Null(KeyboardRegionizer.LetterScale(tuning));
            Assert.Null(KeyboardRegionizer.Model(tuning));
            Assert.False(PitchNotation.IsAvailable(tuning, PitchNotation.Mos));
            Assert.Equal(new[] { 0, 3, 6, 9 }, Whites(tuning));
        }

        [Fact]
        public void UnequalTuningsCanUseAMos() {
            var tuning = TuningPresets.All.First(t => t.Divisions == 0 && t.DegreeCount == 12).Clone();
            tuning.Keyboard.Classes.Clear();
            var scale = MosGenerator.Generate(tuning, 7, 5).Scale;
            tuning.Scales.Add(scale);
            tuning.Keyboard.ActiveScaleId = scale.Id;
            Assert.NotNull(KeyboardRegionizer.LetterScale(tuning));
            Assert.Equal(scale.Degrees, Whites(tuning));
            Assert.Equal("A", PitchNotation.Format(tuning, 0, PitchNotation.ResolvePrimary(tuning, null), false));
        }

        [Fact]
        public void RastStartsWithMosOffAndItsNotesAsWhiteKeys() {
            var rast = TuningPresets.All.Single(t => t.Id == "rast-24edo-approximation");
            var notes = new[] { 0, 4, 7, 10, 14, 18, 21 };
            Assert.True(KeyboardRegionizer.IsManual(rast));
            Assert.Equal(notes, Whites(rast));
            Assert.Equal(new[] { 2, 6, 8, 12, 16, 20, 22 }, Enumerable.Range(0, 24).Where(d => PitchNotation.Role(rast, d) == KeyRole.Black));
            Assert.All(Enumerable.Range(0, 24).Where(d => d % 2 == 1 && !notes.Contains(d)),
                d => Assert.Equal(KeyRole.Between, PitchNotation.Role(rast, d)));
        }

        public static TheoryData<string> UnequalPresets() {
            var data = new TheoryData<string>();
            foreach (var preset in TuningPresets.All.Where(t => t.Divisions == 0)) data.Add(preset.Id);
            return data;
        }

        [Theory]
        [MemberData(nameof(UnequalPresets))]
        public void UnequalPresetsStartAsPianoKeyboardsAndCanGenerateADiatonicMos(string id) {
            var tuning = TuningPresets.All.Single(t => t.Id == id);
            var piano = new[] { 0, 2, 4, 5, 7, 9, 11 };
            // MOS off: the keys follow the chromatic names.
            Assert.True(KeyboardRegionizer.IsManual(tuning));
            Assert.Equal(piano, Whites(tuning));
            Assert.Equal(new[] { "C", "D", "E", "F", "G", "A", "B" },
                piano.Select(d => PitchNotation.Format(tuning, d, PitchNotation.ResolvePrimary(tuning, null), false)));
            var layout = NotchedKeyboardLayout.Create(Enumerable.Range(0, 12).Select(d => new NotchedKeyStep(d,
                piano.Contains(d) ? NotchedKeyKind.Natural : NotchedKeyKind.Shared, d)), true);
            Assert.True(layout.IsTraditionalKeyboard);
            // MOS on: the degree nearest 3/2 generates C major as 5L2s, keeping the conventional keyboard and names.
            var on = tuning.Clone();
            on.Keyboard.Classes.Clear();
            int generator = KeyboardRegionizer.DefaultGenerator(on);
            Assert.Equal(7, generator);
            var diatonic = MosGenerator.Generate(on, generator, 7);
            Assert.True(diatonic.IsMos);
            Assert.Equal(piano, diatonic.Scale.Degrees);
            on.Scales.Add(diatonic.Scale);
            on.Keyboard.ActiveScaleId = diatonic.Scale.Id;
            Assert.Null(KeyboardRegionizer.LetterScale(on));
            Assert.Equal(piano, Whites(on));
            Assert.Equal("C", PitchNotation.Format(on, 0, PitchNotation.ResolvePrimary(on, null), false));
            Assert.True(MosGenerator.Generate(on, generator, 5).IsMos);
        }
    }
}
