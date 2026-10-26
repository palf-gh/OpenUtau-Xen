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
        static readonly ConcurrentDictionary<int, KeyboardMos?> searchCache = new ConcurrentDictionary<int, KeyboardMos?>();
        static readonly ConcurrentDictionary<(int, int, int, int, int), int[]?> mosCache = new ConcurrentDictionary<(int, int, int, int, int), int[]?>();

        /// <summary>Roles per degree index, or null when the tuning has no valid piano-style theory.</summary>
        public static KeyRole[]? Roles(TuningDefinition tuning) {
            return Model(tuning)?.Roles;
        }

        /// <summary>
        /// The keyboard's MOS model: a letter-named keyboard MOS, or the seven-note octave / nine-note tritave
        /// family baseline. Null for manual keyboards, unavailable MOS settings and tunings without a family.
        /// </summary>
        public static MosKeyboard? Model(TuningDefinition tuning) {
            if (IsManual(tuning) || IsUnavailableMos(tuning)) return null;
            var letters = LetterNominals(tuning);
            if (letters != null) {
                if (letterCache.Count > 64) letterCache.Clear();
                int n = tuning.DegreeCount;
                var mos = EffectiveMos(tuning)!;
                return letterCache.GetOrAdd(n + ":" + string.Join(",", letters),
                    _ => MosKeyboard.FromNominals(n, letters, mos.Generator, mos.Mode));
            }
            var theory = KeyboardTheory.For(tuning);
            if (theory == null || !theory.IsValid) return null;
            return cache.GetOrAdd((theory.Divisions, theory.ReferenceDivision, theory.Generator), _ => Baseline(theory));
        }

        /// <summary>The key-by-key layout: every key comes from the explicit classes.</summary>
        public static bool IsManual(TuningDefinition tuning) => tuning.Keyboard.Layout == KeyboardLayout.ManualLayout;

        /// <summary>The scale degrees of a keyboard MOS, or null when the settings do not give a MOS.</summary>
        public static int[]? MosDegrees(TuningDefinition tuning, KeyboardMos mos) {
            int n = tuning.DegreeCount;
            if (mos.Cardinality < 1 || mos.Cardinality > n) return null;
            // Step sizes are counted in degrees, so the result depends only on these five numbers.
            if (mosCache.Count > 256) mosCache.Clear();
            int periods = mos.Periods ?? 1;
            return mosCache.GetOrAdd((n, mos.Generator, mos.Cardinality, mos.Mode, periods), _ => {
                try {
                    var result = MosGenerator.Generate(tuning, mos.Generator, mos.Cardinality, mos.Mode, periods);
                    return result.IsMos || result.IsEqualStep ? result.Scale.Degrees.ToArray() : null;
                } catch (ArgumentException) {
                    return null;
                }
            });
        }

        /// <summary>A MOS keyboard whose settings give no MOS: every key is white and shows its degree number.</summary>
        public static bool IsUnavailableMos(TuningDefinition tuning) =>
            !IsManual(tuning) && EffectiveMos(tuning) is KeyboardMos mos && MosDegrees(tuning, mos) == null;

        /// <summary>
        /// The MOS that builds a MOS keyboard: the stored one, or for tunings without a family keyboard the default
        /// MOS (generator nearest 3/2, seven notes). Null means the family baseline (or, for unequal tunings, names).
        /// </summary>
        public static KeyboardMos? EffectiveMos(TuningDefinition tuning) {
            if (tuning.Keyboard.Mos != null) return tuning.Keyboard.Mos;
            var theory = KeyboardTheory.For(tuning);
            if (theory != null && theory.IsValid || tuning.Divisions == 0) return null;
            return DefaultMos(tuning);
        }

        /// <summary>
        /// The keyboard MOS used when none is stored: the family MOS, or for other equal divisions the best-scoring
        /// MOS (see <see cref="SearchMos"/>), or seven notes from the generator nearest 3/2.
        /// </summary>
        public static KeyboardMos DefaultMos(TuningDefinition tuning) {
            var theory = KeyboardTheory.For(tuning);
            if (theory != null && theory.IsValid) return new KeyboardMos { Generator = theory.Generator, Cardinality = theory.Naturals.Count };
            if (tuning.Divisions > 0 && searchCache.GetOrAdd(tuning.Divisions, SearchMos) is KeyboardMos found) return found.Clone();
            // Up to nine degrees the tuning is already a small scale: every degree is a white key.
            int count = tuning.DegreeCount < 10 ? tuning.DegreeCount : 7;
            return new KeyboardMos { Generator = DefaultGenerator(tuning), Cardinality = count };
        }

        /// <summary>
        /// Chooses a MOS for an equal division without a family keyboard: five to nine notes with two step sizes,
        /// preferring few in-between keys (every degree a white or black key), seven notes, a large/small ratio
        /// near 7:4 and a generator near 3/2 or 4/3. This gives porcupine 1L6s in 15-EDO (seven white and eight
        /// black keys), mavila 2L5s in 16-EDO and 3L4s in 10-EDO. Null when no such MOS exists.
        /// </summary>
        static KeyboardMos? SearchMos(int n) {
            if (n < 10 || n > 4096) return null;
            KeyboardMos? best = null;
            double bestScore = double.MaxValue;
            double fifth = 1200 * Math.Log2(1.5), fourth = 1200 - fifth;
            for (int g = 1; g < n; g++) {
                double cents = 1200.0 * g / n;
                double distance = new[] { fifth, fourth }.Min(target => Math.Abs(cents - target));
                for (int cardinality = 5; cardinality <= Math.Min(9, n - 1); cardinality++) {
                    var orbit = Enumerable.Range(0, cardinality).Select(i => (int)((long)i * g % n)).Distinct().OrderBy(p => p).ToArray();
                    if (orbit.Length != cardinality) break;
                    var steps = orbit.Select((p, i) => (i + 1 < orbit.Length ? orbit[i + 1] : n) - p).ToArray();
                    int large = steps.Max(), small = steps.Min();
                    if (large == small || steps.Any(step => step != large && step != small)) continue;
                    // As in MosKeyboard: steps with accidentals hold a sharp and a flat key (one shared key when they
                    // coincide); the remaining degrees are in-between keys.
                    int chroma = large - small;
                    int black = steps.Where(step => MosKeyboard.HasAccidentals(step, large, chroma))
                        .Sum(step => chroma == step - chroma ? 1 : 2);
                    double between = (double)(n - cardinality - black) / n;
                    double score = 2 * Math.Abs(cardinality - 7) + 3 * Math.Abs(Math.Log((double)large / small) - Math.Log(1.75))
                        + distance / 300 + 4 * between;
                    if (score < bestScore) {
                        bestScore = score;
                        best = new KeyboardMos { Generator = g, Cardinality = cardinality };
                    }
                }
            }
            return best;
        }

        /// <summary>
        /// A mode of the seven-note octave or nine-note tritave family (or a fifth-generated seven-note scale
        /// in an unequal octave), which keeps the conventional names and keys rather than letters.
        /// </summary>
        public static bool IsFamilyMos(TuningDefinition tuning, KeyboardMos mos) {
            // Family keyboards repeat once per equave; a multi-period MOS always takes letter names.
            if ((mos.Periods ?? 1) != 1) return false;
            int n = tuning.DegreeCount;
            int g = (mos.Generator % n + n) % n;
            var theory = KeyboardTheory.For(tuning);
            if (theory != null && theory.IsValid && mos.Cardinality == theory.Naturals.Count) {
                return g == theory.Generator % n || g == (n - theory.Generator % n) % n;
            }
            if (tuning.Divisions == 0 && mos.Cardinality == 7 && Math.Abs(tuning.PeriodCents - 1200) < 1e-6) {
                double cents = tuning.GetCents(g);
                return Math.Abs(cents - 702) < 30 || Math.Abs(cents - 498) < 30;
            }
            return false;
        }

        /// <summary>
        /// Nominal degrees of a keyboard MOS whose notes take letter names A, B, C … (skipping I); null when the
        /// keyboard is manual, has no MOS setting, the MOS is unavailable, or it belongs to a family.
        /// </summary>
        public static int[]? LetterNominals(TuningDefinition tuning) {
            var mos = EffectiveMos(tuning);
            if (IsManual(tuning) || mos == null || IsFamilyMos(tuning, mos)) return null;
            var degrees = MosDegrees(tuning, mos);
            if (degrees == null) return null;
            int n = tuning.DegreeCount;
            // The keyboard geometry counts degrees, so more than two step sizes cannot be drawn as a MOS.
            if (degrees.Select((d, i) => (i + 1 < degrees.Length ? degrees[i + 1] : n) - d).Distinct().Count() > 2) return null;
            return degrees;
        }

        /// <summary>Key classes reproducing the tuning's current roles, used to start a key-by-key keyboard.</summary>
        public static Dictionary<int, string> ClassesFromRoles(TuningDefinition tuning) =>
            Enumerable.Range(0, tuning.DegreeCount).ToDictionary(degree => degree, degree => ClassName(PitchNotation.Role(tuning, degree)));

        public static string ClassName(KeyRole role) => role switch {
            KeyRole.Black => "accidental",
            KeyRole.Between => "between",
            _ => "natural",
        };

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
        /// Upgrades a format-1 keyboard: a class on every degree becomes a manual layout; otherwise the family
        /// MOS is used and partial classes that changed a key become a manual layout keeping that change.
        /// </summary>
        public static void UpgradeFormat1(TuningDefinition tuning) {
            var keyboard = tuning.Keyboard;
            keyboard.Tonic = 0;
            keyboard.Mos = null;
            int n = tuning.DegreeCount;
            if (Enumerable.Range(0, n).All(keyboard.Classes.ContainsKey)) {
                keyboard.Layout = KeyboardLayout.ManualLayout;
                return;
            }
            keyboard.Layout = KeyboardLayout.MosLayout;
            if (keyboard.Classes.Count == 0) return;
            var classes = keyboard.Classes;
            keyboard.Classes = new Dictionary<int, string>();
            var baseline = Enumerable.Range(0, n).Select(degree => PitchNotation.Role(tuning, degree)).ToArray();
            bool changed = classes.Any(pair => pair.Key >= 0 && pair.Key < n && PitchNotation.ClassRole(pair.Value) != baseline[pair.Key]);
            if (!changed) return;
            keyboard.Layout = KeyboardLayout.ManualLayout;
            keyboard.Classes = Enumerable.Range(0, n).ToDictionary(degree => degree,
                degree => classes.TryGetValue(degree, out var keyClass) ? keyClass : ClassName(baseline[degree]));
        }

        /// <summary>
        /// Carries upstream's twelve-key project key ("1=D" is 2) into the tonic of a twelve-note octave tuning,
        /// for projects saved before the tonic existed.
        /// </summary>
        public static void ApplyUpstreamKey(TuningDefinition? tuning, int key) {
            if (tuning == null || tuning.DegreeCount != 12 || Math.Abs(tuning.PeriodCents - 1200) > 1e-6) return;
            tuning.Keyboard.Tonic = (key % 12 + 12) % 12;
        }

        /// <summary>Piano-roll scale rows: the active scale counted from the tonic.</summary>
        public static IReadOnlyList<int>? DisplayDegrees(TuningDefinition tuning) => ScalePresets.ActiveDegrees(tuning);

        /// <summary>
        /// The letter of nominal <paramref name="index"/> among <paramref name="count"/>: the first count letters
        /// (I skipped), rotated so degree 0 is C as on conventional keyboards — C D E F G A B for seven notes,
        /// C D E F G H J A B for nine (as Bohlen–Pierce), C D E A B for five. Fewer than three notes start at A.
        /// </summary>
        public static string LetterName(int index, int count) =>
            LetterName(count < 3 ? index : (index + 2) % count);

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
