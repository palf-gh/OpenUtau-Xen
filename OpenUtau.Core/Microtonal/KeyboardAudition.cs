using System;

namespace OpenUtau.Core.Microtonal {
    /// <summary>One held preview voice, independent of audio-device initialisation.</summary>
    public sealed class KeyboardAudition {
        readonly Action<double> playTone;
        readonly Action<double> endTone;
        public bool PointerHeld { get; private set; }
        public bool KeyboardHeld { get; private set; }
        public double? Frequency { get; private set; }

        public KeyboardAudition(Action<double> playTone, Action<double> endTone) {
            this.playTone = playTone ?? throw new ArgumentNullException(nameof(playTone));
            this.endTone = endTone ?? throw new ArgumentNullException(nameof(endTone));
        }

        public void BeginPointer(TuningDefinition? tuning, int? degree) {
            PointerHeld = true;
            Update(tuning, degree);
        }

        public void BeginKeyboard(TuningDefinition? tuning, int? degree) {
            KeyboardHeld = true;
            Update(tuning, degree);
        }

        public void SetSelection(TuningDefinition? tuning, int? degree) {
            if (PointerHeld || KeyboardHeld) Update(tuning, degree);
        }

        public void EndPointer() {
            PointerHeld = false;
            if (!KeyboardHeld) StopTone();
        }

        public void EndKeyboard() {
            KeyboardHeld = false;
            if (!PointerHeld) StopTone();
        }

        public void Cancel() {
            PointerHeld = false;
            KeyboardHeld = false;
            StopTone();
        }

        void Update(TuningDefinition? tuning, int? degree) {
            try {
                if (tuning == null || degree == null) {
                    StopTone();
                    return;
                }
                double next = PitchResolver.ResolveFrequency(tuning, new PitchAddress { Degree = degree.Value });
                if (!double.IsFinite(next) || next <= 0) {
                    StopTone();
                    return;
                }
                if (Frequency == next) return;
                StopTone();
                Frequency = next;
                playTone(next);
            } catch {
                PointerHeld = false;
                KeyboardHeld = false;
                StopTone();
                throw;
            }
        }

        void StopTone() {
            if (Frequency is not double frequency) return;
            Frequency = null;
            endTone(frequency);
        }
    }
}
