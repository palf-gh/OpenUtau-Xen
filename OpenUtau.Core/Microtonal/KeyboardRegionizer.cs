using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace OpenUtau.Core.Microtonal {
    public enum KeyRole { White, Black, Between }

    /// <summary>
    /// Theory behind a piano-style keyboard for one equal division: natural anchors from a generator,
    /// and a chromatic alteration splitting each large step into sharp and flat anchors.
    /// See docs/microtonal_keyboard_algorithm_spec.md.
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
        static readonly ConcurrentDictionary<(int, int, int), KeyRole[]?> cache = new ConcurrentDictionary<(int, int, int), KeyRole[]?>();

        /// <summary>Roles per degree index, or null when the tuning has no valid piano-style theory.</summary>
        public static KeyRole[]? Roles(TuningDefinition tuning) {
            var theory = KeyboardTheory.For(tuning);
            if (theory == null) return null;
            return cache.GetOrAdd((theory.Divisions, theory.ReferenceDivision, theory.Generator),
                _ => theory.IsValid ? Regionize(theory) : null);
        }

        public static KeyRole[] Regionize(KeyboardTheory theory) {
            if (!theory.IsValid) throw new ArgumentException("The theory has no positive large, small and chromatic steps.", nameof(theory));
            int n = theory.Divisions;
            int Mod(int value) => (value % n + n) % n;
            var white = new HashSet<int>(theory.Naturals.Select(Mod));
            var black = new HashSet<int>();
            var pairs = new List<(int Low, int High)>();
            foreach (int gap in theory.LargeGaps) {
                int x = theory.Naturals[gap];
                int sharp = x + theory.Chromatic, flat = x + theory.Small;
                black.Add(Mod(sharp));
                black.Add(Mod(flat));
                pairs.Add((Math.Min(sharp, flat), Math.Max(sharp, flat)));
            }
            var roles = Enumerable.Repeat(KeyRole.Between, n).ToArray();
            foreach (int step in black) roles[step] = KeyRole.Black;
            foreach (int step in white) roles[step] = KeyRole.White;
            // A White and a Black exact anchor on one step is a conflict, never silently resolved.
            foreach (int step in white.Intersect(black)) roles[step] = KeyRole.Between;
            var anchors = new HashSet<int>(white.Union(black));

            int radius = n / (2 * theory.ReferenceDivision);
            if (radius > 0) {
                int Distance(int a, int b) { int d = Math.Abs(Mod(a - b)); return Math.Min(d, n - d); }
                var dilated = (KeyRole[])roles.Clone();
                for (int step = 0; step < n; step++) {
                    if (anchors.Contains(step)) continue;
                    int whiteDistance = white.Where(a => !black.Contains(a)).Select(a => Distance(step, a)).DefaultIfEmpty(int.MaxValue).Min();
                    int blackDistance = black.Where(a => !white.Contains(a)).Select(a => Distance(step, a)).DefaultIfEmpty(int.MaxValue).Min();
                    bool nearWhite = whiteDistance <= radius, nearBlack = blackDistance <= radius;
                    dilated[step] = nearWhite && nearBlack
                        ? whiteDistance < blackDistance ? KeyRole.White : blackDistance < whiteDistance ? KeyRole.Black : KeyRole.Between
                        : nearWhite ? KeyRole.White : nearBlack ? KeyRole.Black : KeyRole.Between;
                }
                roles = dilated;
            }

            // Short-gap closure: within one large step, join the sharp and flat regions when at most one
            // Between step separates them. Natural regions are never bridged.
            foreach (var (low, high) in pairs) {
                if (low == high) continue;
                int top = low;
                while (top + 1 < high && roles[Mod(top + 1)] == KeyRole.Black) top++;
                int bottom = high;
                while (bottom - 1 > top && roles[Mod(bottom - 1)] == KeyRole.Black) bottom--;
                int between = bottom - top - 1;
                if (between == 1 && roles[Mod(top + 1)] == KeyRole.Between) roles[Mod(top + 1)] = KeyRole.Black;
            }
            return roles;
        }
    }
}
