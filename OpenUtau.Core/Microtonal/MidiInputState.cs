using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenUtau.Core.Microtonal {
    /// <summary>A mapped note, captured at key-down so later setting changes cannot alter its pitch.</summary>
    public sealed class MidiInputNote {
        public long Id { get; internal set; }
        public int Channel { get; internal set; }
        public int Key { get; internal set; }
        public int Velocity { get; internal set; }
        public PitchAddress Address { get; internal set; } = new PitchAddress();
        public double MidiTone { get; internal set; }
        public double StartMilliseconds { get; internal set; }
        public double EndMilliseconds { get; internal set; }
        internal bool Released { get; set; }
    }

    /// <summary>
    /// Device-independent MIDI note lifecycle. Times use one monotonic clock supplied by the caller.
    /// Callbacks run synchronously; a UI subscriber must marshal them onto its own thread.
    /// </summary>
    public sealed class MidiInputState {
        readonly object sync = new object();
        readonly Dictionary<(int channel, int key), MidiInputNote> notes = new();
        readonly bool[] sustain = new bool[16];
        readonly TuningDefinition tuning;
        long nextId;
        int? channel;

        public event Action<MidiInputNote>? NoteStarted;
        public event Action<MidiInputNote>? NoteEnded;

        public MidiInputState(TuningDefinition tuning, int? channel = null) {
            this.tuning = tuning.Clone();
            ValidateChannel(channel);
            this.channel = channel;
        }

        public int ActiveCount { get { lock (sync) { return notes.Count; } } }

        public void SetChannel(int? channel, double milliseconds) {
            ValidateChannel(channel);
            lock (sync) {
                AllNotesOff(milliseconds);
                this.channel = channel;
            }
        }

        static void ValidateChannel(int? value) {
            if (value.HasValue && (value.Value < 0 || value.Value > 15)) {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        static void ValidateTime(double milliseconds) {
            if (!double.IsFinite(milliseconds) || milliseconds < 0) {
                throw new ArgumentOutOfRangeException(nameof(milliseconds));
            }
        }

        public void NoteOn(int channel, int key, int velocity, double milliseconds) {
            ValidateChannel(channel);
            ValidateTime(milliseconds);
            if (key < 0 || key > 127 || velocity < 0 || velocity > 127) {
                throw new ArgumentOutOfRangeException(nameof(key));
            }
            if (velocity == 0) {
                NoteOff(channel, key, milliseconds);
                return;
            }
            lock (sync) {
                if (this.channel.HasValue && this.channel != channel) { return; }
                var address = PitchResolver.MapMidi(tuning, key);
                if (address == null) { return; }
                var identity = (channel, key);
                // Repeated key-down retriggers, closing the previous note even under sustain.
                if (notes.TryGetValue(identity, out var previous)) { End(previous, milliseconds); }
                var note = new MidiInputNote {
                    Id = ++nextId,
                    Channel = channel,
                    Key = key,
                    Velocity = velocity,
                    Address = address,
                    MidiTone = PitchResolver.ResolveMidi(tuning, address),
                    StartMilliseconds = milliseconds,
                };
                notes.Add(identity, note);
                NoteStarted?.Invoke(note);
            }
        }

        public void NoteOff(int channel, int key, double milliseconds) {
            ValidateChannel(channel);
            ValidateTime(milliseconds);
            lock (sync) {
                if (notes.TryGetValue((channel, key), out var note)) {
                    if (sustain[channel]) { note.Released = true; }
                    else { End(note, milliseconds); }
                }
            }
        }

        public void ControlChange(int channel, int controller, int value, double milliseconds) {
            ValidateChannel(channel);
            ValidateTime(milliseconds);
            lock (sync) {
                if (this.channel.HasValue && this.channel != channel) { return; }
                if (controller == 64) {
                    sustain[channel] = value >= 64;
                    if (!sustain[channel]) {
                        foreach (var note in notes.Values.Where(n => n.Channel == channel && n.Released).ToArray()) {
                            End(note, milliseconds);
                        }
                    }
                } else if (controller == 120 || controller == 123) {
                    sustain[channel] = false;
                    foreach (var note in notes.Values.Where(n => n.Channel == channel).ToArray()) {
                        End(note, milliseconds);
                    }
                } else if (controller == 121) {
                    sustain[channel] = false;
                    foreach (var note in notes.Values.Where(n => n.Channel == channel && n.Released).ToArray()) {
                        End(note, milliseconds);
                    }
                }
            }
        }

        public void AllNotesOff(double milliseconds) {
            ValidateTime(milliseconds);
            lock (sync) {
                foreach (var note in notes.Values.ToArray()) { End(note, milliseconds); }
                Array.Clear(sustain, 0, sustain.Length);
            }
        }

        void End(MidiInputNote note, double milliseconds) {
            notes.Remove((note.Channel, note.Key));
            note.EndMilliseconds = Math.Max(note.StartMilliseconds, milliseconds);
            NoteEnded?.Invoke(note);
        }
    }
}
