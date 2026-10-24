using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenUtau.Core.Microtonal {
    /// <summary>Immutable, monotone mapping between physical MIDI pitch and displayed rows.</summary>
    public sealed class PitchRowMapper {
        readonly double[] midi;
        readonly int[] degrees;
        readonly HashSet<int> active;
        public int Count => midi.Length;
        public int RowCount => Count;
        public TuningDefinition Tuning { get; }
        public PitchDisplayMode Mode { get; }

        public PitchRowMapper(TuningDefinition tuning, PitchDisplayMode mode = PitchDisplayMode.Full,
            IEnumerable<int>? scaleDegrees = null, IEnumerable<int>? supplementaryDegrees = null,
            double minMidi = 0, double maxMidi = 127) {
            if (!double.IsFinite(minMidi) || !double.IsFinite(maxMidi) || minMidi >= maxMidi) {
                throw new ArgumentOutOfRangeException(nameof(minMidi));
            }
            Tuning = tuning.Clone();
            tuning = Tuning;
            Mode = mode;
            int count = tuning.DegreeCount;
            active = new HashSet<int>((scaleDegrees ?? Enumerable.Range(0, count)).Select(d => Mod(d, count)));
            if (active.Count == 0) throw new ArgumentException("Select at least one scale degree.", nameof(scaleDegrees));
            var additional = new HashSet<int>(supplementaryDegrees ?? Array.Empty<int>());
            foreach (int degree in additional) {
                double tone = PitchResolver.ResolveMidi(tuning, new PitchAddress { Degree = degree });
                minMidi = Math.Min(minMidi, tone);
                maxMidi = Math.Max(maxMidi, tone);
            }
            int first = PitchResolver.NearestAddress(tuning, minMidi).Degree - count;
            int last = PitchResolver.NearestAddress(tuning, maxMidi).Degree + count;
            if ((long)last - first > 131072) throw new ArgumentOutOfRangeException(nameof(tuning), "The visible pitch range exceeds 131072 degrees.");
            var rows = new List<(int degree, double tone)>();
            for (int degree = first; degree <= last; degree++) {
                if (mode == PitchDisplayMode.Compact && !active.Contains(Mod(degree, count)) && !additional.Contains(degree)) continue;
                double tone = PitchResolver.ResolveMidi(tuning, new PitchAddress { Degree = degree });
                if (tone >= minMidi - 1e-9 && tone <= maxMidi + 1e-9) rows.Add((degree, tone));
            }
            if (rows.Count < 2) throw new ArgumentException("The selected scale has fewer than two visible pitches.", nameof(tuning));
            degrees = rows.Select(r => r.degree).ToArray();
            midi = rows.Select(r => r.tone).ToArray();
        }

        public int DegreeAtRow(int row) => degrees[Math.Clamp(row, 0, Count - 1)];
        public bool IsScaleRow(int row) => active.Contains(Mod(DegreeAtRow(row), Tuning.DegreeCount));
        public double MidiAtRow(double row) {
            int lower = Math.Clamp((int)Math.Floor(row), 0, Count - 2);
            return midi[lower] + (midi[lower + 1] - midi[lower]) * (row - lower);
        }
        public double RowAtMidi(double tone) {
            if (!double.IsFinite(tone)) return 0;
            int index = Array.BinarySearch(midi, tone);
            if (index >= 0) return index;
            int lower = Math.Clamp(~index - 1, 0, Count - 2);
            return lower + (tone - midi[lower]) / (midi[lower + 1] - midi[lower]);
        }
        public PitchAddress AddressAtRow(double row) => new PitchAddress {
            Degree = DegreeAtRow((int)Math.Round(row, MidpointRounding.AwayFromZero)),
        };
        static int Mod(int value, int divisor) => (value % divisor + divisor) % divisor;
    }
}
