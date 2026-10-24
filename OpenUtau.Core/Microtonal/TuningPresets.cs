using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenUtau.Core.Microtonal {
    /// <summary>Definitions are generated afresh so a project cannot mutate the shared library.</summary>
    public static class TuningPresets {
        public static TuningDefinition Default => EqualDivision(12);
        public static IReadOnlyList<TuningDefinition> All {
            get {
                var all = new List<TuningDefinition>();
                foreach (int divisions in new[] { 12, 17, 19, 22, 24, 31, 41, 53, 72 }) all.Add(EqualDivision(divisions));
                all.Add(FromRatios("ji-5-limit", "5-limit just intonation", new[] { "1/1", "16/15", "9/8", "6/5", "5/4", "4/3", "45/32", "3/2", "8/5", "5/3", "9/5", "15/8" },
                    "A C-centred chromatic 5-limit example; ratios are one selected spelling, not a unique JI chromatic scale."));
                all.Add(FromRatios("ji-7-limit", "7-limit just intonation", new[] { "1/1", "16/15", "9/8", "7/6", "5/4", "4/3", "7/5", "3/2", "8/5", "5/3", "7/4", "15/8" },
                    "A C-centred 7-limit example with harmonic seventh and septimal minor third."));
                all.Add(FifthChain("pythagorean", "Pythagorean (E-flat to G-sharp)", 1200 * Math.Log2(1.5)));
                double syntonicComma = 1200 * Math.Log2(81.0 / 80);
                all.Add(FifthChain("meantone-quarter-comma", "Quarter-comma meantone", 1200 * Math.Log2(1.5) - syntonicComma / 4));
                all.Add(FifthChain("meantone-sixth-comma", "Sixth-comma meantone", 1200 * Math.Log2(1.5) - syntonicComma / 6));
                // Exact analytical temperament definitions; cents are derived from the specified fifth sizes.
                double pythagoreanComma = 12 * 1200 * Math.Log2(1.5) - 7 * 1200;
                double pureFifth = 1200 * Math.Log2(1.5);
                all.Add(Circulating("werckmeister-iii", "Werckmeister III", -1,
                    new[] { 0d, 1d / 4, 1d / 4, 1d / 4, 0d, 0d, 1d / 4, 0d, 0d, 0d, 0d, 0d }.Select(f => pureFifth - f * pythagoreanComma).ToArray(),
                    "C–G, G–D, D–A and B–F-sharp fifths each tempered by one quarter of the Pythagorean comma."));
                // Kirnberger III: four C–E fifths share the syntonic comma; F-sharp–C-sharp carries the schisma.
                double schisma = pythagoreanComma - syntonicComma;
                var kirnberger = Enumerable.Repeat(pureFifth, 12).ToArray();
                for (int i = 1; i <= 4; i++) kirnberger[i] -= syntonicComma / 4;
                kirnberger[7] -= schisma;
                all.Add(Circulating("kirnberger-iii", "Kirnberger III", -1, kirnberger,
                    "Four C–E fifths each narrow by one quarter syntonic comma; F-sharp–C-sharp is narrow by a schisma."));
                all.Add(Circulating("vallotti", "Vallotti", -1,
                    Enumerable.Range(0, 12).Select(i => pureFifth - (i < 6 ? pythagoreanComma / 6 : 0)).ToArray(),
                    "Six fifths F–C–G–D–A–E–B share the Pythagorean comma; the other six are pure."));
                all.Add(Circulating("young-ii", "Young II", 0,
                    Enumerable.Range(0, 12).Select(i => pureFifth - (i < 6 ? pythagoreanComma / 6 : 0)).ToArray(),
                    "Six fifths C–G–D–A–E–B–F-sharp share the Pythagorean comma; the other six are pure."));
                all.Single(t => t.Id == "werckmeister-iii").Source = "Andreas Werckmeister, Musicalische Temperatur (1691), temperament III; analytical Pythagorean-comma construction.";
                all.Single(t => t.Id == "kirnberger-iii").Source = "Johann Philipp Kirnberger, Die Kunst des reinen Satzes in der Musik, temperament III; four quarter-syntonic-comma fifths plus one schisma. Modern formula cross-check: Native Instruments Kontakt Factory Library Scripts Manual, tuning presets.";
                all.Single(t => t.Id == "vallotti").Source = "Analytical construction; verified against Ross W. Duffin, 'Why I hate Vallotti (or is it Young?) 2.1': https://casfaculty.case.edu/ross-duffin/why-i-hate-vallotti-or-is-it-young-2-1/";
                all.Single(t => t.Id == "young-ii").Source = "Analytical construction; verified against Carey Beebe Harpsichords, 'Temperaments XIX — How to tune Young II': https://www.hpschd.nu/tech/tmp/young.html";
                var bp = EqualDivision(13, PitchInterval.FromRatio("3/1"));
                bp.Id = "bohlen-pierce-13edt"; bp.Name = "Bohlen–Pierce (13 equal divisions of 3/1)";
                bp.Reference = new TuningReference { Degree = 0, Frequency = 440 };
                bp.Description = "Equal-tempered Bohlen–Pierce: thirteen divisions of a tritave; degree zero is the 440 Hz reference.";

                all.Add(bp);
                // A named modern approximation, not a claim that regional performance practice is fixed or equal-tempered.
                var rast = EqualDivision(24); rast.Id = "rast-24edo-approximation"; rast.Name = "Rast (24-EDO approximation)";
                rast.Description = "Modern quarter-tone approximation of Rast: C D E-half-flat F G A B-half-flat. Actual intonation varies by tradition and performance.";
                rast.Source = "Analytical 24-EDO approximation of the Rast interval pattern; not a measured historical tuning.";
                rast.Scales = new List<ScaleDefinition> { new ScaleDefinition { Id = "rast", Name = "Rast approximation", Degrees = new List<int> { 0, 4, 7, 10, 14, 18, 21 } } };
                rast.Keyboard.ActiveScaleId = "rast";
                all.Add(rast);
                return all;
            }
        }
        public static TuningDefinition EqualDivision(int divisions, PitchInterval? period = null) {
            var tuning = new TuningDefinition {
                Id = $"{divisions}edo", Name = $"{divisions}-EDO", Divisions = divisions,
                Period = period?.Clone() ?? PitchInterval.FromRatio("2/1"),
                Description = $"{divisions} equal divisions of the period.", Source = "Analytical equal division: degree × period / divisions.",
                Reference = new TuningReference { Degree = (int)Math.Round(divisions * 0.75, MidpointRounding.AwayFromZero), Frequency = 440 },
            };
            tuning.InputMapping.FormalOctave = divisions;
            tuning.Scales.Add(new ScaleDefinition { Degrees = Enumerable.Range(0, divisions).ToList() });
            bool octave = Math.Abs(tuning.PeriodCents - 1200) < 1e-8;
            if (!octave) {
                string periodText = tuning.Period.Ratio ?? tuning.PeriodCents.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                tuning.Id = $"{divisions}ed-{tuning.PeriodCents.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}";
                tuning.Name = $"{divisions}-ED ({periodText})";
                tuning.Reference.Degree = 0;
            }
            if (divisions <= 256 && !octave && Math.Abs(tuning.PeriodCents - 1200 * Math.Log2(3)) < 1e-6
                && PitchNotation.BohlenPierce(divisions) is NotationProfile lambda) {
                tuning.Notations.Add(lambda);
                tuning.Keyboard.PrimaryNotation = "bp";
                tuning.Keyboard.SecondaryNotation = "degree";
            }
            if (divisions <= 256 && octave) {
                tuning.Notations.Add(PitchNotation.UpsAndDowns(divisions));
                tuning.Keyboard.PrimaryNotation = "ups-downs";
                tuning.Keyboard.SecondaryNotation = "degree";
            }
            if (divisions == 31 && octave) {
                tuning.Notations.Add(PitchNotation.ThirtyOneHalfAccidentals()); tuning.Keyboard.PrimaryNotation = "szg";
            }
            if (divisions == 12 && octave) {
                var standard = PitchNotation.UpsAndDowns(12); standard.Id = "szg"; standard.Name = "Standard accidentals (12-EDO)";
                tuning.Notations.Add(standard); tuning.Keyboard.PrimaryNotation = "szg"; tuning.Keyboard.SecondaryNotation = "degree";
            } else if (divisions == 24 && octave) {
                tuning.Notations.Add(PitchNotation.QuarterTone()); tuning.Keyboard.PrimaryNotation = "szg"; tuning.Keyboard.SecondaryNotation = "degree";
            }
            if (divisions == 53 && octave) {
                tuning.Reference.Degree = 40;
                tuning.Notations.Add(PitchNotation.FiftyThreeSzg());
                tuning.Keyboard.PrimaryNotation = "szg";
                tuning.Keyboard.SecondaryNotation = "degree";
                var mos = MosGenerator.Generate(tuning, 31, 7).Scale;
                tuning.Scales.Add(mos);
                foreach (int degree in mos.Degrees) tuning.Keyboard.Classes[degree] = "natural";
            } else if (octave && (divisions == 12 || divisions == 24)) {
                int multiplier = divisions / 12;
                foreach (int degree in new[] { 0, 2, 4, 5, 7, 9, 11 }) tuning.Keyboard.Classes[degree * multiplier] = "natural";
            }
            tuning.Validate();
            return tuning;
        }
        public static TuningDefinition FromRatios(string id, string name, IEnumerable<string> ratios, string description) {
            var tuning = new TuningDefinition {
                Id = id, Name = name, Divisions = 0, Degrees = ratios.Select(PitchInterval.FromRatio).ToList(),
                Description = description, Source = "Exact rational intervals listed in this preset.",
            };
            tuning.Reference.Degree = tuning.Degrees.Count == 12 ? 9 : 0;
            tuning.InputMapping.FormalOctave = tuning.DegreeCount;
            // Spelling follows each ratio's 5-limit function (16/15 is D-flat, 45/32 is F-sharp); septimal
            // intervals take the nearest chromatic name without marking their comma.
            if (tuning.Degrees.Count == 12) AddChromaticNames(tuning, "Ratio-based chromatic names", JustNames);
            tuning.Scales.Add(new ScaleDefinition { Degrees = Enumerable.Range(0, tuning.DegreeCount).ToList() });
            tuning.Validate();
            return tuning;
        }
        static TuningDefinition FifthChain(string id, string name, double fifth) {
            // Twelve keys, fifth indices -3 to +8: E-flat through G-sharp, with C as index zero.
            var pitches = Enumerable.Range(-3, 12).Select(k => {
                double cents = k * fifth; return cents - Math.Floor(cents / 1200) * 1200;
            }).OrderBy(c => c).Select(PitchInterval.FromCents).ToList();
            var tuning = new TuningDefinition {
                Id = id, Name = name, Divisions = 0, Degrees = pitches,
                Description = "C-centred twelve-key fifth chain, indices −3 to +8 (E-flat through G-sharp); the closing wolf is retained.",
                Source = "Fifth-chain construction; fifth cents = " + fifth.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            };
            tuning.Scales.Add(new ScaleDefinition { Degrees = Enumerable.Range(0, 12).ToList() });
            AddChromaticNames(tuning, "Fifth-chain names (E-flat to G-sharp)", FifthChainNames);
            tuning.Validate();
            return tuning;
        }
        static TuningDefinition Circulating(string id, string name, int startFifth, double[] fifths, string description) {
            // Walk twelve fifths; the final point closes the circle and is excluded.
            var raw = new List<(int PitchClass, double Cents)>();
            double position = startFifth * 700;
            for (int i = 0; i < 12; i++) {
                raw.Add((((startFifth + i) * 7 % 12 + 12) % 12, position));
                position += fifths[i];
            }
            double c = raw.Single(p => p.PitchClass == 0).Cents;
            var degrees = raw.OrderBy(p => p.PitchClass).Select(p => {
                double cents = p.Cents - c; return cents - Math.Floor(cents / 1200) * 1200;
            }).ToArray();
            var tuning = new TuningDefinition {
                Id = id, Name = name, Divisions = 0, Degrees = degrees.Select(PitchInterval.FromCents).ToList(),
                Description = description, Source = "Analytical construction from the stated tempered fifths; no external scale file copied.",
            };
            tuning.Scales.Add(new ScaleDefinition { Degrees = Enumerable.Range(0, 12).ToList() });
            AddChromaticNames(tuning, "Conventional chromatic names", FifthChainNames);
            tuning.Validate();
            return tuning;
        }
        static readonly string[] FifthChainNames = { "C", "C♯", "D", "E♭", "E", "F", "F♯", "G", "G♯", "A", "B♭", "B" };
        static readonly string[] JustNames = { "C", "D♭", "D", "E♭", "E", "F", "F♯", "G", "A♭", "A", "B♭", "B" };
        static void AddChromaticNames(TuningDefinition tuning, string name, IReadOnlyList<string> names) {
            tuning.Notations.Add(PitchNotation.Chromatic(name, names));
            tuning.Keyboard.PrimaryNotation = "szg";
            tuning.Keyboard.SecondaryNotation = "degree";
        }
    }
}
