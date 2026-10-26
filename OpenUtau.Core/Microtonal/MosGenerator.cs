using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenUtau.Core.Microtonal {
    public sealed class MosResult {
        public ScaleDefinition Scale { get; set; } = new ScaleDefinition();
        public bool IsMos { get; set; }
        public bool IsEqualStep { get; set; }
        public int LargeCount { get; set; }
        public int SmallCount { get; set; }
        public string Pattern { get; set; } = string.Empty;
    }
    /// <summary>A MOS that holds in a tuning, offered as a keyboard choice.</summary>
    public sealed class MosChoice {
        public KeyboardMos Mos { get; }
        public int LargeCount { get; }
        public int SmallCount { get; }
        /// <summary>Rothenberg propriety in degrees; see <see cref="MosGenerator.IsProper"/>.</summary>
        public bool IsProper { get; }
        public int Periods => Mos.Periods ?? 1;
        public MosChoice(KeyboardMos mos, int largeCount, int smallCount, bool isProper = true) {
            Mos = mos; LargeCount = largeCount; SmallCount = smallCount; IsProper = isProper;
        }
        /// <summary>Whether keyboard settings give this MOS: the same notes and repeats, and the generator or its inversion.</summary>
        public bool Matches(KeyboardMos mos, int degreeCount) {
            int repeats = mos.Periods ?? 1;
            if (mos.Cardinality != Mos.Cardinality || repeats != Periods || degreeCount % repeats != 0) return false;
            int period = degreeCount / repeats;
            int g = (mos.Generator % period + period) % period, own = Mos.Generator % period;
            return g == own || g == (period - own) % period;
        }
    }

    public static class MosGenerator {
        static readonly System.Collections.Concurrent.ConcurrentDictionary<(int, int, int), MosChoice[]> choiceCache =
            new System.Collections.Concurrent.ConcurrentDictionary<(int, int, int), MosChoice[]>();

        /// <summary>
        /// Every MOS with <paramref name="minNotes"/> to <paramref name="maxNotes"/> notes in a tuning of
        /// <paramref name="degreeCount"/> degrees, including multi-period MOS. Step sizes are counted in degrees, as in
        /// <see cref="Generate"/>. A generator and its inversion give the same MOS, so only the smaller one is listed.
        /// Two step sizes are not enough in an equal division, where sizes can coincide: 12-EDO's eight-note chain of
        /// fourths has steps sLLsssLL, so every interval class must also come in at most two sizes. Single-period
        /// chains of one degree are left out: they are the whole tuning with one gap. Sorted by note count, repeat
        /// count and generator. Empty above 4096 degrees.
        /// </summary>
        public static IReadOnlyList<MosChoice> Choices(int degreeCount, int minNotes = 4, int maxNotes = 24) {
            if (degreeCount < 2 || degreeCount > 4096) return Array.Empty<MosChoice>();
            if (choiceCache.Count > 64) choiceCache.Clear();
            return choiceCache.GetOrAdd((degreeCount, minNotes, maxNotes), _ => {
                var found = new List<MosChoice>();
                for (int repeats = 1; repeats <= degreeCount / 2; repeats++) {
                    if (degreeCount % repeats != 0) continue;
                    int period = degreeCount / repeats;
                    for (int g = 1; g <= period / 2; g++) {
                        if (g == 1 && repeats == 1) continue;
                        // A generator sharing a factor with the period stays inside a smaller cycle; it is still a MOS
                        // until the chain closes, as 24-EDO's 14-step fifth gives 5L2s.
                        int cycle = period / Gcd(g, period);
                        // The chain grows one generator at a time from one below the root, as in Generate.
                        var chain = new SortedSet<int> { period - g, 0 };
                        for (int perPeriod = 2; perPeriod < cycle && perPeriod * repeats <= maxNotes; perPeriod++) {
                            if (perPeriod > 2) chain.Add((int)((long)(perPeriod - 2) * g % period));
                            if (perPeriod * repeats < minNotes) continue;
                            var points = chain.ToArray();
                            var steps = points.Select((d, i) => (i + 1 < points.Length ? points[i + 1] : period) - d).ToArray();
                            int large = steps.Max(), small = steps.Min();
                            if (large == small || steps.Any(step => step != large && step != small) || !IsBalanced(steps)) continue;
                            found.Add(new MosChoice(
                                new KeyboardMos { Generator = g, Cardinality = perPeriod * repeats, Periods = repeats == 1 ? null : repeats },
                                steps.Count(step => step == large) * repeats, steps.Count(step => step == small) * repeats,
                                IsProper(steps)));
                        }
                    }
                }
                return found.OrderBy(c => c.Mos.Cardinality).ThenBy(c => c.Periods).ThenBy(c => c.Mos.Generator).ToArray();
            });
        }

        static int Gcd(int a, int b) { while (b != 0) (a, b) = (b, a % b); return a; }

        /// <summary>
        /// Rothenberg propriety: no interval of j steps is larger than one of j + 1 steps. Equal sizes are allowed, so
        /// the 12-EDO diatonic, whose tritone is both a fourth and a fifth, is proper. A Pythagorean-like diatonic is
        /// not: its augmented fourth (3L) is larger than its diminished fifth (2L + 2s), as in 17-, 22-, 41- and 53-EDO.
        /// One period of steps is enough, since the scale repeats.
        /// </summary>
        public static bool IsProper(IReadOnlyList<int> steps) {
            int k = steps.Count;
            // Prefix sums over two cycles give every cyclic run in constant time.
            var prefix = new long[2 * k + 1];
            for (int i = 0; i < 2 * k; i++) prefix[i + 1] = prefix[i] + steps[i % k];
            long previousMax = 0;
            for (int span = 1; span < k; span++) {
                long min = long.MaxValue, max = long.MinValue;
                for (int start = 0; start < k; start++) {
                    long sum = prefix[start + span] - prefix[start];
                    min = Math.Min(min, sum); max = Math.Max(max, sum);
                }
                if (min < previousMax) return false;
                previousMax = max;
            }
            return true;
        }

        /// <summary>Whether every run of j consecutive steps, cyclically, takes at most two sizes.</summary>
        static bool IsBalanced(int[] steps) {
            int k = steps.Length;
            for (int span = 1; span < k; span++) {
                int sum = 0;
                for (int i = 0; i < span; i++) sum += steps[i];
                int first = sum, second = -1;
                for (int start = 1; start < k; start++) {
                    sum += steps[(start + span - 1) % k] - steps[start - 1];
                    if (sum == first || sum == second) continue;
                    if (second >= 0) return false;
                    second = sum;
                }
            }
            return true;
        }

        /// <summary>
        /// A MOS whose generator chain repeats <paramref name="periods"/> times in the tuning period, such as 12-EDO 4L4s
        /// (diminished) or 22-EDO 2L8s (pajara). The generator is counted inside one period, the cardinality covers
        /// the whole tuning period and the mode is a rotation within one repeat. A period count that does not divide the
        /// degree count and the cardinality is rejected.
        /// </summary>
        public static MosResult Generate(TuningDefinition tuning, int generatorDegree, int cardinality, int mode = 0, int periods = 1) {
            tuning.Validate();
            int n = tuning.DegreeCount;
            if (cardinality <= 0 || cardinality > n) throw new ArgumentOutOfRangeException(nameof(cardinality));
            if (periods <= 0 || n % periods != 0 || cardinality % periods != 0) throw new ArgumentOutOfRangeException(nameof(periods));
            int period = n / periods, perPeriod = cardinality / periods;
            int Mod(long value) => (int)((value % period + period) % period);
            // Begin the chain one generator below the root: for seven fifth-generated notes this is F C G D A E B.
            var chain = Enumerable.Range(0, perPeriod).Select(i => Mod((long)(perPeriod == 1 ? 0 : i - 1) * generatorDegree)).Distinct().OrderBy(d => d).ToArray();
            if (chain.Length != perPeriod) throw new ArgumentException("The generator repeats before reaching the requested cardinality.");
            mode = (mode % perPeriod + perPeriod) % perPeriod;
            int origin = chain[mode];
            var degrees = Enumerable.Range(0, periods).SelectMany(k => chain.Select(p => Mod(p - origin) + k * period)).OrderBy(d => d).ToList();
            // Step sizes are counted in the tuning's degrees, as on the keyboard, so a fifth-generated diatonic
            // in twelve-note just intonation or a well temperament is still 5L2s although its cents vary.
            var widths = degrees.Select((d, i) => (double)((i + 1 < degrees.Count ? degrees[i + 1] : n) - d)).ToArray();
            var sizes = new List<double>();
            foreach (double width in widths) if (!sizes.Any(s => Math.Abs(s - width) <= 1e-6)) sizes.Add(width);
            sizes.Sort();
            bool isMos = sizes.Count == 2;
            string pattern = string.Concat(widths.Select(w => sizes.Count == 1 ? "=" : Math.Abs(w - sizes[0]) <= 1e-6 ? "s" : "L"));
            int smallCount = widths.Count(w => Math.Abs(w - sizes[0]) <= 1e-6);
            return new MosResult {
                IsMos = isMos, IsEqualStep = sizes.Count == 1,
                SmallCount = isMos ? smallCount : 0, LargeCount = isMos ? cardinality - smallCount : 0, Pattern = pattern,
                Scale = new ScaleDefinition {
                    // Single-period identifiers keep their earlier form.
                    Id = periods == 1 ? $"mos-{generatorDegree}-{cardinality}-{mode}" : $"mos-{generatorDegree}-{cardinality}-{mode}-p{periods}",
                    Name = isMos ? $"{cardinality - smallCount}L{smallCount}s (mode {mode + 1})" : sizes.Count == 1 ? "Equal-step subset" : "Generator subset",
                    Degrees = degrees, GeneratorDegree = generatorDegree, Mode = mode,
                },
            };
        }
        /// <summary>Create an explicit generator tuning without quantising any ratio to another tuning.</summary>
        public static TuningDefinition CreateTuning(PitchInterval period, PitchInterval generator, int cardinality) {
            double periodCents = period.ToCents();
            double generatorCents = generator.ToCents();
            if (periodCents <= 0 || cardinality <= 0 || cardinality > 65536) throw new ArgumentOutOfRangeException(nameof(cardinality));
            var cents = Enumerable.Range(0, cardinality)
                .Select(i => { double c = i * generatorCents; return c - Math.Floor(c / periodCents) * periodCents; })
                .OrderBy(c => c).ToList();
            // Preserve rational expressions exactly when both inputs are rational.
            var degrees = new List<PitchInterval>();
            if (period.Ratio != null && generator.Ratio != null) {
                var p = PitchInterval.NormaliseRatio(period.Ratio).Split('/').Select(System.Numerics.BigInteger.Parse).ToArray();
                var g = PitchInterval.NormaliseRatio(generator.Ratio).Split('/').Select(System.Numerics.BigInteger.Parse).ToArray();
                for (int i = 0; i < cardinality; i++) {
                    int repetitions = checked((int)Math.Floor(i * generatorCents / periodCents));
                    var numerator = System.Numerics.BigInteger.Pow(g[0], i);
                    var denominator = System.Numerics.BigInteger.Pow(g[1], i);
                    if (repetitions >= 0) { numerator *= System.Numerics.BigInteger.Pow(p[1], repetitions); denominator *= System.Numerics.BigInteger.Pow(p[0], repetitions); }
                    else { numerator *= System.Numerics.BigInteger.Pow(p[0], -repetitions); denominator *= System.Numerics.BigInteger.Pow(p[1], -repetitions); }
                    degrees.Add(PitchInterval.FromRatio($"{numerator}/{denominator}"));
                }
                degrees = degrees.OrderBy(d => d.ToCents()).ToList();
            } else degrees = cents.Select(PitchInterval.FromCents).ToList();
            var tuning = new TuningDefinition {
                Id = "generator-" + Guid.NewGuid().ToString("N"), Name = "Generator tuning", Divisions = 0,
                Period = period.Clone(), Degrees = degrees,
                Reference = new TuningReference { Degree = 0, Frequency = 440 },
                Scales = new List<ScaleDefinition> { new ScaleDefinition { Degrees = Enumerable.Range(0, cardinality).ToList() } },
            };
            tuning.InputMapping.FormalOctave = cardinality;
            tuning.Validate();
            return tuning;
        }
    }
}
