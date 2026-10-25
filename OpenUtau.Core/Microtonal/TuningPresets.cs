using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenUtau.Core.Microtonal {
    /// <summary>Definitions are generated afresh so a project cannot mutate the shared library.</summary>
    public static class TuningPresets {
        public const string PelogId = "pelog-9edo-2l5s";
        public const string AeuId = "turkish-aeu-24";
        public static TuningDefinition Default => EqualDivision(12);
        public static IReadOnlyList<TuningDefinition> All {
            get {
                var all = new List<TuningDefinition>();
                foreach (int divisions in new[] { 5, 7, 10, 12, 15, 16, 17, 19, 22, 24, 26, 31, 34, 41, 46, 53, 72 }) all.Add(EqualDivision(divisions));
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
                // Rast is a scale of 24-EDO: the standard 24-EDO keyboard with the Rast scale selected.
                var rast = EqualDivision(24); rast.Id = "rast-24edo-approximation"; rast.Name = "Rast (24-EDO approximation)";
                rast.Description = "Modern quarter-tone approximation of Rast: C D E-half-flat F G A B-half-flat. Actual intonation varies by tradition and performance.";
                rast.Source = "Analytical 24-EDO approximation of the Rast interval pattern; not a measured historical tuning.";
                rast.Keyboard.ActiveScaleId = ScalePresets.Prefix + "maqam-rast";
                all.Add(rast);
                AddExtendedPresets(all);
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
            }
            tuning.Validate();
            return tuning;
        }

        public static TuningDefinition FromRatios(string id, string name, IEnumerable<string> ratios, string description, string period = "2/1") {
            var tuning = new TuningDefinition {
                Id = id, Name = name, Divisions = 0, Degrees = ratios.Select(PitchInterval.FromRatio).ToList(), Period = PitchInterval.FromRatio(period),
                Description = description, Source = "Exact rational intervals listed in this preset.",
            };
            tuning.Reference.Degree = tuning.Degrees.Count == 12 ? 9 : 0;
            tuning.InputMapping.FormalOctave = tuning.DegreeCount;
            // Spelling follows each ratio's 5-limit function (16/15 is D-flat, 45/32 is F-sharp); septimal
            // intervals take the nearest chromatic name without marking their comma.
            if (tuning.Degrees.Count == 12) AddChromaticNames(tuning, "Ratio-based chromatic names", JustNames);
            ManualKeys(tuning);
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
            ManualKeys(tuning);
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
            ManualKeys(tuning);
            tuning.Validate();
            return tuning;
        }
        /// <summary>Unequal presets use a key-by-key keyboard whose keys follow their names.</summary>
        static void ManualKeys(TuningDefinition tuning) {
            // Derive the keys from the current names, not from classes set before the names were added.
            tuning.Keyboard.Layout = KeyboardLayout.MosLayout;
            tuning.Keyboard.Classes.Clear();
            tuning.Keyboard.Classes = KeyboardRegionizer.ClassesFromRoles(tuning);
            tuning.Keyboard.Layout = KeyboardLayout.ManualLayout;
        }

        static void AddOwnNames(TuningDefinition tuning, string name, IReadOnlyList<string> names) {
            var profile = new NotationProfile { Id = "custom", Name = name };
            for (int i = 0; i < names.Count; i++) profile.Names[i] = names[i];
            tuning.Notations.Add(profile);
            tuning.Keyboard.PrimaryNotation = "custom";
            tuning.Keyboard.SecondaryNotation = "degree";
        }

        static TuningDefinition FromCents(string id, string name, IReadOnlyList<double> cents, double period, string description, string source) {
            var tuning = new TuningDefinition {
                Id = id, Name = name, Divisions = 0, Degrees = cents.Select(PitchInterval.FromCents).ToList(),
                Period = PitchInterval.FromCents(period), Description = description, Source = source,
            };
            tuning.InputMapping.FormalOctave = tuning.DegreeCount;
            tuning.Scales.Add(new ScaleDefinition { Degrees = Enumerable.Range(0, tuning.DegreeCount).ToList() });
            return tuning;
        }

        /// <summary>
        /// A non-octave equal step, grouped on the keyboard in cycles of <paramref name="steps"/> (about 3/2);
        /// the cycle is only a grouping, so the pitches are those of the step itself.
        /// </summary>
        static TuningDefinition EqualStep(string id, string name, double step, int steps, string source) {
            var tuning = EqualDivision(steps, PitchInterval.FromCents(step * steps));
            tuning.Id = id; tuning.Name = name;
            tuning.Description = $"Equal steps of {step.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} cents without an octave; the keyboard repeats every {steps} steps.";
            tuning.Source = source;
            tuning.Validate();
            return tuning;
        }

        static void AddExtendedPresets(List<TuningDefinition> all) {
            double syntonicComma = 1200 * Math.Log2(81.0 / 80);
            double pureFifth = 1200 * Math.Log2(1.5);
            all.Add(FifthChain("meantone-third-comma", "Third-comma meantone", pureFifth - syntonicComma / 3));
            all.Add(FifthChain("meantone-two-sevenths-comma", "Two-sevenths-comma meantone", pureFifth - syntonicComma * 2 / 7));

            var ptolemy = FromRatios("ptolemy-intense-diatonic", "Ptolemy's intense diatonic", new[] { "1/1", "9/8", "5/4", "4/3", "3/2", "5/3", "15/8" },
                "Ptolemy's intense (syntonic) diatonic: a seven-note 5-limit major scale.");
            ptolemy.Reference.Degree = 5;
            AddOwnNames(ptolemy, "Diatonic names", new[] { "C", "D", "E", "F", "G", "A", "B" });
            ManualKeys(ptolemy);
            all.Add(ptolemy);

            var harmonic = FromRatios("harmonic-series-8-16", "Harmonic series (8–16)",
                Enumerable.Range(8, 8).Select(h => $"{h}/8"), "Harmonics 8 to 15 of a fundamental, repeating at the octave (harmonic 16).");
            AddOwnNames(harmonic, "Harmonic numbers", Enumerable.Range(8, 8).Select(h => "H" + h).ToArray());
            ManualKeys(harmonic);
            all.Add(harmonic);

            var bpJust = FromRatios("bohlen-pierce-just", "Bohlen–Pierce just intonation",
                new[] { "1/1", "27/25", "25/21", "9/7", "7/5", "75/49", "5/3", "9/5", "49/25", "15/7", "7/3", "63/25", "25/9" },
                "Thirteen just intervals of the Bohlen–Pierce scale within the tritave 3/1.", "3/1");
            bpJust.Reference.Degree = 0;
            bpJust.Source = "Bohlen–Pierce rational scale; ratios as listed at https://en.xen.wiki/w/Bohlen%E2%80%93Pierce_scale";
            if (PitchNotation.BohlenPierce(13) is NotationProfile lambdaNames) {
                bpJust.Notations.Add(lambdaNames);
                bpJust.Keyboard.PrimaryNotation = "bp";
                bpJust.Keyboard.SecondaryNotation = "degree";
            }
            ManualKeys(bpJust);
            all.Add(bpJust);

            var shruti = FromRatios("shruti-22", "22 śruti (5-limit)",
                new[] { "1/1", "256/243", "16/15", "10/9", "9/8", "32/27", "6/5", "5/4", "81/64", "4/3", "27/20", "45/32",
                    "729/512", "3/2", "128/81", "8/5", "5/3", "27/16", "16/9", "9/5", "15/8", "243/128" },
                "Twenty-two śruti as 5-limit ratios; steps are 256/243, 81/80 and 25/24 in the classical pattern.");
            shruti.Source = "5-limit śruti ratios matching the poorna/pramana/nyuna step pattern in https://en.wikipedia.org/wiki/Shruti_(music); names group four śruti per swara (a common teaching convention).";
            AddOwnNames(shruti, "Swara and śruti", new[] { "Sa", "Re1", "Re2", "Re3", "Re4", "Ga1", "Ga2", "Ga3", "Ga4",
                "Ma1", "Ma2", "Ma3", "Ma4", "Pa", "Dha1", "Dha2", "Dha3", "Dha4", "Ni1", "Ni2", "Ni3", "Ni4" });
            shruti.Keyboard.Classes = Enumerable.Range(0, 22).ToDictionary(d => d, d => d is 0 or 4 or 7 or 9 or 13 or 16 or 20 ? "natural" : "accidental");
            shruti.Keyboard.Layout = KeyboardLayout.ManualLayout;
            all.Add(shruti);

            var partchRatios = new[] { "1/1", "81/80", "33/32", "21/20", "16/15", "12/11", "11/10", "10/9", "9/8", "8/7", "7/6",
                "32/27", "6/5", "11/9", "5/4", "14/11", "9/7", "21/16", "4/3", "27/20", "11/8", "7/5", "10/7", "16/11", "40/27",
                "3/2", "32/21", "14/9", "11/7", "8/5", "18/11", "5/3", "27/16", "12/7", "7/4", "16/9", "9/5", "20/11", "11/6",
                "15/8", "40/21", "64/33", "160/81" };
            var partch = FromRatios("partch-43", "Partch 43-tone scale", partchRatios,
                "Harry Partch's 43-tone just scale: the 11-limit tonality diamond filled in with further ratios, symmetric about the octave.");
            partch.Source = "Ratios as listed at https://en.wikipedia.org/wiki/Harry_Partch%27s_43-tone_scale";
            AddOwnNames(partch, "Ratios", partchRatios);
            ManualKeys(partch);
            all.Add(partch);

            // AEU: 24 of the 53 Holdrian commas, named from Rast; Dügâh (A) is the 440 Hz reference.
            int[] commas = { 0, 4, 5, 8, 9, 13, 14, 17, 18, 21, 22, 26, 27, 30, 31, 35, 36, 39, 40, 44, 45, 48, 49, 52 };
            string[] perde = { "Rast", "Nim Zirgüle", "Zirgüle", "Dik Zirgüle", "Dügâh", "Kürdî", "Dik Kürdî", "Segâh", "Bûselik",
                "Dik Bûselik", "Çârgâh", "Nim Hicâz", "Hicâz", "Dik Hicâz", "Nevâ", "Nim Hisâr", "Hisâr", "Dik Hisâr", "Hüseynî",
                "Acem", "Dik Acem", "Eviç", "Mâhûr", "Dik Mâhûr" };
            var aeu = FromCents(AeuId, "Turkish AEU (24 of 53 commas)", commas.Select(c => c * 1200.0 / 53).ToArray(), 1200,
                "Arel–Ezgi–Uzdilek pitches: 24 of the 53 Holdrian commas in the octave, from Rast. Dügâh (A) at 440 Hz is this preset's convention, not part of the system.",
                "Comma positions and perde names from https://en.wikipedia.org/wiki/Turkish_makam (53 equal commas).");
            aeu.Reference = new TuningReference { Degree = 4, Frequency = 440 };
            AddOwnNames(aeu, "Perde names", perde);
            // Western staff naturals G A B C D E F are white keys: Rast, Dügâh, Bûselik, Çârgâh, Nevâ, Hüseynî, Acem.
            aeu.Keyboard.Classes = Enumerable.Range(0, 24).ToDictionary(d => d, d => d is 0 or 4 or 8 or 10 or 14 or 18 or 19 ? "natural" : "accidental");
            aeu.Keyboard.Layout = KeyboardLayout.ManualLayout;
            aeu.Validate();
            all.Add(aeu);

            var slendro = EqualDivision(5);
            slendro.Id = "slendro-5edo"; slendro.Name = "Slendro (5-EDO approximation)";
            slendro.Description = "Javanese slendro approximated by five equal steps; real gamelan tunings vary from set to set.";
            slendro.Source = "Five roughly equal pitches per https://en.wikipedia.org/wiki/Slendro; tone names ji ro lu ma nem (1 2 3 5 6).";
            slendro.Notations.Clear();
            AddOwnNames(slendro, "Javanese tone names", new[] { "ji", "ro", "lu", "ma", "nem" });
            ManualKeys(slendro);
            all.Add(slendro);

            int[] pelogSteps = { 0, 1, 2, 4, 5, 6, 7 };
            var pelog = FromCents(PelogId, "Pelog (9-EDO approximation)", pelogSteps.Select(k => k * 1200.0 / 9).ToArray(), 1200,
                "Javanese pelog approximated by seven notes of 9-EDO (its 2L5s scale); real gamelan tunings vary from set to set.",
                "9-EDO 2L5s per https://en.xen.wiki/w/Pelog; the mode (large steps after lu and pi) is inferred from the pelog interval pattern, not quoted. Tone names per https://en.wikipedia.org/wiki/Pelog.");
            pelog.Reference.Degree = 0;
            AddOwnNames(pelog, "Javanese tone names", new[] { "ji", "ro", "lu", "pat", "ma", "nem", "pi" });
            ManualKeys(pelog);
            all.Add(pelog);

            const string carlos = "Wendy Carlos's non-octave scale. https://en.wikipedia.org/wiki/Alpha_scale rounds the steps to 78, 63.8 and 35.1 cents; the preset uses the commonly derived 77.965, 63.833 and 35.099 cents.";
            all.Add(EqualStep("carlos-alpha", "Carlos Alpha", 77.965, 9, carlos));
            all.Add(EqualStep("carlos-beta", "Carlos Beta", 63.833, 11, carlos));
            all.Add(EqualStep("carlos-gamma", "Carlos Gamma", 35.099, 20, carlos));
            all.Add(EqualStep("equal-88-cent", "88-cent equal steps", 88, 8, "Equal steps of 88 cents without an octave (Gary Morrison)."));
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
