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
        static readonly ConcurrentDictionary<string, MosKeyboard> letterCache = new ConcurrentDictionary<string, MosKeyboard>();

        /// <summary>Roles per degree index, or null when the tuning has no valid piano-style theory.</summary>
        public static KeyRole[]? Roles(TuningDefinition tuning) {
            return Model(tuning)?.Roles;
        }

        /// <summary>
        /// The tuning's baseline MOS. An active generator scale outside the seven-note octave and nine-note
        /// tritave families supplies its own nominals; otherwise the family baseline is used.
        /// </summary>
        public static MosKeyboard? Model(TuningDefinition tuning) {
            var letters = LetterScale(tuning);
            if (letters != null) {
                if (letterCache.Count > 64) letterCache.Clear();
                return letterCache.GetOrAdd(LetterKey(tuning, letters), _ => MosKeyboard.FromNominals(tuning.DegreeCount, letters.Degrees,
                    letters.GeneratorDegree ?? 0, letters.Mode));
            }
            if (IsManual(tuning)) return null;
            var theory = KeyboardTheory.For(tuning);
            if (theory == null || !theory.IsValid) return null;
            return cache.GetOrAdd((theory.Divisions, theory.ReferenceDivision, theory.Generator), _ => Baseline(theory));
        }

        /// <summary>
        /// The active generator scale whose nominals take letter names A, B, C … (skipping I), or null when
        /// the tuning keeps its family names: MOS switched off, no generator scale, more than two step sizes
        /// (counted in degrees), or a mode of the seven-note octave or nine-note tritave family.
        /// </summary>
        public static ScaleDefinition? LetterScale(TuningDefinition tuning) {
            int n = tuning.DegreeCount;
            if (n <= 0 || IsManual(tuning)) return null;
            var scale = tuning.Scales.FirstOrDefault(s => s.Id == tuning.Keyboard.ActiveScaleId);
            if (scale?.GeneratorDegree is not int generator || scale.Degrees.Count == 0) return null;
            var degrees = scale.Degrees.Select(d => (d % n + n) % n).Distinct().OrderBy(d => d).ToArray();
            if (degrees.Length != scale.Degrees.Count || degrees[0] != 0) return null;
            if (degrees.Select((d, i) => (i + 1 < degrees.Length ? degrees[i + 1] : n) - d).Distinct().Count() > 2) return null;
            var theory = KeyboardTheory.For(tuning);
            if (theory != null && theory.IsValid && degrees.Length == theory.Naturals.Count) {
                int g = (generator % n + n) % n;
                if (g == theory.Generator % n || g == (n - theory.Generator % n) % n) return null;
            }
            // Unequal octave tunings with a fifth-generated seven-note scale keep their conventional names.
            if (tuning.Divisions == 0 && degrees.Length == 7 && Math.Abs(tuning.PeriodCents - 1200) < 1e-6) {
                double cents = tuning.GetCents((generator % n + n) % n);
                if (Math.Abs(cents - 702) < 30 || Math.Abs(cents - 498) < 30) return null;
            }
            return scale;
        }

        /// <summary>Key classes reproducing the tuning's current roles, used to start a key-by-key keyboard.</summary>
        public static Dictionary<int, string> ClassesFromRoles(TuningDefinition tuning) =>
            Enumerable.Range(0, tuning.DegreeCount).ToDictionary(degree => degree, degree => PitchNotation.Role(tuning, degree) switch {
                KeyRole.Black => "accidental",
                KeyRole.Between => "between",
                _ => "natural",
            });

        /// <summary>
        /// The default MOS generator: the family generator, or for other tunings the degree nearest
        /// the same fraction of the period as 3/2 is of the octave.
        /// </summary>
        public static int DefaultGenerator(TuningDefinition tuning) {
            var theory = KeyboardTheory.For(tuning);
            if (theory != null) return theory.Generator;
            double target = tuning.PeriodCents * Math.Log2(1.5);
            return Enumerable.Range(1, Math.Max(1, tuning.DegreeCount - 1))
                .OrderBy(degree => Math.Abs(tuning.GetCents(degree) - target)).First();
        }

        /// <summary>
        /// Piano-roll scale rows. A generator scale also shows every white and black key of the keyboard,
        /// so chromatic alterations such as semitones stay visible and only in-between microsteps are left out.
        /// </summary>
        public static IReadOnlyList<int>? DisplayDegrees(TuningDefinition tuning) {
            var scale = tuning.Scales.FirstOrDefault(s => s.Id == tuning.Keyboard.ActiveScaleId);
            if (scale == null || scale.GeneratorDegree == null) return scale?.Degrees;
            int n = tuning.DegreeCount;
            return scale.Degrees.Select(d => (d % n + n) % n)
                .Concat(Enumerable.Range(0, n).Where(d => PitchNotation.Role(tuning, d) != KeyRole.Between))
                .Distinct().OrderBy(d => d).ToList();
        }

        /// <summary>
        /// MOS switched off: every degree has an explicit key class, so the keyboard is set key by key
        /// and neither a MOS nor a family baseline applies.
        /// </summary>
        public static bool IsManual(TuningDefinition tuning) {
            var classes = tuning.Keyboard.Classes;
            return classes.Count >= tuning.DegreeCount && Enumerable.Range(0, tuning.DegreeCount).All(classes.ContainsKey);
        }

        static string LetterKey(TuningDefinition tuning, ScaleDefinition scale) {
            int n = tuning.DegreeCount;
            return n + ":" + string.Join(",", scale.Degrees.Select(d => (d % n + n) % n).OrderBy(d => d));
        }

        /// <summary>Letter nominal names in order: A–H, J–Z, then doubled letters.</summary>
        public static string LetterName(int index) {
            const string letters = "ABCDEFGHJKLMNOPQRSTUVWXYZ";
            return index < letters.Length ? letters[index].ToString()
                : letters[(index / letters.Length - 1) % letters.Length].ToString() + letters[index % letters.Length];
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
