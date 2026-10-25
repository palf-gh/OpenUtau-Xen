using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenUtau.Core.Microtonal {
    public sealed class KeyboardPitchCell {
        public int Step { get; }
        public KeyRole Role { get; }
        public NotchedKeyKind Kind { get; }
        public int Owner { get; }
        public int? AnchorStep { get; }
        public bool IsAnchor => AnchorStep == Step;

        public KeyboardPitchCell(int step, NotchedKeyKind kind, int owner = -1, int? anchorStep = null) {
            Step = step; Kind = kind; Owner = owner; AnchorStep = anchorStep;
            Role = kind == NotchedKeyKind.Natural ? KeyRole.White
                : kind == NotchedKeyKind.Flat || kind == NotchedKeyKind.Sharp || kind == NotchedKeyKind.Shared
                    ? KeyRole.Black : KeyRole.Between;
        }
    }

    /// <summary>
    /// Equal-step MOS theory followed by the central-third keyboard heuristic.
    /// Equave frequency, note names and the performance-scale selection do not affect the geometry.
    /// </summary>
    public sealed class MosKeyboard {
        public int Divisions { get; }
        public int Generator { get; }
        public int Rotation { get; }
        public int Large { get; }
        public int Small { get; }
        public bool IsEqualStep => Large == Small;
        public IReadOnlyList<int> Nominals { get; }
        public IReadOnlyList<int> Gaps { get; }
        public IReadOnlyList<KeyboardPitchCell> Cells { get; }
        internal KeyRole[] Roles { get; }

        MosKeyboard(int divisions, int generator, int rotation, int[] nominals, int[] gaps, KeyboardPitchCell[] cells) {
            Divisions = divisions; Generator = generator; Rotation = rotation;
            Large = gaps.Max(); Small = gaps.Min();
            Nominals = Array.AsReadOnly(nominals); Gaps = Array.AsReadOnly(gaps); Cells = Array.AsReadOnly(cells);
            Roles = cells.Select(cell => cell.Role).ToArray();
        }

        public static int[] NominalSteps(int divisions, int generator, int cardinality, int rotation = 0) {
            if (divisions < 1 || divisions > 65536) throw new ArgumentOutOfRangeException(nameof(divisions));
            if (cardinality < 1 || cardinality > divisions) throw new ArgumentOutOfRangeException(nameof(cardinality));
            int Mod(long value) => (int)((value % divisions + divisions) % divisions);
            var orbit = Enumerable.Range(0, cardinality).Select(i => Mod((long)i * generator)).Distinct().OrderBy(p => p).ToArray();
            if (orbit.Length != cardinality) throw new ArgumentException("The generator repeats before the requested cardinality.", nameof(generator));
            rotation = (rotation % cardinality + cardinality) % cardinality;
            int origin = orbit[rotation];
            // Subtracting the rotated origin is the cumulative sum of the cyclically rotated gap pattern.
            return orbit.Select(p => Mod((long)p - origin)).OrderBy(p => p).ToArray();
        }

        /// <summary>
        /// Whether a step holds the lower nominal's sharp and the upper nominal's flat as black keys: always in a
        /// large step; in a small step only when the sharp is not above the flat (twice the chroma at most the step),
        /// so 19-EDO E-sharp/F-flat and porcupine 15-EDO share one black key, while 31-EDO's crossed E-sharp and
        /// F-flat stay in-between keys.
        /// </summary>
        public static bool HasAccidentals(int step, int large, int chroma) =>
            chroma > 0 && (step == large ? chroma < step : 2 * chroma <= step);

        public static MosKeyboard Generate(int divisions, int generator, int cardinality, int rotation = 0) =>
            FromNominals(divisions, NominalSteps(divisions, generator, cardinality, rotation), generator,
                (rotation % cardinality + cardinality) % cardinality);

        /// <summary>The keyboard for nominal degrees already chosen elsewhere, such as a generated scale starting at 0.</summary>
        public static MosKeyboard FromNominals(int divisions, IReadOnlyList<int> nominals, int generator = 0, int rotation = 0) {
            if (divisions < 1 || divisions > 65536) throw new ArgumentOutOfRangeException(nameof(divisions));
            var nominal = nominals.Select(p => (p % divisions + divisions) % divisions).Distinct().OrderBy(p => p).ToArray();
            if (nominal.Length == 0) throw new ArgumentException("At least one nominal is required.", nameof(nominals));
            var gaps = nominal.Select((p, i) => (i + 1 < nominal.Length ? nominal[i + 1] : divisions) - p).ToArray();
            var sizes = gaps.Distinct().OrderBy(gap => gap).ToArray();
            if (sizes.Length > 2) throw new ArgumentException("The nominals have more than two gap sizes.", nameof(nominals));
            int small = sizes[0], large = sizes[sizes.Length - 1], chroma = large - small;
            var anchors = new SortedDictionary<int, KeyboardPitchCell>();
            int Mod(int value) => (value % divisions + divisions) % divisions;
            void Add(int step, NotchedKeyKind kind, int owner) {
                step = Mod(step);
                if (anchors.TryGetValue(step, out var existing)) {
                    bool white = existing.Kind == NotchedKeyKind.Natural || kind == NotchedKeyKind.Natural;
                    kind = white ? NotchedKeyKind.Conflict : NotchedKeyKind.Shared;
                }
                anchors[step] = new KeyboardPitchCell(step, kind, owner, step);
            }
            for (int i = 0; i < nominal.Length; i++) Add(nominal[i], NotchedKeyKind.Natural, i);
            if (chroma > 0) {
                for (int i = 0; i < nominal.Length; i++) {
                    if (!HasAccidentals(gaps[i], large, chroma)) continue;
                    Add(nominal[i] + chroma, NotchedKeyKind.Sharp, i);
                    Add(nominal[i] + gaps[i] - chroma, NotchedKeyKind.Flat, i);
                }
            }
            var cells = Enumerable.Range(0, divisions).Select(step => new KeyboardPitchCell(step, NotchedKeyKind.Between)).ToArray();
            var ordered = anchors.Values.ToArray();
            foreach (var anchor in ordered) cells[anchor.Step] = anchor;
            for (int i = 0; i < ordered.Length; i++) {
                var left = ordered[i];
                var right = ordered[(i + 1) % ordered.Length];
                int distance = right.Step - left.Step;
                if (distance <= 0) distance += divisions;
                // Natural-to-natural interiors remain Between, including short diatonic gaps.
                if (left.Role == KeyRole.White && right.Role == KeyRole.White) continue;
                for (int offset = 1; offset < distance; offset++) {
                    // Integer comparisons keep the exact one-third and two-thirds boundaries Between.
                    var owner = 3 * offset < distance ? left : 3 * offset > 2 * distance ? right : null;
                    if (owner == null || owner.Role == KeyRole.Between) continue;
                    int step = Mod(left.Step + offset);
                    cells[step] = new KeyboardPitchCell(step, owner.Kind, owner.Owner, owner.Step);
                }
            }
            return new MosKeyboard(divisions, generator, rotation, nominal, gaps, cells);
        }
    }
}
