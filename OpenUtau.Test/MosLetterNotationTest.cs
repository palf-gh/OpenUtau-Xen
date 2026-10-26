using System.Linq;
using OpenUtau.Core.Microtonal;
using Xunit;

namespace OpenUtau.Test {
    public class MosLetterNotationTest {
        static TuningDefinition WithMos(int divisions, string period, int generator, int cardinality, int mode = 0) {
            var tuning = TuningPresets.EqualDivision(divisions, PitchInterval.FromRatio(period));
            tuning.Keyboard.Mos = new KeyboardMos { Generator = generator, Cardinality = cardinality, Mode = mode };
            return tuning;
        }

        static int[] MosNotes(TuningDefinition tuning) => KeyboardRegionizer.MosDegrees(tuning, tuning.Keyboard.Mos!)!;

        static int[] Whites(TuningDefinition tuning) => Enumerable.Range(0, tuning.DegreeCount)
            .Where(d => PitchNotation.Role(tuning, d) == KeyRole.White).ToArray();

        static int[] Roles(TuningDefinition tuning) => Enumerable.Range(0, tuning.DegreeCount)
            .Select(d => (int)PitchNotation.Role(tuning, d)).ToArray();

        [Theory]
        [InlineData(31, "2/1", 18, 7)]
        [InlineData(31, "2/1", 13, 7)]
        [InlineData(12, "2/1", 7, 7)]
        [InlineData(13, "3/1", 3, 9)]
        public void FamilyModesKeepTraditionalNamesAndKeys(int divisions, string period, int generator, int cardinality) {
            var tuning = WithMos(divisions, period, generator, cardinality, 1);
            Assert.Null(KeyboardRegionizer.LetterNominals(tuning));
            Assert.False(PitchNotation.IsAvailable(tuning, PitchNotation.Mos));
            Assert.Equal("C", PitchNotation.Format(tuning, 0, PitchNotation.ResolvePrimary(tuning, null), false));
            Assert.Equal(cardinality, Whites(tuning).Length);
        }

        [Fact]
        public void PentatonicIn12EdoNamesItsFiveWhiteKeysFromC() {
            var tuning = WithMos(12, "2/1", 7, 5);
            var notes = MosNotes(tuning);
            Assert.Equal(notes, Whites(tuning));
            Assert.Equal(PitchNotation.Mos, PitchNotation.ResolvePrimary(tuning, null));
            Assert.Equal(new[] { "C", "D", "E", "A", "B" }, notes.Select(d => PitchNotation.Format(tuning, d, PitchNotation.Mos, false)));
            Assert.All(Enumerable.Range(0, 12).Except(notes), d => Assert.NotEqual(KeyRole.White, PitchNotation.Role(tuning, d)));
            Assert.Equal("C4", PitchNotation.Format(tuning, 0, PitchNotation.Mos));
            Assert.True(PitchNameInput.TryParse(tuning, "D4", 0, out int d4));
            Assert.Equal(notes[1], d4);
            Assert.Equal("E4", PitchNameInput.Format(tuning, notes[2]));
        }

        [Fact]
        public void ModesMoveTheLettersWithTheKeyboard() {
            var first = WithMos(31, "2/1", 18, 5, 0);
            var second = WithMos(31, "2/1", 18, 5, 1);
            Assert.NotEqual(MosNotes(first), MosNotes(second));
            Assert.Equal(MosNotes(first), Whites(first));
            Assert.Equal(MosNotes(second), Whites(second));
            Assert.Equal("D", PitchNotation.Format(second, MosNotes(second)[1], PitchNotation.Mos, false));
        }

        [Fact]
        public void LettersSkipI() {
            Assert.Equal("H", KeyboardRegionizer.LetterName(7));
            Assert.Equal("J", KeyboardRegionizer.LetterName(8));
            Assert.Equal("Z", KeyboardRegionizer.LetterName(24));
            Assert.Equal("AA", KeyboardRegionizer.LetterName(25));
            var tuning = WithMos(31, "2/1", 18, 12);
            var notes = MosNotes(tuning);
            Assert.Equal("CDEFGHJKLMAB", string.Concat(notes.Select(d => PitchNotation.Format(tuning, d, PitchNotation.Mos, false))));
            Assert.Equal("CDEFGHJAB", string.Concat(Enumerable.Range(0, 9).Select(i => KeyboardRegionizer.LetterName(i, 9))));
            Assert.True(PitchNameInput.TryParse(tuning, "K4", 0, out int k));
            Assert.Equal(notes[7], k);
        }

        [Fact]
        public void TritaveAndOtherPeriodsUseLettersOutsideTheFamily() {
            var tritave = WithMos(13, "3/1", 3, 4);
            Assert.NotNull(KeyboardRegionizer.LetterNominals(tritave));
            Assert.Equal(MosNotes(tritave), Whites(tritave));
            var fifth = WithMos(9, "3/2", 2, 4);
            Assert.NotNull(KeyboardRegionizer.LetterNominals(fifth));
            Assert.Equal(MosNotes(fifth), Whites(fifth));
        }

        [Fact]
        public void ChromaSharpsNameTheBlackKeys() {
            var tuning = WithMos(12, "2/1", 7, 5);
            var notes = MosNotes(tuning);
            int large = Enumerable.Range(0, notes.Length).First(i => (i + 1 < notes.Length ? notes[i + 1] : 12) - notes[i] == 3);
            Assert.Equal(KeyboardRegionizer.LetterName(large, notes.Length) + "♯", PitchNotation.Format(tuning, notes[large] + 1, PitchNotation.Mos, false));
        }

        [Fact]
        public void AnUnavailableMosShowsWhiteKeysWithDegreeNumbers() {
            var tuning = WithMos(25, "2/1", 15, 7);
            Assert.True(KeyboardRegionizer.IsUnavailableMos(tuning));
            Assert.Equal(Enumerable.Range(0, 25), Whites(tuning));
            Assert.Equal("degree", PitchNotation.ResolvePrimary(tuning, null));
            Assert.Null(KeyboardRegionizer.Model(tuning));
        }

        [Fact]
        public void KeyByKeyUsesOnlyTheClasses() {
            var tuning = WithMos(12, "2/1", 7, 5);
            tuning.Keyboard.Layout = KeyboardLayout.ManualLayout;
            tuning.Keyboard.Classes = Enumerable.Range(0, 12).ToDictionary(d => d, d => d % 3 == 0 ? "natural" : "accidental");
            Assert.True(KeyboardRegionizer.IsManual(tuning));
            Assert.Null(KeyboardRegionizer.LetterNominals(tuning));
            Assert.Null(KeyboardRegionizer.Model(tuning));
            Assert.False(PitchNotation.IsAvailable(tuning, PitchNotation.Mos));
            Assert.Equal(new[] { 0, 3, 6, 9 }, Whites(tuning));
        }

        [Fact]
        public void ClassesDoNotChangeAMosKeyboard() {
            var tuning = TuningPresets.EqualDivision(12);
            var before = Roles(tuning);
            tuning.Keyboard.Classes[1] = "natural";
            Assert.Equal(before, Roles(tuning));
        }

        [Fact]
        public void KeyboardAndScaleAreIndependent() {
            var tuning = TuningPresets.EqualDivision(12);
            var keys = Roles(tuning);
            tuning.Keyboard.ActiveScaleId = ScalePresets.Prefix + "major";
            tuning.Keyboard.Tonic = 2;
            Assert.Equal(new[] { 1, 2, 4, 6, 7, 9, 11 }, ScalePresets.ActiveDegrees(tuning));
            Assert.Equal(keys, Roles(tuning));
            tuning.Keyboard.Mos = new KeyboardMos { Generator = 7, Cardinality = 5 };
            Assert.NotEqual(keys, Roles(tuning));
            Assert.Equal(new[] { 1, 2, 4, 6, 7, 9, 11 }, ScalePresets.ActiveDegrees(tuning));
            Assert.Equal(new[] { 1, 2, 4, 6, 7, 9, 11 }, KeyboardRegionizer.DisplayDegrees(tuning));
        }

        [Fact]
        public void EqualDivisionPresetsStartWithTheFamilyKeyboardAndAllDegrees() {
            var twelve = TuningPresets.EqualDivision(12);
            Assert.Null(twelve.Keyboard.Mos);
            Assert.Equal(KeyboardLayout.MosLayout, twelve.Keyboard.Layout);
            Assert.Empty(twelve.Keyboard.Classes);
            Assert.Null(KeyboardRegionizer.DisplayDegrees(twelve));
            Assert.Equal(new[] { 0, 2, 4, 5, 7, 9, 11 }, Whites(twelve));
        }

        [Fact]
        public void ScalePresetsFollowTheTuning() {
            string[] Ids(TuningDefinition t) => ScalePresets.For(t).SelectMany(s => ScalePresets.Keys(s.Id).Select(k => ScalePresets.Prefix + k)).ToArray();
            var twelve = Ids(TuningPresets.EqualDivision(12));
            Assert.Contains(ScalePresets.Prefix + "major", twelve);
            Assert.Contains(ScalePresets.Prefix + "minor-pentatonic", twelve);
            Assert.Contains(ScalePresets.Prefix + "miyakobushi", twelve);
            Assert.DoesNotContain(ScalePresets.Prefix + "maqam-rast", twelve);
            var twentyFour = TuningPresets.EqualDivision(24);
            Assert.Contains(ScalePresets.Prefix + "maqam-bayati", Ids(twentyFour));
            Assert.Equal(new[] { 0, 4, 8, 10, 14, 18, 22 }, ScalePresets.Find(twentyFour, ScalePresets.Prefix + "major")!.Degrees);
            var thirtyOne = TuningPresets.EqualDivision(31);
            Assert.Equal(new[] { 0, 5, 8, 13, 18, 21, 28 }, ScalePresets.Find(thirtyOne, ScalePresets.Prefix + "harmonic-minor")!.Degrees);
            Assert.Contains(ScalePresets.Prefix + "lambda", Ids(TuningPresets.EqualDivision(13, PitchInterval.FromRatio("3/1"))));
            Assert.DoesNotContain(ScalePresets.Prefix + "major", Ids(TuningPresets.EqualDivision(7)));
            var pelog = TuningPresets.All.Single(t => t.Id == TuningPresets.PelogId);
            Assert.Contains(ScalePresets.Prefix + "pelog-barang", Ids(pelog));
            var ji = TuningPresets.All.Single(t => t.Id == "ji-5-limit");
            Assert.Equal(new[] { 0, 2, 4, 5, 7, 9, 11 }, ScalePresets.Find(ji, ScalePresets.Prefix + "major")!.Degrees);
        }

        [Theory]
        [InlineData("yonanuki-major", new[] { 0, 2, 4, 7, 9 })]
        [InlineData("yonanuki-minor", new[] { 0, 2, 3, 7, 8 })]
        [InlineData("yo", new[] { 0, 2, 5, 7, 9 })]
        [InlineData("in", new[] { 0, 1, 5, 7, 8 })]
        [InlineData("hirajoshi", new[] { 0, 2, 3, 7, 8 })]
        [InlineData("kumoi", new[] { 0, 2, 3, 7, 9 })]
        [InlineData("iwato", new[] { 0, 1, 5, 6, 10 })]
        public void JapaneseScalesIn12Edo(string id, int[] degrees) {
            var twelve = TuningPresets.EqualDivision(12);
            Assert.Equal(degrees, ScalePresets.Find(twelve, ScalePresets.Prefix + id)!.Degrees);
            Assert.NotNull(ScalePresets.Find(TuningPresets.EqualDivision(31), ScalePresets.Prefix + id));
        }

        [Fact]
        public void PresetsWithTheSameNotesShareOneEntry() {
            var twelve = TuningPresets.EqualDivision(12);
            var ids = ScalePresets.For(twelve).Select(s => s.Id).ToArray();
            Assert.Contains(ScalePresets.Prefix + "major-pentatonic+yonanuki-major", ids);
            Assert.Contains(ScalePresets.Prefix + "minor-pentatonic+minyo", ids);
            Assert.Contains(ScalePresets.Prefix + "miyakobushi+in", ids);
            Assert.Contains(ScalePresets.Prefix + "ritsu+yo", ids);
            Assert.Contains(ScalePresets.Prefix + "yonanuki-minor+hirajoshi", ids);
            Assert.Equal(ids.Length, ScalePresets.For(twelve).Select(s => string.Join(",", s.Degrees)).Distinct().Count());
            Assert.Contains(ScalePresets.Prefix + "major+maqam-ajam", ScalePresets.For(TuningPresets.EqualDivision(24)).Select(s => s.Id));
            // A scale chosen under one name still resolves to the merged entry.
            twelve.Keyboard.ActiveScaleId = ScalePresets.Prefix + "hirajoshi";
            Assert.Equal(new[] { 0, 2, 3, 7, 8 }, ScalePresets.ActiveDegrees(twelve));
        }

        [Fact]
        public void RastIsTheStandard24EdoKeyboardWithTheRastScale() {
            var rast = TuningPresets.All.Single(t => t.Id == "rast-24edo-approximation");
            Assert.False(KeyboardRegionizer.IsManual(rast));
            Assert.Equal(Roles(TuningPresets.EqualDivision(24)), Roles(rast));
            Assert.Equal(new[] { 0, 4, 7, 10, 14, 18, 21 }, KeyboardRegionizer.DisplayDegrees(rast));
        }

        [Fact]
        public void TunxenVersion2RoundTripsTheKeyboardAndScale() {
            var tuning = WithMos(12, "2/1", 7, 5, 2);
            tuning.Keyboard.Tonic = 7;
            tuning.Keyboard.ActiveScaleId = ScalePresets.Prefix + "ritsu";
            var text = TunxenFormat.Serialize(tuning);
            Assert.Contains("version: 3", text);
            var read = TunxenFormat.Parse(text);
            Assert.Equal(7, read.Keyboard.Tonic);
            Assert.Equal(5, read.Keyboard.Mos!.Cardinality);
            Assert.Equal(2, read.Keyboard.Mos.Mode);
            Assert.Equal(ScalePresets.Prefix + "ritsu", read.Keyboard.ActiveScaleId);
            Assert.Equal(Roles(tuning), Roles(read));
        }

        [Fact]
        public void TunxenVersion1KeyClassesBecomeTheMatchingLayout() {
            TuningDefinition ReadV1(TuningDefinition tuning) =>
                TunxenFormat.Parse(TunxenFormat.Serialize(tuning).Replace("version: 3", "version: 1"));
            var complete = TuningPresets.EqualDivision(12);
            complete.Keyboard.Classes = Enumerable.Range(0, 12).ToDictionary(d => d, _ => "natural");
            Assert.Equal(KeyboardLayout.ManualLayout, ReadV1(complete).Keyboard.Layout);
            var matching = TuningPresets.EqualDivision(12);
            foreach (int d in new[] { 0, 2, 4, 5, 7, 9, 11 }) matching.Keyboard.Classes[d] = "natural";
            var upgraded = ReadV1(matching);
            Assert.Equal(KeyboardLayout.MosLayout, upgraded.Keyboard.Layout);
            Assert.Empty(upgraded.Keyboard.Classes);
            var changed = TuningPresets.EqualDivision(12);
            changed.Keyboard.Classes[1] = "natural";
            var kept = ReadV1(changed);
            Assert.Equal(KeyboardLayout.ManualLayout, kept.Keyboard.Layout);
            Assert.Equal(KeyRole.White, PitchNotation.Role(kept, 1));
            Assert.Equal(KeyRole.Black, PitchNotation.Role(kept, 3));
        }

        public static TheoryData<string> TwelveNoteUnequalPresets() {
            var data = new TheoryData<string>();
            foreach (var preset in TuningPresets.All.Where(t => t.Divisions == 0 && t.DegreeCount == 12)) data.Add(preset.Id);
            return data;
        }

        [Theory]
        [MemberData(nameof(TwelveNoteUnequalPresets))]
        public void UnequalPresetsStartAsPianoKeyboardsAndCanUseADiatonicMos(string id) {
            var tuning = TuningPresets.All.Single(t => t.Id == id);
            var piano = new[] { 0, 2, 4, 5, 7, 9, 11 };
            Assert.True(KeyboardRegionizer.IsManual(tuning));
            Assert.Equal(piano, Whites(tuning));
            Assert.Equal(new[] { "C", "D", "E", "F", "G", "A", "B" },
                piano.Select(d => PitchNotation.Format(tuning, d, PitchNotation.ResolvePrimary(tuning, null), false)));
            var layout = NotchedKeyboardLayout.Create(Enumerable.Range(0, 12).Select(d => new NotchedKeyStep(d,
                piano.Contains(d) ? NotchedKeyKind.Natural : NotchedKeyKind.Shared, d)), true);
            Assert.True(layout.IsTraditionalKeyboard);
            var on = tuning.Clone();
            on.Keyboard.Layout = KeyboardLayout.MosLayout;
            on.Keyboard.Classes.Clear();
            int generator = KeyboardRegionizer.DefaultGenerator(on);
            Assert.Equal(7, generator);
            on.Keyboard.Mos = new KeyboardMos { Generator = generator, Cardinality = 7 };
            Assert.Equal(piano, MosNotes(on));
            Assert.Null(KeyboardRegionizer.LetterNominals(on));
            Assert.Equal(piano, Whites(on));
            Assert.Equal("C", PitchNotation.Format(on, 0, PitchNotation.ResolvePrimary(on, null), false));
            Assert.NotNull(KeyboardRegionizer.MosDegrees(on, new KeyboardMos { Generator = generator, Cardinality = 5 }));
        }

        [Fact]
        public void ExtendedPresetsCarryTheirOwnNames() {
            string Name(string id, int degree) {
                var t = TuningPresets.All.Single(p => p.Id == id);
                return PitchNotation.Format(t, degree, PitchNotation.ResolvePrimary(t, null), false);
            }
            Assert.Equal("Dügâh", Name(TuningPresets.AeuId, 4));
            Assert.Equal(440, TuningPresets.All.Single(p => p.Id == TuningPresets.AeuId).Reference.Frequency);
            Assert.Equal("pat", Name(TuningPresets.PelogId, 3));
            Assert.Equal("nem", Name("slendro-5edo", 4));
            Assert.Equal("Pa", Name("shruti-22", 13));
            Assert.Equal("H11", Name("harmonic-series-8-16", 3));
            Assert.Equal("11/8", Name("partch-43", 20));
            Assert.Equal(43, TuningPresets.All.Single(p => p.Id == "partch-43").DegreeCount);
            Assert.Equal(13, TuningPresets.All.Single(p => p.Id == "bohlen-pierce-just").DegreeCount);
            var alpha = TuningPresets.All.Single(p => p.Id == "carlos-alpha");
            Assert.Equal(9, alpha.Divisions);
            Assert.Equal(77.965, alpha.GetCents(1), 6);
            foreach (int divisions in new[] { 5, 7, 10, 15, 16, 26, 34, 46 }) Assert.Contains(TuningPresets.All, p => p.Id == $"{divisions}edo");
        }

        [Theory]
        [InlineData(15, 7, 1, 6)]
        [InlineData(16, 7, 2, 5)]
        [InlineData(10, 7, 3, 4)]
        public void EqualDivisionsWithoutAFamilyGetAUsableDefaultMos(int divisions, int cardinality, int large, int small) {
            var tuning = TuningPresets.EqualDivision(divisions);
            Assert.False(KeyboardRegionizer.IsUnavailableMos(tuning));
            var mos = KeyboardRegionizer.DefaultMos(tuning);
            Assert.Equal(cardinality, mos.Cardinality);
            var notes = KeyboardRegionizer.MosDegrees(tuning, mos)!;
            var steps = notes.Select((d, i) => (i + 1 < notes.Length ? notes[i + 1] : divisions) - d).ToArray();
            Assert.Equal(large, steps.Count(step => step == steps.Max()));
            Assert.Equal(small, steps.Count(step => step == steps.Min()));
            Assert.Equal(notes, Whites(tuning));
            Assert.Contains(KeyRole.Black, Enumerable.Range(0, divisions).Select(d => PitchNotation.Role(tuning, d)));
            if (divisions == 15) Assert.DoesNotContain(KeyRole.Between, Enumerable.Range(0, 15).Select(d => PitchNotation.Role(tuning, d)));
        }

        [Fact]
        public void SmallEqualDivisionsUseEveryDegreeAsAWhiteKey() {
            foreach (int divisions in new[] { 5, 7 }) {
                var tuning = TuningPresets.EqualDivision(divisions);
                Assert.Equal(Enumerable.Range(0, divisions), Whites(tuning));
            }
        }

        [Fact]
        public void JustBohlenPierceHasLambdaBlackKeys() {
            var bp = TuningPresets.All.Single(p => p.Id == "bohlen-pierce-just");
            Assert.Equal(new[] { 0, 2, 3, 4, 6, 7, 9, 10, 12 }, Whites(bp));
            Assert.Equal(4, Enumerable.Range(0, 13).Count(d => PitchNotation.Role(bp, d) == KeyRole.Black));
        }

        [Fact]
        public void NonGeneratorScalesKeepTheFamilyKeyboard() {
            var tuning = TuningPresets.EqualDivision(31);
            tuning.Keyboard.ActiveScaleId = "all";
            Assert.Null(KeyboardRegionizer.LetterNominals(tuning));
            Assert.Equal(7, Whites(tuning).Length);
        }
    }
}
