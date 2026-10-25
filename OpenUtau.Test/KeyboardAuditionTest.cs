using System;
using System.Collections.Generic;
using OpenUtau.Core.Microtonal;
using Xunit;

namespace OpenUtau.Test {
    public class KeyboardAuditionTest {
        static TuningDefinition Tuning(int divisions, string period = "2/1") {
            var tuning = TuningPresets.EqualDivision(divisions, PitchInterval.FromRatio(period));
            tuning.Reference.Degree = 0;
            tuning.Reference.Frequency = 440;
            return tuning;
        }

        static KeyboardAudition Audition(List<(bool Start, double Frequency)> events) =>
            new KeyboardAudition(frequency => events.Add((true, frequency)), frequency => events.Add((false, frequency)));

        [Theory]
        [InlineData(53, "2/1", 1)]
        [InlineData(13, "3/1", 3)]
        public void PreviewUsesTheResolvedFrequencyWithoutMidiRounding(int divisions, string period, int degree) {
            var events = new List<(bool Start, double Frequency)>();
            var audition = Audition(events);
            var tuning = Tuning(divisions, period);
            audition.BeginPointer(tuning, degree);
            double ratio = period == "2/1" ? 2 : 3;
            double expected = 440 * Math.Pow(ratio, (double)degree / divisions);
            var started = Assert.Single(events);
            Assert.True(started.Start);
            Assert.Equal(expected, started.Frequency, 10);
            Assert.Equal(started.Frequency, audition.Frequency);
            audition.EndPointer();
            Assert.Equal((false, started.Frequency), events[1]);
            Assert.Null(audition.Frequency);
        }

        [Fact]
        public void ExactRatioAndReferenceFrequencyAreBothApplied() {
            var tuning = MosGenerator.CreateTuning(PitchInterval.FromRatio("2/1"), PitchInterval.FromRatio("5/4"), 2);
            tuning.Reference.Degree = 0;
            tuning.Reference.Frequency = 432;
            var events = new List<(bool Start, double Frequency)>();
            var audition = Audition(events);
            audition.BeginKeyboard(tuning, 1);
            Assert.Equal(540, Assert.Single(events).Frequency, 10);
            audition.EndKeyboard();
            Assert.Equal(2, events.Count);
            Assert.Equal(events[0].Frequency, events[1].Frequency);
        }

        [Fact]
        public void NonZeroReferenceDegreeAndCleanupUseTheOriginalStartedFrequency() {
            var tuning = Tuning(53);
            tuning.Reference.Degree = 40;
            tuning.Reference.Frequency = 432;
            var events = new List<(bool Start, double Frequency)>();
            var audition = Audition(events);
            audition.BeginKeyboard(tuning, 40);
            Assert.Equal(432, Assert.Single(events).Frequency, 10);
            audition.SetSelection(tuning, 0);
            Assert.Equal(432 * Math.Pow(2, -40.0 / 53), events[2].Frequency, 10);
            double started = events[2].Frequency;
            tuning.Reference.Frequency = 442;
            audition.Cancel();
            Assert.Equal((false, started), events[3]);
        }

        [Fact]
        public void DraggingEndsTheOldPitchBeforeStartingTheNextAndCanLeaveTheKeyboard() {
            var tuning = Tuning(53);
            var events = new List<(bool Start, double Frequency)>();
            var audition = Audition(events);
            audition.BeginPointer(tuning, 1);
            audition.SetSelection(tuning, 1);
            Assert.Single(events);
            audition.SetSelection(tuning, 2);
            Assert.Equal(3, events.Count);
            Assert.Equal((false, events[0].Frequency), events[1]);
            Assert.True(events[2].Start);
            Assert.NotEqual(events[0].Frequency, events[2].Frequency);
            audition.SetSelection(tuning, null);
            Assert.Equal((false, events[2].Frequency), events[3]);
            audition.SetSelection(tuning, 3);
            audition.EndPointer();
            audition.EndPointer();
            Assert.Equal(6, events.Count);
            Assert.Equal((false, events[4].Frequency), events[5]);
        }

        [Fact]
        public void KeyboardRepeatAndConcurrentInputSourcesDoNotDuplicateVoices() {
            var tuning = Tuning(31);
            var events = new List<(bool Start, double Frequency)>();
            var audition = Audition(events);
            audition.BeginKeyboard(tuning, 1);
            audition.BeginKeyboard(tuning, 1);
            audition.BeginPointer(tuning, 1);
            Assert.Single(events);
            audition.SetSelection(tuning, 2);
            Assert.Equal(3, events.Count);
            audition.EndPointer();
            Assert.Equal(3, events.Count);
            Assert.True(audition.KeyboardHeld);
            audition.EndKeyboard();
            Assert.Equal(4, events.Count);
            Assert.Equal((false, events[2].Frequency), events[3]);
        }

        [Fact]
        public void CancellationClearsAllHoldsAndRepeatedCleanupDoesNotEndAnotherVoice() {
            var tuning = Tuning(13, "3/1");
            var events = new List<(bool Start, double Frequency)>();
            var audition = Audition(events);
            audition.BeginPointer(tuning, 1);
            audition.BeginKeyboard(tuning, 1);
            audition.Cancel();
            audition.Cancel();
            audition.EndPointer();
            audition.EndKeyboard();
            audition.SetSelection(tuning, 2);
            Assert.Equal(2, events.Count);
            Assert.False(audition.PointerHeld);
            Assert.False(audition.KeyboardHeld);
            Assert.Null(audition.Frequency);
        }

        [Fact]
        public void InvalidFrequencyStopsThePreviousToneWithoutSendingInvalidAudio() {
            var tuning = Tuning(12);
            var events = new List<(bool Start, double Frequency)>();
            var audition = Audition(events);
            audition.BeginPointer(tuning, 0);
            tuning.Reference.Frequency = double.MaxValue;
            audition.SetSelection(tuning, 12);
            Assert.Equal(new[] { (true, 440.0), (false, 440.0) }, events);
            Assert.Null(audition.Frequency);
            audition.Cancel();
            Assert.Equal(2, events.Count);
        }

        [Fact]
        public void AFailedAudioStartCleansUpTheAttemptedToneAndInputHolds() {
            var events = new List<(bool Start, double Frequency)>();
            var audition = new KeyboardAudition(frequency => {
                events.Add((true, frequency));
                throw new InvalidOperationException("No audio output");
            }, frequency => events.Add((false, frequency)));
            Assert.Throws<InvalidOperationException>(() => audition.BeginPointer(Tuning(12), 0));
            Assert.Equal(new[] { (true, 440.0), (false, 440.0) }, events);
            Assert.False(audition.PointerHeld);
            Assert.False(audition.KeyboardHeld);
            Assert.Null(audition.Frequency);
        }

        [Fact]
        public void AFailedStopStillClearsStateAndCannotBeSentTwice() {
            int stops = 0;
            var audition = new KeyboardAudition(_ => { }, _ => {
                stops++;
                throw new InvalidOperationException("Audio output stopped");
            });
            audition.BeginPointer(Tuning(12), 0);
            Assert.Throws<InvalidOperationException>(audition.Cancel);
            Assert.False(audition.PointerHeld);
            Assert.Null(audition.Frequency);
            audition.Cancel();
            Assert.Equal(1, stops);
        }
    }
}
