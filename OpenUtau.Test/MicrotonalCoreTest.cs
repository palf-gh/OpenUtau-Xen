using System;
using System.Linq;
using OpenUtau.Core.Microtonal;
using Xunit;

namespace OpenUtau.Test {
    public class MicrotonalCoreTest {
        [Fact]
        public void NonOctaveEqualDivisionDoesNotInheritOctaveAccidentals() {
            foreach (int count in new[] { 12, 24, 53 }) {
                var tuning = TuningPresets.EqualDivision(count, PitchInterval.FromRatio("5/2"));
                Assert.Empty(tuning.Notations);
                Assert.Empty(tuning.Keyboard.Classes);
                Assert.Equal("degree", tuning.Keyboard.PrimaryNotation);
                Assert.DoesNotContain("EDO", tuning.Name);
                tuning.Validate();
            }
        }
        [Theory]
        [InlineData(440)]
        [InlineData(442)]
        [InlineData(432)]
        [InlineData(415)]
        public void ReferenceFrequencyAndNegativeDegrees(double frequency) {
            var tuning = TuningPresets.Default;
            tuning.Reference.Frequency = frequency;
            Assert.Equal(frequency, PitchResolver.ResolveFrequency(tuning, new PitchAddress { Degree = 9 }), 9);
            Assert.Equal(2, PitchResolver.ResolveFrequency(tuning, new PitchAddress { Degree = 9 })
                / PitchResolver.ResolveFrequency(tuning, new PitchAddress { Degree = -3 }), 12);
            Assert.Equal(-1300, tuning.GetCents(-13), 10);
        }
        [Fact]
        public void FiftyThreeEdoPreservesDistinctSharpsAndFlats() {
            var tuning = TuningPresets.EqualDivision(53);
            Assert.Equal(40, tuning.Reference.Degree);
            Assert.Equal(440, PitchResolver.ResolveFrequency(tuning, new PitchAddress { Degree = 40 }), 10);
            double cSharp = PitchResolver.ResolveMidi(tuning, new PitchAddress { Degree = 5 });
            double dFlat = PitchResolver.ResolveMidi(tuning, new PitchAddress { Degree = 4 });
            Assert.Equal(1200.0 / 53, 100 * (cSharp - dFlat), 8);
            var notation = tuning.Notations.Single(p => p.Id == "ups-downs");
            Assert.Contains("C♯", notation.Aliases[5]);
            Assert.Contains("D♭", notation.Aliases[4]);
        }
        [Fact]
        public void NearestDegreeTiesGoDownAndResidualsPreservePitch() {
            var tuning = TuningPresets.Default;
            Assert.Equal(0, PitchResolver.NearestAddress(tuning, 60.5).Degree);
            Assert.Equal(-1, PitchResolver.NearestAddress(tuning, 59.5).Degree);
            var nonUniform = TuningPresets.All.Single(t => t.Id == "ji-5-limit");
            foreach (double midi in new[] { -2.15, 59.3917, 69.123456, 127.9 }) {
                var address = PitchResolver.NearestAddress(nonUniform, midi, true);
                Assert.Equal(midi, PitchResolver.ResolveMidi(nonUniform, address), 10);
            }
        }
        [Fact]
        public void RationalIntervalsRemainExactAndRejectMalformedValues() {
            var tuning = TuningPresets.All.Single(t => t.Id == "ji-5-limit");
            Assert.Equal("5/4", tuning.Degrees[4].Ratio);
            double e = PitchResolver.ResolveFrequency(tuning, new PitchAddress { Degree = 4 });
            double c = PitchResolver.ResolveFrequency(tuning, new PitchAddress { Degree = 0 });
            Assert.Equal(1.25, e / c, 12);
            Assert.Equal("5/4", PitchInterval.FromRatio("10000000000000000000000000000000000000000000000000000000000000000000000/8000000000000000000000000000000000000000000000000000000000000000000000").Ratio);
            Assert.Throws<FormatException>(() => PitchInterval.FromRatio("1/0"));
            Assert.Throws<FormatException>(() => PitchInterval.FromRatio("-1/2"));
            Assert.Throws<FormatException>(() => new PitchInterval { Ratio = "1/1", Cents = 0 }.ToCents());
        }
        [Fact]
        public void NativeTuningRoundTripIncludesKeyboardNotationMappingAndExactRatios() {
            var tuning = TuningPresets.All.Single(t => t.Id == "ji-5-limit");
            tuning.Reference.Frequency = 432;
            tuning.Keyboard.Mode = PitchDisplayMode.Compact;
            tuning.Keyboard.ActiveScaleId = "triad";
            tuning.Scales.Add(new ScaleDefinition { Id = "triad", Name = "Pure triad", Degrees = new() { 0, 4, 7 } });
            tuning.Notations.Add(new NotationProfile { Id = "custom", Name = "Names", Names = new() { [0] = "do", [4] = "mi" } });
            string yaml = TunxenFormat.Serialize(tuning);
            var copy = TunxenFormat.Parse(yaml);
            Assert.Contains("OpenUtauXenTuning", yaml);
            Assert.Equal(432, copy.Reference.Frequency);
            Assert.Equal("5/4", copy.Degrees[4].Ratio);
            Assert.Equal(PitchDisplayMode.Compact, copy.Keyboard.Mode);
            Assert.Equal(new[] { 0, 4, 7 }, copy.Scales.Single(s => s.Id == "triad").Degrees);
            Assert.Equal("mi", copy.Notations.Single(n => n.Id == "custom").Names[4]);
            Assert.Throws<FormatException>(() => TunxenFormat.Parse("{}"));
            Assert.Throws<FormatException>(() => TunxenFormat.Parse(yaml.Replace("version: 3", "version: 99")));
        }
        [Fact]
        public void PresetsAreValidAndIndependent() {
            var first = TuningPresets.All;
            Assert.Equal(first.Count, first.Select(t => t.Id).Distinct().Count());
            Assert.True(first.Count >= 18);
            foreach (var tuning in first) {
                tuning.Validate();
                var clone = tuning.Clone();
                clone.Reference.Frequency = 123;
                Assert.NotEqual(123, tuning.Reference.Frequency);
                Assert.Equal(tuning.PeriodCents, tuning.GetCents(tuning.DegreeCount), 9);
                var roundTrip = TunxenFormat.Parse(TunxenFormat.Serialize(tuning));
                Assert.Equal(tuning.Id, roundTrip.Id);
                Assert.Equal(tuning.GetCents(-17), roundTrip.GetCents(-17), 8);
            }
        }
        [Fact]
        public void ScalaPreservesRatioPeriodAndCentsWithComments() {
            const string text = "! example\nMixed Scala scale\n3\n! intervals\n5/4 ! pure third\n701.955 arbitrary label\n3/1\n";
            var tuning = ScalaScl.Parse(text);
            Assert.Equal(3, tuning.DegreeCount);
            Assert.Equal("5/4", tuning.Degrees[1].Ratio);
            Assert.Equal(701.955, tuning.Degrees[2].Cents);
            Assert.Equal("3/1", tuning.Period.Ratio);
            var copy = ScalaScl.Parse(ScalaScl.Serialize(tuning));
            Assert.Equal("5/4", copy.Degrees[1].Ratio);
            Assert.Equal("3/1", copy.Period.Ratio);
            Assert.Equal(tuning.GetCents(-4), copy.GetCents(-4), 9);
            Assert.Throws<FormatException>(() => ScalaScl.Parse("bad\n2\n100.0\n"));
            Assert.Throws<FormatException>(() => ScalaScl.Parse("bad\n2\n3/2\n5/4\n"));
        }
        [Fact]
        public void KeyboardMappingSupportsHolesDuplicatesAndNegativeCycles() {
            var mapping = ScalaKbm.Parse("! map\n5\n0\n127\n60\n69\n442.0\n7\n0\n1\nx\n1\n4\n");
            Assert.Null(mapping.MapDegree(62));
            Assert.Equal(mapping.MapDegree(61), mapping.MapDegree(63));
            Assert.Equal(-3, mapping.MapDegree(59));
            Assert.Equal(11, mapping.MapDegree(69));
            var tuning = TuningPresets.EqualDivision(53);
            ScalaKbm.ApplyTo(tuning, mapping);
            Assert.Equal(442, PitchResolver.ResolveFrequency(tuning, PitchResolver.MapMidi(tuning, 69)!), 9);
            Assert.Null(PitchResolver.MapMidi(tuning, 62));
            Assert.Equal(mapping.Mapping, ScalaKbm.Parse(ScalaKbm.Serialize(mapping)).Mapping);
        }
        [Fact]
        public void ExportedKeyboardMappingPreservesNormalInputForNonOctaveTuning() {
            var original = TuningPresets.All.Single(t => t.Id == "bohlen-pierce-13edt");
            var copy = original.Clone();
            ScalaKbm.ApplyTo(copy, ScalaKbm.Parse(ScalaKbm.Serialize(original)));
            for (int key = 0; key < 128; key++) {
                Assert.Equal(PitchResolver.MapMidi(original, key)!.Degree, PitchResolver.MapMidi(copy, key)!.Degree);
                Assert.Equal(PitchResolver.ResolveMidi(original, PitchResolver.MapMidi(original, key)!),
                    PitchResolver.ResolveMidi(copy, PitchResolver.MapMidi(copy, key)!), 9);
            }
        }
        [Fact]
        public void FiftyThreeDiatonicMosAndEqualStepDegeneracy() {
            var tuning = TuningPresets.EqualDivision(53);
            var mos = MosGenerator.Generate(tuning, 31, 7);
            Assert.True(mos.IsMos);
            Assert.Equal(new[] { 0, 9, 18, 22, 31, 40, 49 }, mos.Scale.Degrees);
            Assert.Equal("LLsLLLs", mos.Pattern);
            Assert.Equal(5, mos.LargeCount);
            Assert.Equal(2, mos.SmallCount);
            var equal = MosGenerator.Generate(TuningPresets.Default, 2, 6);
            Assert.False(equal.IsMos);
            Assert.True(equal.IsEqualStep);
            Assert.Throws<ArgumentException>(() => MosGenerator.Generate(TuningPresets.Default, 2, 7));
        }
        [Fact]
        public void RatioGeneratorCreatesExactIntervalsWithoutQuantisation() {
            var tuning = MosGenerator.CreateTuning(PitchInterval.FromRatio("2/1"), PitchInterval.FromRatio("3/2"), 3);
            Assert.Equal(new[] { "1/1", "9/8", "3/2" }, tuning.Degrees.Select(d => d.Ratio));
            Assert.Equal(3, tuning.DegreeCount);
        }
        [Fact]
        public void UnsupportedNotationFallsBackToDegreeWithoutChangingPitch() {
            var tuning = TuningPresets.EqualDivision(53);
            Assert.Equal("5·4", PitchNotation.Format(tuning, 5, "unsupported-profile"));
            var address = new PitchAddress { Degree = 5 };
            double before = PitchResolver.ResolveFrequency(tuning, address);
            tuning.Keyboard.PrimaryNotation = "ups-downs";
            tuning.Keyboard.Mode = PitchDisplayMode.Compact;
            Assert.Equal(before, PitchResolver.ResolveFrequency(tuning, address));
        }
        [Fact]
        public void FiftyThreeSzgUsesVerifiedTuningSpecificSemanticOffsets() {
            var tuning = TuningPresets.EqualDivision(53);
            var profile = tuning.Notations.Single(p => p.Id == "szg");
            Assert.Equal(53, profile.Names.Count);
            Assert.Equal("D", profile.Names[4]);
            Assert.Equal("accidentalFlat", profile.AccidentalIds[4]);
            Assert.Equal("C", profile.Names[5]);
            Assert.Equal("accidentalSharp", profile.AccidentalIds[5]);
            Assert.Equal("accidentalThreeQuarterTonesSharpArrowUp", profile.AccidentalIds[6]);
            Assert.Equal("C\uE2624", PitchNotation.Format(tuning, 5, "szg"));
            Assert.Equal("C\uE261\uE27B5", PitchNotation.Format(tuning, 52, "szg"));
            string yaml = TunxenFormat.Serialize(tuning);
            Assert.Contains("accidentalSharp", yaml);
            Assert.DoesNotContain("\uE262", yaml);
        }
    }
}
