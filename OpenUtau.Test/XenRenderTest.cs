using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Microtonal;
using OpenUtau.Core.Render;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;
using Xunit;

namespace OpenUtau.Test {
    /// <summary>
    /// Tests the actual shared phrase constructor with synthetic phonemes. No native synthesiser,
    /// voicebank recording, ONNX model, or external engine is invoked by these tests.
    /// </summary>
    public class XenRenderTest {
        [Fact]
        public void FlatF0IncludesReferenceDegreeResidualAndNoteTuningOnce() {
            var fixture = CreateFixture();
            fixture.Project.pitchSystem.Reference.Frequency = 432;
            var note = fixture.Part.notes.First();
            note.pitchAddress!.Degree = 40;
            note.pitchAddress.ResidualCents = 3.25;
            note.tuning = 17;
            var phrase = fixture.Build();
            double expectedFrequency = 432 * Math.Pow(2, (3.25 + 17) / 1200.0);
            Assert.All(phrase.pitches, cents => {
                double actual = MusicMath.ToneToFreq(cents * 0.01);
                Assert.InRange(Math.Abs(1200 * Math.Log2(actual / expectedFrequency)), 0, 0.001);
            });
            Assert.Equal(note.AdjustedTone, phrase.notes[0].adjustedTone);
            Assert.Equal(note.tone, phrase.phones[0].tone);
        }

        [Fact]
        public void Adjacent53DegreesInvalidateInferenceAndSynthesisEvenWithSameLegacyTone() {
            var fixture = CreateFixture();
            var note = fixture.Part.notes.First();
            note.pitchAddress!.Degree = 1;
            var lower = fixture.Build();
            note.pitchAddress.Degree = 2;
            var upper = fixture.Build();
            Assert.Equal(lower.notes[0].tone, upper.notes[0].tone);
            Assert.Equal(lower.phones[0].hash, upper.phones[0].hash);
            Assert.NotEqual(lower.preEffectHash, upper.preEffectHash);
            Assert.NotEqual(lower.hash, upper.hash);
            double cents = upper.pitches[20] - lower.pitches[20];
            Assert.InRange(Math.Abs(cents - 1200.0 / 53), 0, 0.001);
        }

        [Fact]
        public void ReferenceFrequencyInvalidatesInferenceAndSynthesisWithinSameSemitone() {
            var fixture = CreateFixture();
            fixture.Part.notes.First().pitchAddress!.Degree = 40;
            var standard = fixture.Build();
            fixture.Project.pitchSystem.Reference.Frequency = 432;
            var lowered = fixture.Build();
            Assert.Equal(standard.notes[0].tone, lowered.notes[0].tone);
            Assert.NotEqual(standard.preEffectHash, lowered.preEffectHash);
            Assert.NotEqual(standard.hash, lowered.hash);
            Assert.InRange(Math.Abs(MusicMath.ToneToFreq(lowered.pitches[20] * 0.01) - 432), 0, 0.001);
        }

        [Fact]
        public void TrackReferenceOverridesProjectReferenceWithoutDoubleApplyingIt() {
            var fixture = CreateFixture();
            fixture.Project.pitchSystem.Reference.Frequency = 432;
            fixture.Track.pitchSystem = TuningPresets.Default.Clone();
            fixture.Track.pitchSystem.Reference.Frequency = 442;
            fixture.Part.notes.First().pitchAddress!.Degree = 9;
            var first = fixture.Build();
            fixture.Project.pitchSystem.Reference.Frequency = 415;
            var second = fixture.Build();
            Assert.Equal(first.preEffectHash, second.preEffectHash);
            Assert.Equal(first.hash, second.hash);
            Assert.All(second.pitches, cents =>
                Assert.InRange(Math.Abs(MusicMath.ToneToFreq(cents * 0.01) - 442), 0, 0.001));
        }

        [Fact]
        public void ContiguousNotesKeepMicrotonalStepAcrossPhraseBoundary() {
            var fixture = CreateFixture(2);
            var notes = fixture.Part.notes.ToArray();
            notes[0].pitchAddress!.Degree = 40;
            notes[1].pitchAddress!.Degree = 41;
            var phrase = fixture.Build();
            Assert.Equal(2, phrase.notes.Length);
            Assert.Equal(2, phrase.phones.Length);
            Assert.Equal(notes[0].AdjustedTone * 100, phrase.pitches[10]);
            Assert.Equal(notes[1].AdjustedTone * 100, phrase.pitches[70]);
            Assert.InRange(Math.Abs((phrase.pitches[70] - phrase.pitches[10]) - 1200.0 / 53), 0, 0.001);
        }

        [Fact]
        public void PitchDeviationChangesFinalPitchWithoutRetuningBaseNotes() {
            var fixture = CreateFixture();
            fixture.Part.notes.First().pitchAddress!.Degree = 40;
            var before = fixture.Build();
            fixture.Part.curves.Add(new UCurve(fixture.Project.expressions[Ustx.PITD]) {
                xs = new List<int> { 0, 480 },
                ys = new List<int> { 25, 25 },
            });
            var after = fixture.Build();
            Assert.Equal(before.notes[0].adjustedTone, after.notes[0].adjustedTone);
            Assert.Equal(before.preEffectHash, after.preEffectHash);
            Assert.NotEqual(before.hash, after.hash);
            Assert.Equal(before.pitches[20], after.pitchesBeforeDeviation[20]);
            Assert.InRange(Math.Abs(after.pitches[20] - before.pitches[20] - 25), 0, 0.001);
        }

        [Fact]
        public void Legacy12EdoNotesRetainTheirOriginalPitchAndFineTuning() {
            var fixture = CreateFixture();
            var note = fixture.Part.notes.First();
            note.pitchAddress = null;
            note.tone = 69;
            note.tuning = -13;
            fixture.Project.pitchSystem = TuningPresets.Default.Clone();
            var phrase = fixture.Build();
            Assert.All(phrase.pitches, cents => Assert.InRange(Math.Abs(cents - 6887), 0, 0.001));
        }

        [Fact]
        public void VibratoAndPitchPointOffsetsDoNotDoubleApplyFineTuning() {
            var fixture = CreateFixture();
            var note = fixture.Part.notes.First();
            note.vibrato.length = 100;
            note.vibrato.depth = 20;
            note.vibrato.@in = 0;
            note.vibrato.@out = 0;
            note.pitch.AddPoint(new PitchPoint(0, 2));
            note.pitch.AddPoint(new PitchPoint((float)note.DurationMs, 2));
            var before = fixture.Build();
            note.tuning = 17;
            var after = fixture.Build();
            Assert.Equal(before.pitches.Length, after.pitches.Length);
            for (int index = 0; index < before.pitches.Length; index++) {
                Assert.InRange(Math.Abs(after.pitches[index] - before.pitches[index] - 17), 0, 0.002);
            }
        }

        [Fact]
        public void VariableVibratoChangesSharedPitchAndRenderCacheIdentity() {
            var fixture = CreateFixture();
            var note = fixture.Part.notes.First();
            note.vibrato.length = 100;
            note.vibrato.@in = 0;
            note.vibrato.@out = 0;
            note.vibrato.rateCurve = new List<VibratoPoint> { new(0, 3), new(1, 7) };
            note.vibrato.depthCurve = new List<VibratoPoint> { new(0, 10), new(1, 70) };
            var before = fixture.Build();
            note.vibrato.rateCurve[1].y = 9;
            var after = fixture.Build();
            Assert.NotEqual(before.hash, after.hash);
            Assert.False(before.pitches.SequenceEqual(after.pitches));
            // Acoustic score inference is reusable; final synthesis is not.
            Assert.Equal(before.preEffectHash, after.preEffectHash);
            Assert.Equal(before.notes[0].adjustedTone, after.notes[0].adjustedTone);
        }

        static Fixture CreateFixture(int noteCount = 1) => new Fixture(noteCount);

        sealed class Fixture {
            public readonly UProject Project = new UProject();
            public readonly UTrack Track;
            public readonly UVoicePart Part;

            public Fixture(int noteCount) {
                Ustx.AddDefaultExpressions(Project);
                Project.pitchSystem = TuningPresets.All.First(tuning => tuning.Divisions == 53).Clone();
                Track = Project.tracks[0];
                Track.Singer = new FixtureSinger();
                Track.RendererSettings.Renderer = new FixtureRenderer();
                Track.RendererSettings.renderer = "XEN-TEST";
                Part = new UVoicePart { trackNo = 0, position = 0, Duration = 480 * noteCount };
                Project.parts.Add(Part);
                var notes = new List<UNote>();
                for (int index = 0; index < noteCount; index++) {
                    var note = Project.CreateNote(69, 240 * index, 240);
                    note.ExtendedDuration = note.duration;
                    note.pitchAddress = new PitchAddress { Degree = 40 };
                    note.pitch.data.Clear();
                    note.vibrato.length = 0;
                    note.PositionMs = Project.timeAxis.TickPosToMsPos(note.position);
                    note.EndMs = Project.timeAxis.TickPosToMsPos(note.End);
                    notes.Add(note);
                    Part.notes.Add(note);
                }
                for (int index = 0; index < notes.Count; index++) {
                    notes[index].Prev = index == 0 ? null : notes[index - 1];
                    notes[index].Next = index + 1 == notes.Count ? null : notes[index + 1];
                    Part.phonemes.Add(new UPhoneme {
                        Parent = notes[index], position = notes[index].position,
                        phoneme = "a", rawPhoneme = "a", index = 0,
                    });
                }
                for (int index = 0; index < Part.phonemes.Count; index++) {
                    var phoneme = Part.phonemes[index];
                    phoneme.Prev = index == 0 ? null : Part.phonemes[index - 1];
                    phoneme.Next = index + 1 == Part.phonemes.Count ? null : Part.phonemes[index + 1];
                    phoneme.Validate(new ValidateOptions(), Project, Track, Part, phoneme.Parent);
                }
            }

            public RenderPhrase Build() => new RenderPhrase(Project, Track, Part, Part.phonemes);
        }

        sealed class FixtureSinger : USinger {
            public FixtureSinger() { found = true; loaded = true; }
            public override string Id => "xen-render-fixture";
            public override string Name => "Synthetic phrase fixture";
            public override USingerType SingerType => USingerType.Classic;
            public override IList<USubbank> Subbanks { get; } = new List<USubbank>();
            public override bool TryGetOto(string phoneme, out UOto oto) {
                oto = UOto.OfDummy(phoneme);
                return true;
            }
        }

        sealed class FixtureRenderer : IRenderer {
            public USingerType SingerType => USingerType.Classic;
            public bool SupportsRenderPitch => false;
            public bool SupportsExpression(UExpressionDescriptor descriptor) => true;
            public RenderResult Layout(RenderPhrase phrase) => throw new NotSupportedException();
            public Task<RenderResult> Render(RenderPhrase phrase, Progress progress, int trackNo,
                CancellationTokenSource cancellation, bool isPreRender = false,
                RenderPhraseEvents? renderEvents = null) => throw new NotSupportedException();
            public RenderPitchResult LoadRenderedPitch(RenderPhrase phrase) => throw new NotSupportedException();
            public UExpressionDescriptor[] GetSuggestedExpressions(USinger singer, URenderSettings settings) =>
                Array.Empty<UExpressionDescriptor>();
            public override string ToString() => "XEN-TEST";
        }
    }
}
