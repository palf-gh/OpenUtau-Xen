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
    public static class MosGenerator {
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
