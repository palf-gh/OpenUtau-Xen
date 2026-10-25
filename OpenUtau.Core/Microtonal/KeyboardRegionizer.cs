using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace OpenUtau.Core.Microtonal {
    public enum KeyRole { White, Black, Between }

    /// <summary>
    /// Theory behind a piano-style keyboard for one equal division: natural anchors from a generator,
    /// and a chromatic alteration splitting each large step into sharp and flat anchors.
    /// Family presets feed the general engine described in docs/microtonal/keyboard-mos.md.
    /// </summary>
    public sealed class KeyboardTheory {
        public int Divisions { get; }
        public int Generator { get; }
        public int Large { get; }
        public int Small { get; }
        public int Chromatic => Large - Small;
        public int ReferenceDivision { get; }
        public IReadOnlyList<string> NaturalNames { get; }
        public IReadOnlyList<int> Naturals { get; }
        /// <summary>Indices into <see cref="Naturals"/> whose following step is large.</summary>
        public IReadOnlyList<int> LargeGaps { get; }
        public bool IsValid => Large > 0 && Small > 0 && Chromatic > 0;

        KeyboardTheory(int divisions, int generator, int large, int small, int referenceDivision,
            string[] names, bool[] largePattern) {
            Divisions = divisions; Generator = generator; Large = large; Small = small;
            ReferenceDivision = referenceDivision; NaturalNames = names;
            var naturals = new List<int>();
            var gaps = new List<int>();
            int position = 0;
            for (int i = 0; i < largePattern.Length; i++) {
                naturals.Add(position);
                if (largePattern[i]) gaps.Add(i);
                position += largePattern[i] ? large : small;
            }
            Naturals = naturals; LargeGaps = gaps;
        }

        /// <summary>Octave family: generator nearest 3/2, C D E F G A B = L L s L L L s, reference 31-EDO.</summary>
        public static KeyboardTheory Octave(int divisions) {
            int g = (int)Math.Round(divisions * Math.Log2(1.5), MidpointRounding.AwayFromZero);
            return new KeyboardTheory(divisions, g, 2 * g - divisions, 3 * divisions - 5 * g, 31,
                new[] { "C", "D", "E", "F", "G", "A", "B" },
                new[] { true, true, false, true, true, true, false });
        }

        /// <summary>Tritave family: generator nearest 9/7, Lambda C D E F G H J A B = L s s L s L s L s, reference 13-EDT.</summary>
        public static KeyboardTheory Tritave(int divisions) {
            int g = (int)Math.Round(divisions * Math.Log(9.0 / 7) / Math.Log(3), MidpointRounding.AwayFromZero);
            return new KeyboardTheory(divisions, g, 5 * g - divisions, divisions - 4 * g, 13,
                new[] { "C", "D", "E", "F", "G", "H", "J", "A", "B" },
                new[] { true, false, false, true, false, true, false, true, false });
        }

        public static KeyboardTheory? For(TuningDefinition tuning) {
            if (tuning.Divisions <= 0) return null;
            double period = tuning.PeriodCents;
            if (Math.Abs(period - 1200) < 1e-8) return Octave(tuning.Divisions);
            if (Math.Abs(period - 1200 * Math.Log2(3)) < 1e-6) return Tritave(tuning.Divisions);
            return null;
        }
    }

    public static class KeyboardRegionizer {
        static readonly ConcurrentDictionary<(int, int, int), MosKeyboard> cache = new ConcurrentDictionary<(int, int, int), MosKeyboard>();

        /// <summary>Roles per degree index, or null when the tuning has no valid piano-style theory.</summary>
        public static KeyRole[]? Roles(TuningDefinition tuning) {
            return Model(tuning)?.Roles;
        }

        /// <summary>The tuning's baseline MOS, independent of its active performance scale.</summary>
        public static MosKeyboard? Model(TuningDefinition tuning) {
            var theory = KeyboardTheory.For(tuning);
            if (theory == null || !theory.IsValid) return null;
            return cache.GetOrAdd((theory.Divisions, theory.ReferenceDivision, theory.Generator), _ => Baseline(theory));
        }

        public static KeyRole[] Regionize(KeyboardTheory theory) => Baseline(theory).Roles;

        static MosKeyboard Baseline(KeyboardTheory theory) {
            if (!theory.IsValid) throw new ArgumentException("The theory has no positive large, small and chromatic steps.", nameof(theory));
            // Family definitions choose a baseline mode; both families then use the same orbit engine.
            // Retain C-diatonic and C-Lambda roots rather than assigning western names to a different mode.
            for (int rotation = 0; rotation < theory.Naturals.Count; rotation++) {
                if (MosKeyboard.NominalSteps(theory.Divisions, theory.Generator, theory.Naturals.Count, rotation)
                    .SequenceEqual(theory.Naturals)) {
                    return MosKeyboard.Generate(theory.Divisions, theory.Generator, theory.Naturals.Count, rotation);
                }
            }
            throw new ArgumentException("The baseline mode is not on the generator orbit.", nameof(theory));
        }
    }
}
