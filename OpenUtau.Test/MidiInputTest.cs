using System;
using System.Collections.Generic;
using OpenUtau.Core.Microtonal;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Test {
    public class MidiInputTest {
        [Fact]
        public void NoteOffRetainsMappedPitchAndMonotonicDuration() {
            var state = new MidiInputState(TuningPresets.Default);
            MidiInputNote? started = null;
            MidiInputNote? ended = null;
            state.NoteStarted += note => started = note;
            state.NoteEnded += note => ended = note;
            state.NoteOn(0, 69, 90, 100);
            state.NoteOff(0, 69, 375);
            Assert.NotNull(started);
            Assert.Same(started, ended);
            Assert.Equal(69, started!.MidiTone, 8);
            Assert.Equal(275, ended!.EndMilliseconds - ended.StartMilliseconds, 8);
            Assert.Equal(0, state.ActiveCount);
        }

        [Fact]
        public void SustainReleasesOnlyKeysAlreadyReleasedOnSameChannel() {
            var state = new MidiInputState(TuningPresets.Default);
            var ended = new List<MidiInputNote>();
            state.NoteEnded += ended.Add;
            state.ControlChange(0, 64, 127, 0);
            state.NoteOn(0, 60, 80, 1);
            state.NoteOn(0, 64, 80, 2);
            state.NoteOn(1, 67, 80, 3);
            state.NoteOff(0, 60, 4);
            state.NoteOff(1, 67, 5);
            Assert.Single(ended);
            Assert.Equal(1, ended[0].Channel);
            state.ControlChange(0, 64, 0, 10);
            Assert.Equal(2, ended.Count);
            Assert.Equal(60, ended[1].Key);
            Assert.Equal(10, ended[1].EndMilliseconds);
            Assert.Equal(1, state.ActiveCount);
            state.NoteOff(0, 64, 15);
            Assert.Equal(0, state.ActiveCount);
        }

        [Fact]
        public void VelocityZeroIsNoteOffAndRetriggerClosesPreviousVoice() {
            var state = new MidiInputState(TuningPresets.Default);
            var ended = new List<MidiInputNote>();
            state.NoteEnded += ended.Add;
            state.NoteOn(0, 60, 100, 0);
            state.ControlChange(0, 64, 127, 1);
            state.NoteOn(0, 60, 100, 10);
            Assert.Single(ended);
            Assert.Equal(10, ended[0].EndMilliseconds);
            state.NoteOn(0, 60, 0, 20);
            Assert.Equal(1, state.ActiveCount);
            state.ControlChange(0, 64, 0, 30);
            Assert.Equal(2, ended.Count);
            Assert.NotEqual(ended[0].Id, ended[1].Id);
            Assert.Equal(0, state.ActiveCount);
        }

        [Fact]
        public void FilterAndChannelChangeCannotLeaveStuckNotes() {
            var state = new MidiInputState(TuningPresets.Default, 2);
            int ended = 0;
            state.NoteEnded += _ => ended++;
            state.NoteOn(0, 60, 100, 0);
            Assert.Equal(0, state.ActiveCount);
            state.NoteOn(2, 60, 100, 1);
            state.SetChannel(0, 5);
            Assert.Equal(1, ended);
            Assert.Equal(0, state.ActiveCount);
            state.NoteOn(0, 60, 100, 6);
            Assert.Equal(1, state.ActiveCount);
        }

        [Theory]
        [InlineData(120)]
        [InlineData(123)]
        public void PanicFlushesOnlyRequestedChannel(int controller) {
            var state = new MidiInputState(TuningPresets.Default);
            state.NoteOn(0, 60, 100, 0);
            state.NoteOn(1, 60, 100, 0);
            state.ControlChange(0, 64, 127, 1);
            state.NoteOff(0, 60, 2);
            state.ControlChange(0, controller, 0, 3);
            Assert.Equal(1, state.ActiveCount);
            state.AllNotesOff(4);
            Assert.Equal(0, state.ActiveCount);
        }

        [Fact]
        public void DisconnectFlushIsIdempotentAndClearsSustain() {
            var state = new MidiInputState(TuningPresets.Default);
            var ended = new List<MidiInputNote>();
            state.NoteEnded += ended.Add;
            state.ControlChange(0, 64, 127, 0);
            state.NoteOn(0, 60, 90, 1);
            state.NoteOff(0, 60, 2);
            state.AllNotesOff(10);
            state.AllNotesOff(11);
            Assert.Single(ended);
            Assert.Equal(10, ended[0].EndMilliseconds);
            state.NoteOn(0, 60, 90, 12);
            state.NoteOff(0, 60, 13);
            Assert.Equal(2, ended.Count);
            Assert.Equal(0, state.ActiveCount);
        }

        [Fact]
        public void StateCapturesTuningSnapshotAndUsesSameMappingAsFileImport() {
            var tuning = TuningPresets.Default.Clone();
            tuning.Reference.Frequency = 432;
            var address = PitchResolver.MapMidi(tuning, 69);
            var expected = PitchResolver.ResolveMidi(tuning, address!);
            var state = new MidiInputState(tuning);
            tuning.Reference.Frequency = 442;
            MidiInputNote? started = null;
            state.NoteStarted += note => started = note;
            state.NoteOn(0, 69, 100, 0);
            Assert.NotNull(started);
            Assert.Equal(expected, started!.MidiTone, 8);
        }

        [Fact]
        public void ScalaUnmappedKeysNeverCreatePreviewOrRecordedNotes() {
            var tuning = TuningPresets.Default.Clone();
            tuning.InputMapping = new InputMapping {
                IsScala = true, MapSize = 2, MiddleMidi = 60,
                FirstMidi = 60, LastMidi = 63, FormalOctave = 12,
                ReferenceMidi = 60, ReferenceFrequency = 261.6255653005986,
                Mapping = new List<int?> { 0, null },
            };
            var state = new MidiInputState(tuning);
            var started = new List<MidiInputNote>();
            state.NoteStarted += started.Add;
            state.NoteOn(0, 59, 100, 0);
            state.NoteOn(0, 61, 100, 1);
            state.NoteOn(0, 63, 100, 2);
            Assert.Empty(started);
            state.NoteOn(0, 60, 100, 3);
            state.NoteOn(0, 62, 100, 4);
            Assert.Equal(2, started.Count);
            Assert.Equal(12, started[1].Address.Degree - started[0].Address.Degree);
        }

        [Fact]
        public void RejectsInvalidChannelsAndTimestamps() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MidiInputState(TuningPresets.Default, 16));
            var state = new MidiInputState(TuningPresets.Default);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.NoteOn(-1, 60, 100, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.NoteOn(0, 60, 100, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.AllNotesOff(-1));
        }

        [Fact]
        public void RecordingConvertsWallClockThroughTempoChanges() {
            var project = new UProject();
            project.tempos.Add(new UTempo(480, 60));
            project.timeAxis.BuildSegments(project);
            var part = new UVoicePart { position = 0, trackNo = 0 };
            var state = new MidiInputState(TuningPresets.Default);
            var take = new List<MidiInputNote>();
            state.NoteEnded += take.Add;
            state.NoteOn(0, 69, 100, 1250);
            state.NoteOff(0, 69, 2000);
            var notes = MidiInputRecording.BuildNotes(project, part, take, 1000, 0);
            Assert.Single(notes);
            Assert.Equal(240, notes[0].position);
            Assert.Equal(480, notes[0].duration);
            Assert.Equal(take[0].Address.Degree, notes[0].pitchAddress!.Degree);
            Assert.NotSame(take[0].Address, notes[0].pitchAddress);
        }

        [Fact]
        public void RecordingUsesPartRelativePositionsAndMinimumDuration() {
            var project = new UProject();
            var part = new UVoicePart { position = 480, trackNo = 0 };
            var state = new MidiInputState(TuningPresets.Default);
            var take = new List<MidiInputNote>();
            state.NoteEnded += take.Add;
            state.NoteOn(0, 60, 100, 1000);
            state.AllNotesOff(1000);
            var notes = MidiInputRecording.BuildNotes(project, part, take, 1000,
                project.timeAxis.TickPosToMsPos(720));
            Assert.Single(notes);
            Assert.Equal(240, notes[0].position);
            Assert.Equal(10, notes[0].duration);
        }

        [Fact]
        public void RecordingRetainsOutOfRangeMappedPitchWithoutClamping() {
            var tuning = TuningPresets.Default.Clone();
            tuning.InputMapping = new InputMapping { IsScala = true, MapSize = 0, MiddleMidi = 0 };
            var state = new MidiInputState(tuning);
            var take = new List<MidiInputNote>();
            state.NoteEnded += take.Add;
            state.NoteOn(0, 127, 100, 0);
            state.NoteOff(0, 127, 100);
            var notes = MidiInputRecording.BuildNotes(new UProject(),
                new UVoicePart { trackNo = 0 }, take, 0, 0);
            Assert.Single(notes);
            Assert.Equal(187, notes[0].tone);
            Assert.Equal(127, notes[0].pitchAddress!.Degree);
        }
    }
}
