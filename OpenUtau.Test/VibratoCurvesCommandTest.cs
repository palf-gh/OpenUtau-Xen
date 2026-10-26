using System.Collections.Generic;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Test {
    public class VibratoCurvesCommandTest {
        [Fact]
        public void CurvesApplyAndUndoKeepEachNotesLegacySettings() {
            var part = new UVoicePart();
            var first = new UNote { vibrato = new UVibrato { length = 30, period = 160, depth = 25 } };
            var second = new UNote { vibrato = new UVibrato { length = 80, period = 240, depth = 40 } };
            var rate = new List<VibratoPoint> { new VibratoPoint(0, 5), new VibratoPoint(1, 8) };
            var depth = new List<VibratoPoint> { new VibratoPoint(0, 10), new VibratoPoint(1, 60) };
            var command = new SetVibratoCurvesCommand(part, new[] { first, second }, rate, depth, 66);
            rate[0].y = 100;
            command.Execute();
            Assert.Equal(5, first.vibrato.rateCurve[0].y);
            Assert.Equal(5, second.vibrato.rateCurve[0].y);
            Assert.Equal(160, first.vibrato.period);
            Assert.Equal(240, second.vibrato.period);
            Assert.Equal(66, first.vibrato.length);
            Assert.NotSame(first.vibrato.rateCurve, second.vibrato.rateCurve);
            Assert.NotSame(first.vibrato.rateCurve[0], second.vibrato.rateCurve[0]);
            command.Unexecute();
            Assert.Equal(30, first.vibrato.length);
            Assert.Equal(80, second.vibrato.length);
            Assert.Null(first.vibrato.rateCurve);
            Assert.Null(second.vibrato.depthCurve);
            command.Execute();
            Assert.Equal(8, second.vibrato.rateCurve[1].y);
        }

        [Fact]
        public void RemovingCurvesPreservesLegacyVibratoAndUndoRestoresCurves() {
            var part = new UVoicePart();
            var note = new UNote {
                vibrato = new UVibrato {
                    length = 75, period = 170, depth = 35, shift = 15,
                    rateCurve = new List<VibratoPoint> { new VibratoPoint(0.5f, 6) },
                },
            };
            var command = new SetVibratoCurvesCommand(part, new[] { note }, null, null);
            command.Execute();
            Assert.False(note.vibrato.HasCurves);
            Assert.Equal(75, note.vibrato.length);
            Assert.Equal(170, note.vibrato.period);
            Assert.Equal(35, note.vibrato.depth);
            Assert.Equal(15, note.vibrato.shift);
            command.Unexecute();
            Assert.True(note.vibrato.HasCurves);
            Assert.Equal(6, note.vibrato.rateCurve[0].y);
        }
    }
}
