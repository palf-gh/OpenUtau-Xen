using System;

namespace OpenUtau.Core.Microtonal {
    /// <summary>Absolute MIDI pitch remains a 440 Hz coordinate; tuning reference is applied once here.</summary>
    public static class PitchResolver {
        static double ReferenceMidi(TuningDefinition tuning) {
            if (!double.IsFinite(tuning.Reference.Frequency) || tuning.Reference.Frequency <= 0)
                throw new ArgumentOutOfRangeException(nameof(tuning), "Reference frequency must be positive and finite.");
            return 69 + 12 * (Math.Log2(tuning.Reference.Frequency) - Math.Log2(440));
        }
        public static double ResolveMidi(TuningDefinition tuning, PitchAddress address) {
            if (!double.IsFinite(address.ResidualCents)) throw new ArgumentOutOfRangeException(nameof(address));
            return ReferenceMidi(tuning)
                + (tuning.GetCents(address.Degree) - tuning.GetCents(tuning.Reference.Degree) + address.ResidualCents) / 100;
        }
        public static double ResolveFrequency(TuningDefinition tuning, PitchAddress address) =>
            tuning.Reference.Frequency * Math.Pow(2,
                (ResolveMidi(tuning, address) - ReferenceMidi(tuning)) / 12);
        public static PitchAddress NearestAddress(TuningDefinition tuning, double midi, bool preserveFrequency = false) {
            if (!double.IsFinite(midi)) throw new ArgumentOutOfRangeException(nameof(midi));
            double cents = (midi - ReferenceMidi(tuning)) * 100
                + tuning.GetCents(tuning.Reference.Degree);
            int count = tuning.DegreeCount;
            double period = tuning.PeriodCents;
            long cycle = checked((long)Math.Floor(cents / period));
            if (cycle * count < int.MinValue + count || cycle * count > int.MaxValue - count)
                throw new ArgumentOutOfRangeException(nameof(midi), "Pitch exceeds the degree address range.");
            double local = cents - cycle * period;
            int low = 0, high = count;
            while (low < high) {
                int middle = low + (high - low) / 2;
                if (tuning.GetCents(middle) < local) low = middle + 1;
                else high = middle;
            }
            int upper = checked((int)(cycle * count + low));
            int lower = upper - 1;
            double lowerDistance = Math.Abs(cents - tuning.GetCents(lower));
            double upperDistance = Math.Abs(tuning.GetCents(upper) - cents);
            int degree = lowerDistance <= upperDistance + 1e-9 ? lower : upper;
            return new PitchAddress {
                Degree = degree,
                ResidualCents = preserveFrequency ? cents - tuning.GetCents(degree) : 0,
            };
        }
        public static int CompatibilityTone(double midi) {
            if (!double.IsFinite(midi)) throw new ArgumentOutOfRangeException(nameof(midi));
            // Ties go to the lower semitone, including negative coordinates.
            return checked((int)Math.Ceiling(midi - 0.5));
        }
        public static PitchAddress? MapMidi(TuningDefinition tuning, int midiKey) {
            var mapping = tuning.InputMapping;
            if (midiKey < 0 || midiKey > 127 || midiKey < mapping.FirstMidi || midiKey > mapping.LastMidi) return null;
            if (mapping.IsScala) {
                int? degree = mapping.MapDegree(midiKey);
                return degree.HasValue ? new PitchAddress { Degree = degree.Value } : null;
            }
            double midi = ReferenceMidi(tuning) + midiKey - mapping.ReferenceMidi;
            return NearestAddress(tuning, midi);
        }
    }
}
