using System;
using System.Collections.Generic;
using System.IO;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Test {
    public class VibratoCurveTest {
        [Fact]
        public void LegacyNotesKeepTheOriginalPitchAndVolumeFormulae() {
            var note = UNote.Create();
            note.tone = 69;
            note.duration = 960;
            var vibrato = note.vibrato;
            vibrato.length = 70;
            vibrato.period = 180;
            vibrato.depth = 42;
            vibrato.@in = 20;
            vibrato.@out = 25;
            vibrato.shift = 35;
            vibrato.drift = -12;
            vibrato.volLink = -65;
            const float nPeriod = 0.18f;
            for (int i = 0; i <= 100; ++i) {
                float position = i / 100f;
                float start = vibrato.NormalizedStart;
                float fadeIn = vibrato.length / 100f * vibrato.@in / 100f;
                float fadeOut = vibrato.length / 100f * vibrato.@out / 100f;
                float phase = (position - start) / nPeriod + vibrato.shift / 100f;
                float expected = (float)Math.Sin(2 * Math.PI * phase) * vibrato.depth
                    + vibrato.depth / 100 * vibrato.drift;
                float volumePhase = (position - start) / nPeriod + (vibrato.shift + 50) / 100f;
                float reduction = (-(float)Math.Sin(2 * Math.PI * volumePhase) / 2 + 0.3f)
                    * Math.Abs(vibrato.volLink) / 100;
                if (position < start) {
                    expected = 0;
                    reduction = 0;
                } else if (position < start + fadeIn) {
                    expected *= (position - start) / fadeIn;
                    reduction *= (position - start) / fadeIn;
                } else if (position > 1 - fadeOut) {
                    expected *= (1 - position) / fadeOut;
                    reduction *= (1 - position) / fadeOut;
                }
                Assert.Equal(note.AdjustedTone + expected / 100f, vibrato.Evaluate(position, nPeriod, note).Y);
                Assert.Equal(1 - reduction, vibrato.EvaluateVolume(position, nPeriod));
            }
            Assert.False(vibrato.HasCurves);
        }

        [Fact]
        public void VariableRateIntegratesFrequencyWithoutPhaseJumps() {
            var vibrato = new UVibrato {
                length = 100,
                shift = 0,
                rateCurve = new List<VibratoPoint> { new(0, 2), new(1, 6) },
            };
            Assert.Equal(1.25, vibrato.PhaseCycles(0.25f, 2), 6);
            Assert.Equal(3, vibrato.PhaseCycles(0.5f, 2), 6);
            Assert.Equal(8, vibrato.PhaseCycles(1, 2), 6);
            double before = vibrato.PhaseCycles(0.5f - 0.00001f, 2);
            double after = vibrato.PhaseCycles(0.5f + 0.00001f, 2);
            Assert.InRange(after - before, 0.00015, 0.00017);
            vibrato.length = 50;
            Assert.Equal(0, vibrato.PhaseCycles(0.25f, 2));
            Assert.Equal(5, vibrato.PhaseCycles(1, 2), 6);
        }

        [Fact]
        public void VariableRateUsesRealTimeOnBothSidesOfTempoChange() {
            var project = new UProject();
            project.tempos.Clear();
            project.tempos.Add(new UTempo(0, 120));
            project.tempos.Add(new UTempo(480, 60));
            project.timeAxis.BuildSegments(project);
            var part = new UVoicePart();
            var note = project.CreateNote(69, 0, 960);
            note.vibrato.length = 100;
            note.vibrato.shift = 0;
            note.vibrato.rateCurve = new List<VibratoPoint> { new(0, 2), new(1, 6) };
            note.vibrato.depthCurve = new List<VibratoPoint> { new(0, 40), new(1, 40) };
            note.vibrato.@in = 0;
            note.vibrato.@out = 0;
            Assert.Equal(1.5, note.vibrato.PhaseCyclesAtTick(project, part, note, 480), 6);
            Assert.Equal(6.5, note.vibrato.PhaseCyclesAtTick(project, part, note, 960), 6);
            Assert.InRange(Math.Abs(note.vibrato.EvaluateAtTick(project, part, note, 960).Y - note.AdjustedTone), 0, 0.0001);
        }

        [Fact]
        public void CurvesCloneIndependentlyAndAllowZeroDepth() {
            var original = new UVibrato {
                length = 100,
                rateCurve = new List<VibratoPoint> { new(0, 4) },
                depthCurve = new List<VibratoPoint> { new(0, 0), new(1, 80) },
            };
            var clone = original.Clone();
            clone.rateCurve![0].y = 7;
            clone.depthCurve![0].y = 10;
            Assert.Equal(4, original.SampleRate(0));
            Assert.Equal(0, original.SampleDepth(0));
            Assert.Equal(40, original.SampleDepth(0.5f));
            Assert.NotSame(original.rateCurve, clone.rateCurve);
            Assert.NotSame(original.depthCurve![0], clone.depthCurve[0]);
        }

        [Fact]
        public void ValidationRejectsNonFiniteValuesAndNormalisesDuplicatePositions() {
            var vibrato = new UVibrato {
                rateCurve = new List<VibratoPoint> { new(1, 8), new(0, 4), new(0, 6) },
            };
            vibrato.ValidateCurves();
            Assert.Equal(2, vibrato.rateCurve!.Count);
            Assert.Equal(6, vibrato.SampleRate(0));
            vibrato.depthCurve = new List<VibratoPoint> { new(0, float.NaN) };
            Assert.Throws<InvalidDataException>(() => vibrato.ValidateCurves());
        }

        [Fact]
        public void SplittingSlicesCurvesAndRetainsIntegratedPhaseWithUndo() {
            var project = new UProject();
            project.timeAxis.BuildSegments(project);
            var part = new UVoicePart { position = 0, Duration = 960 };
            var note = project.CreateNote(69, 0, 960);
            note.vibrato.length = 100;
            note.vibrato.@in = 0;
            note.vibrato.@out = 0;
            note.vibrato.shift = 0;
            note.vibrato.drift = 0;
            note.vibrato.rateCurve = new List<VibratoPoint> { new(0, 2), new(1, 6) };
            note.vibrato.depthCurve = new List<VibratoPoint> { new(0, 20), new(1, 60) };
            part.notes.Add(note);
            var source = note.Clone();
            float expected = source.vibrato.EvaluateAtTick(project, part, source, 650).Y;
            var command = new SplitNotesAtTickCommand(project, part, new[] { note }, 480, "+");
            command.Execute();
            var right = command.RightNotes[0];
            Assert.Equal(4, right.vibrato.SampleRate(0));
            Assert.Equal(40, right.vibrato.SampleDepth(0));
            Assert.InRange(Math.Abs(expected - right.vibrato.EvaluateAtTick(project, part, right, 650).Y), 0, 0.0001);
            command.Unexecute();
            Assert.Single(part.notes);
            Assert.Equal(960, note.duration);
            Assert.Equal(2, note.vibrato.rateCurve!.Count);
            Assert.Equal(2, note.vibrato.SampleRate(0));
        }

        [Fact]
        public void LengthChangesKeepCurvesInPlaceWithinTheVibrato() {
            var part = new UVoicePart();
            var note = new UNote {
                duration = 480,
                vibrato = new UVibrato {
                    length = 50,
                    depthCurve = new List<VibratoPoint> { new(0.5f, 10), new(0.75f, 40), new(1, 20) },
                },
            };
            var command = new VibratoLengthCommand(part, note, 80);
            command.Execute();
            Assert.Equal(new[] { 0.2f, 0.6f, 1f }, note.vibrato.depthCurve!.ConvertAll(p => p.x).ToArray(),
                new FloatComparer());
            Assert.Equal(40, note.vibrato.depthCurve[1].y);
            command.Unexecute();
            Assert.Equal(0.75f, note.vibrato.depthCurve![1].x);
            // Switching the vibrato off leaves the curves where they are.
            new VibratoLengthCommand(part, note, 0).Execute();
            Assert.Equal(0.75f, note.vibrato.depthCurve![1].x);
            Assert.Null(VibratoCurve.Remap(null, 50, 80));
        }

        [Fact]
        public void HandlesShowTheCurvePeakAndFirstCycle() {
            var vibrato = new UVibrato { length = 50, depth = 25, period = 200 };
            Assert.Equal(25, vibrato.DisplayDepth);
            Assert.Equal(200, vibrato.DisplayPeriod);
            vibrato.depthCurve = new List<VibratoPoint> { new(0.5f, 30), new(1, 90) };
            vibrato.rateCurve = new List<VibratoPoint> { new(0.5f, 4), new(1, 8) };
            Assert.Equal(90, vibrato.DisplayDepth);
            Assert.Equal(250, vibrato.DisplayPeriod, 3);
            var scaled = VibratoCurve.Scale(vibrato.depthCurve, 0.5f, 0, UVibrato.MaxDepthCents)!;
            Assert.Equal(new[] { 15f, 45f }, scaled.ConvertAll(p => p.y).ToArray());
            Assert.Equal(30, vibrato.depthCurve[0].y);
        }

        sealed class FloatComparer : IEqualityComparer<float> {
            public bool Equals(float a, float b) => Math.Abs(a - b) < 1e-5f;
            public int GetHashCode(float value) => 0;
        }
    }
}
