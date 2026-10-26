using System;
using System.Linq;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Test {
    public class VibratoPresetTest {
        // Rate (Hz) measured from V4's output for Standard 1 with flat rate values.
        [Theory]
        [InlineData(4, 2.48)]
        [InlineData(32, 3.501)]
        [InlineData(50, 4.158)]
        [InlineData(64, 4.669)]
        [InlineData(96, 7.003)]
        [InlineData(127, 9.339)]
        public void RateFollowsTheMeasuredValueLaw(int value, double hz) {
            var standard = VibratoPresets.Get(1)!;
            Assert.InRange(standard.RateHz(value), hz * 0.995, hz * 1.005);
            Assert.Equal(value, VibratoPreset.RateValue(standard.BaseRate, standard.RateHz(value)), 6);
        }

        // Rate (Hz) and the fundamental's amplitude (cents) measured for each type.
        [Theory]
        [InlineData(1, 4.1578, 60.83)]
        [InlineData(3, 4.1578, 121.75)]
        [InlineData(6, 2.6561, 106.18)]
        [InlineData(7, 3.1544, 331.75)]
        [InlineData(11, 5.5580, 127.45)]
        [InlineData(14, 4.6668, 21.80)]
        [InlineData(15, 4.6668, 39.37)]
        public void PresetsMatchMeasuredRatesAndDepths(int id, double hz, double cents) {
            var preset = VibratoPresets.Get(id)!;
            Assert.InRange(preset.RateHz(preset.Rate), hz * 0.995, hz * 1.005);
            Assert.InRange(preset.DepthCents(preset.Depth), cents * 0.99, cents * 1.01);
        }

        [Fact]
        public void PresetsKeepV4NumbersUnderXenNames() {
            Assert.Equal(new[] { "Standard 1", "Wide 1", "Quick 1", "Subtle 4" },
                new[] { 1, 5, 9, 16 }.Select(id => VibratoPresets.Get(id)!.Name));
            Assert.Equal("quick", VibratoPresets.Get(11)!.Family);
            Assert.Equal(3, VibratoPresets.Get(11)!.Number);
            Assert.Equal(Enumerable.Range(1, 16), VibratoPresets.All.Select(preset => preset.Id));
        }

        [Fact]
        public void ChoosingATypeSetsShiftAndFadesAndTheTopValueIs128() {
            Assert.Equal(128 * 60.86 / 64, VibratoPresets.Get(1)!.DepthCents(127), 3);
            Assert.Equal(127, VibratoPreset.DepthValue(60.86, 128 * 60.86 / 64), 6);
            // The onset is a fade-in over 0.34 s of a 1.7 s vibrato; the start phase is the shift.
            var vibrato = new UVibrato { length = 50, @in = 30, @out = 30, shift = 0 };
            VibratoPresets.Apply(vibrato, 9, 1.7);
            Assert.Equal(20, vibrato.@in, 4);
            Assert.Equal(0, vibrato.@out);
            Assert.Equal(28.5f, vibrato.shift, 4);
            // Without a known duration the fade-in stays as it is.
            vibrato = new UVibrato { length = 50, @in = 30, @out = 30 };
            VibratoPresets.Apply(vibrato, 1);
            Assert.Equal(30, vibrato.@in);
            Assert.Equal(27.1f, vibrato.shift, 4);
            // The dialogue's command does the same when a type is newly chosen, and only then.
            var note = new UNote { duration = 480, vibrato = new UVibrato { length = 50, @in = 30, @out = 30 } };
            note.PositionMs = 0;
            note.EndMs = 1000;
            var (rate, depth) = VibratoPresets.Get(13)!.Curves(50);
            new SetVibratoCurvesCommand(new UVoicePart(), new[] { note }, rate, depth, 50, 13).Execute();
            Assert.Equal(68, note.vibrato.@in, 4);
            Assert.Equal(0, note.vibrato.@out);
            Assert.Equal(25.7f, note.vibrato.shift, 4);
            note.vibrato.@in = 10;
            new SetVibratoCurvesCommand(new UVoicePart(), new[] { note }, rate, depth, 50, 13).Execute();
            Assert.Equal(10, note.vibrato.@in);
        }

        [Fact]
        public void PresetNotesKeepASine() {
            var note = new UNote { position = 0, duration = 480, tone = 60, vibrato = new UVibrato() };
            note.vibrato.length = 100;
            note.vibrato.@in = 0;
            note.vibrato.@out = 0;
            note.vibrato.drift = 0;
            note.vibrato.preset = 1;
            note.vibrato.shift = 27.1f;
            note.vibrato.depthCurve = new() { new VibratoPoint(0, 100), new VibratoPoint(1, 100) };
            note.vibrato.rateCurve = new() { new VibratoPoint(0, 5), new VibratoPoint(1, 5) };
            float nPos = 0.05f;
            float offset = (note.vibrato.Evaluate(nPos, 0.2f, note).Y - note.AdjustedTone) * 100;
            Assert.Equal(100 * Math.Sin(2 * Math.PI * note.vibrato.PhaseCycles(nPos, note.vibrato.period / 200.0)), offset, 3);
        }
    }
}
