using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenUtau.Core.Microtonal {
    public static class PitchNotation {
        public const string Auto = "auto";
        public const string None = "none";
        /// <summary>Generated letter names for the nominals of the active generator scale.</summary>
        public const string Mos = "mos";
        // Automatic labels prefer user-authored names, then verified accidentals, then Ups and Downs.
        static readonly string[] AutoOrder = { "custom", "szg", "bp", "ups-downs" };
        static readonly ConcurrentDictionary<int, IReadOnlyList<NotationProfile>> builtIn = new ConcurrentDictionary<int, IReadOnlyList<NotationProfile>>();
        static readonly ConcurrentDictionary<string, NotationProfile> mosLetters = new ConcurrentDictionary<string, NotationProfile>();

        public static string Format(TuningDefinition tuning, int degree, string? notationId = null, bool includePeriod = true) {
            int count = tuning.DegreeCount;
            int period = (int)Math.Floor((double)degree / count);
            int index = degree - period * count;
            string id = notationId ?? tuning.Keyboard.PrimaryNotation;
            var profile = id == "degree" ? null : FindProfile(tuning, id);
            if (profile == null || !profile.Names.TryGetValue(index, out string? label)) {
                string number = index.ToString(CultureInfo.InvariantCulture);
                return includePeriod ? number + "·" + (period + 4).ToString(CultureInfo.InvariantCulture) : number;
            }
            if (profile.AccidentalIds.TryGetValue(index, out var accidental)) label += AccidentalGlyph(accidental);
            int nominalPeriodOffset = profile.PeriodOffsets.TryGetValue(index, out int offset) ? offset : 0;
            return includePeriod ? label + (period + 4 + nominalPeriodOffset).ToString(CultureInfo.InvariantCulture) : label;
        }

        /// <summary>
        /// The tuning's own profile, or for an octave equal division the built-in profile of the same name,
        /// so projects saved before a notation table existed still show names. Nothing is written back.
        /// </summary>
        public static NotationProfile? FindProfile(TuningDefinition tuning, string id) {
            var own = tuning.Notations.FirstOrDefault(p => p.Id == id && p.Names.Count > 0);
            if (own != null) return own;
            if (id == Mos) return MosLetters(tuning);
            if (tuning.Divisions <= 0 || tuning.Divisions > 256) return null;
            var theory = KeyboardTheory.For(tuning);
            if (theory == null) return null;
            bool octave = theory.ReferenceDivision == 31;
            var profiles = builtIn.GetOrAdd(octave ? tuning.Divisions : -tuning.Divisions, _ => octave
                ? TuningPresets.EqualDivision(tuning.Divisions).Notations
                : TuningPresets.EqualDivision(tuning.Divisions, PitchInterval.FromRatio("3/1")).Notations);
            return profiles.FirstOrDefault(p => p.Id == id && p.Names.Count > 0);
        }
        public static bool IsAvailable(TuningDefinition tuning, string id) => id == "degree" || FindProfile(tuning, id) != null;

        public static string ResolvePrimary(TuningDefinition tuning, string? preference) {
            if (!string.IsNullOrEmpty(preference) && preference != Auto && IsAvailable(tuning, preference)) return preference;
            string stored = tuning.Keyboard.PrimaryNotation;
            // A letter-named MOS keyboard replaces family names unless the user authored their own.
            if (stored != "custom" && IsAvailable(tuning, Mos)) return Mos;
            if (stored != "degree" && IsAvailable(tuning, stored)) return stored;
            return AutoOrder.FirstOrDefault(id => IsAvailable(tuning, id)) ?? "degree";
        }
        public static string? ResolveSecondary(TuningDefinition tuning, string primary, string? preference) {
            if (preference == None) return null;
            if (!string.IsNullOrEmpty(preference) && preference != Auto && IsAvailable(tuning, preference)) {
                return preference == primary ? null : preference;
            }
            return primary == "degree" ? null : "degree";
        }

        /// <summary>
        /// Key role by degree index, never by physical pitch, so it survives any reference frequency.
        /// Explicit key classes win; equal divisions of the octave or tritave use the keyboard regioniser;
        /// other tunings fall back to their note names.
        /// </summary>
        public static KeyRole Role(TuningDefinition tuning, int degree) {
            int count = tuning.DegreeCount;
            int index = (degree % count + count) % count;
            // A letter-named MOS owns the whole keyboard; otherwise explicit classes override the family baseline.
            if (KeyboardRegionizer.LetterScale(tuning) == null && tuning.Keyboard.Classes.TryGetValue(index, out string? keyClass)) {
                return keyClass switch {
                    "accidental" => KeyRole.Black,
                    "secondary" or "between" => KeyRole.Between,
                    _ => KeyRole.White,
                };
            }
            var roles = KeyboardRegionizer.Roles(tuning);
            if (roles != null) return roles[index];
            foreach (string id in new[] { "szg", "bp", "ups-downs", "custom" }) {
                var profile = FindProfile(tuning, id);
                if (profile != null && profile.Names.TryGetValue(index, out string? name)) {
                    return profile.AccidentalIds.ContainsKey(index) || AccidentalMarks.Any(name.Contains) ? KeyRole.Black : KeyRole.White;
                }
            }
            return count == 12 && Math.Abs(tuning.PeriodCents - 1200) < 1e-8 && index is 1 or 3 or 6 or 8 or 10
                ? KeyRole.Black : KeyRole.White;
        }
        static readonly string[] AccidentalMarks = { "♯", "♭", "↑", "↓", "#", "𝄪", "𝄫" };

        /// <summary>
        /// Bohlen–Pierce Lambda names for equal divisions of the tritave: nominals C D E F G H J A B,
        /// sharps and flats of the Lambda chromatic step, and ups and downs for any remainder.
        /// 13-EDT gives the standard nine naturals and four sharp/flat pairs.
        /// </summary>
        public static NotationProfile? BohlenPierce(int divisions) {
            var theory = KeyboardTheory.Tritave(divisions);
            if (!theory.IsValid) return null;
            return Nominal("bp", "Bohlen–Pierce (Lambda)", divisions, theory.Naturals, theory.NaturalNames, theory.Chromatic);
        }

        static NotationProfile Nominal(string id, string name, int divisions, IReadOnlyList<int> nominalDegrees, IReadOnlyList<string> nominals, int sharp) {
            int Mod(int value) => (value % divisions + divisions) % divisions;
            var profile = new NotationProfile { Id = id, Name = name };
            for (int d = 0; d < divisions; d++) {
                var candidates = new List<(string Name, int Cost, int Nominal, int PeriodOffset)>();
                for (int n = 0; n < nominals.Count; n++) for (int accidental = -1; accidental <= 1; accidental++) {
                    int baseDegree = Mod(nominalDegrees[n] + accidental * sharp);
                    int arrows = Mod(d - baseDegree);
                    if (arrows > divisions / 2) arrows -= divisions;
                    string suffix = accidental == 1 ? "♯" : accidental == -1 ? "♭" : string.Empty;
                    string prefix = arrows > 0 ? new string('↑', arrows) : new string('↓', -arrows);
                    int cost = Math.Abs(arrows) * 10 + Math.Abs(accidental) * 4;
                    if (accidental == 0 && arrows == 0) cost = -100;
                    int nominalOffset = (int)Math.Floor((double)(d - nominalDegrees[n] - accidental * sharp - arrows) / divisions);
                    candidates.Add((prefix + nominals[n] + suffix, cost, n, nominalOffset));
                }
                var ordered = candidates.OrderBy(c => c.Cost).ThenBy(c => c.Nominal).ThenBy(c => c.Name, StringComparer.Ordinal).ToList();
                profile.Names[d] = ordered[0].Name;
                if (ordered[0].PeriodOffset != 0) profile.PeriodOffsets[d] = ordered[0].PeriodOffset;
                var aliases = ordered.Skip(1).Where(c => c.Cost <= ordered[0].Cost + 10).Select(c => c.Name).Distinct().ToList();
                if (aliases.Count > 0) profile.Aliases[d] = aliases;
            }
            return profile;
        }

        /// <summary>
        /// Letters A, B, C … (skipping I) on the active generator scale's nominals, sharps and flats of its
        /// chroma (large minus small step), and ups and downs for any remainder.
        /// </summary>
        static NotationProfile? MosLetters(TuningDefinition tuning) {
            var scale = KeyboardRegionizer.LetterScale(tuning);
            var model = scale == null ? null : KeyboardRegionizer.Model(tuning);
            if (model == null) return null;
            string key = model.Divisions + ":" + string.Join(",", model.Nominals);
            if (mosLetters.Count > 64) mosLetters.Clear();
            return mosLetters.GetOrAdd(key, _ => Nominal(Mos, "MOS letters", model.Divisions, model.Nominals,
                model.Nominals.Select((_, i) => KeyboardRegionizer.LetterName(i)).ToArray(), model.Large - model.Small));
        }

        /// <summary>Twelve named keys for C-centred twelve-note presets, in degree order.</summary>
        public static NotationProfile Chromatic(string name, IReadOnlyList<string> names) {
            if (names.Count != 12) throw new ArgumentException("Twelve names are required.", nameof(names));
            var profile = new NotationProfile { Id = "szg", Name = name };
            for (int i = 0; i < 12; i++) profile.Names[i] = names[i];
            return profile;
        }

        /// <summary>
        /// 31-EDO with half-sharps and half-flats (Fokker's 31-tone spelling): sharp = 2 steps, half-sharp = 1.
        /// Stein quarter-tone glyphs denote the half-sharp/half-flat here. Each enharmonic is kept as an alias.
        /// </summary>
        public static NotationProfile ThirtyOneHalfAccidentals() {
            var profile = new NotationProfile { Id = "szg", Name = "Stein–Zimmermann half accidentals (31-EDO)" };
            const string halfSharp = "accidentalQuarterToneSharpStein", halfFlat = "accidentalQuarterToneFlatStein";
            var table = new (string Name, string? Accidental)[] {
                ("C", null), ("C", halfSharp), ("C♯", null), ("D♭", null), ("D", halfFlat),
                ("D", null), ("D", halfSharp), ("D♯", null), ("E♭", null), ("E", halfFlat),
                ("E", null), ("E", halfSharp), ("F", halfFlat), ("F", null), ("F", halfSharp),
                ("F♯", null), ("G♭", null), ("G", halfFlat), ("G", null), ("G", halfSharp),
                ("G♯", null), ("A♭", null), ("A", halfFlat), ("A", null), ("A", halfSharp),
                ("A♯", null), ("B♭", null), ("B", halfFlat), ("B", null), ("B", halfSharp), ("C", halfFlat),
            };
            for (int degree = 0; degree < table.Length; degree++) {
                profile.Names[degree] = table[degree].Name;
                if (table[degree].Accidental != null) profile.AccidentalIds[degree] = table[degree].Accidental!;
            }
            profile.PeriodOffsets[30] = 1;
            profile.Aliases[1] = new List<string> { "D𝄫" };
            profile.Aliases[11] = new List<string> { "F♭" };
            profile.Aliases[12] = new List<string> { "E♯" };
            profile.Aliases[29] = new List<string> { "C♭" };
            profile.Aliases[30] = new List<string> { "B♯" };
            return profile;
        }
        public static string AccidentalGlyph(string semanticId) => string.Concat(semanticId.Split('|').Select(SingleAccidentalGlyph));
        static string SingleAccidentalGlyph(string semanticId) => semanticId switch {
            "accidentalNatural" => "\uE261",
            "accidentalSharp" => "\uE262",
            "accidentalFlat" => "\uE260",
            "accidentalArrowUp" => "\uE27A",
            "accidentalArrowDown" => "\uE27B",
            "accidentalQuarterToneSharpArrowDown" => "\uE275",
            "accidentalThreeQuarterTonesSharpArrowUp" => "\uE274",
            "accidentalThreeQuarterTonesFlatArrowDown" => "\uE271",
            "accidentalQuarterToneSharpStein" => "\uE282",
            "accidentalThreeQuarterTonesSharpStein" => "\uE283",
            "accidentalQuarterToneFlatStein" => "\uE280",
            "accidentalThreeQuarterTonesFlatZimmermann" => "\uE281",
            _ => string.Empty,
        };
        /// <summary>
        /// Offset semantics follow the 53edo SZG table, not the quarter-tone meanings of the glyph names.
        /// Sources: https://en.xen.wiki/w/53edo#Stein–Zimmermann–Gould_notation and W3C SMuFL glyphnames.
        /// </summary>
        public static NotationProfile FiftyThreeSzg() {
            var profile = new NotationProfile { Id = "szg", Name = "Stein–Zimmermann–Gould (53-EDO)" };
            string[] nominalNames = { "C", "D", "E", "F", "G", "A", "B" };
            int[] nominalDegrees = { 0, 9, 18, 22, 31, 40, 49 };
            var alterations = new Dictionary<int, (string Id, int Cost)> {
                [0] = (string.Empty, 0),
                [1] = ("accidentalArrowUp|accidentalNatural", 8),
                [2] = ("accidentalArrowUp|accidentalArrowUp|accidentalNatural", 16),
                [3] = ("accidentalArrowDown|accidentalQuarterToneSharpArrowDown", 20),
                [4] = ("accidentalQuarterToneSharpArrowDown", 6),
                [5] = ("accidentalSharp", 4),
                [6] = ("accidentalThreeQuarterTonesSharpArrowUp", 7),
                [7] = ("accidentalThreeQuarterTonesSharpArrowUp|accidentalArrowUp", 22),
                [-1] = ("accidentalNatural|accidentalArrowDown", 8),
                [-2] = ("accidentalNatural|accidentalArrowDown|accidentalArrowDown", 16),
                [-3] = ("accidentalArrowUp|accidentalArrowUp|accidentalFlat", 20),
                [-4] = ("accidentalArrowUp|accidentalFlat", 7),
                [-5] = ("accidentalFlat", 4),
                [-6] = ("accidentalThreeQuarterTonesFlatArrowDown", 7),
                [-7] = ("accidentalArrowDown|accidentalThreeQuarterTonesFlatArrowDown", 22),
            };
            for (int degree = 0; degree < 53; degree++) {
                var candidates = new List<(int Nominal, int Offset, int Cost)>();
                for (int n = 0; n < nominalDegrees.Length; n++) {
                    int offset = degree - nominalDegrees[n];
                    if (offset > 26) offset -= 53;
                    if (offset < -26) offset += 53;
                    if (alterations.TryGetValue(offset, out var alteration)) candidates.Add((n, offset, alteration.Cost));
                }
                var chosen = candidates.OrderBy(c => c.Cost).ThenBy(c => Math.Abs(c.Offset)).ThenBy(c => c.Nominal).First();
                profile.Names[degree] = nominalNames[chosen.Nominal];
                if (chosen.Offset != 0) profile.AccidentalIds[degree] = alterations[chosen.Offset].Id;
                int nominalOffset = (degree - nominalDegrees[chosen.Nominal] - chosen.Offset) / 53;
                if (nominalOffset != 0) profile.PeriodOffsets[degree] = nominalOffset;
            }
            return profile;
        }
        public static NotationProfile UpsAndDowns(int divisions) {
            if (divisions <= 0) throw new ArgumentOutOfRangeException(nameof(divisions));
            int fifth = (int)Math.Round(divisions * Math.Log2(1.5), MidpointRounding.AwayFromZero);
            int Mod(int value) => (value % divisions + divisions) % divisions;
            var nominalDegrees = new[] { 0, Mod(2 * fifth), Mod(4 * fifth), Mod(-fifth), Mod(fifth), Mod(3 * fifth), Mod(5 * fifth) };
            string[] nominals = { "C", "D", "E", "F", "G", "A", "B" };
            int sharp = 7 * fifth - 4 * divisions;
            var profile = new NotationProfile { Id = "ups-downs", Name = "Ups and Downs" };
            for (int d = 0; d < divisions; d++) {
                var candidates = new List<(string Name, int Cost, int Nominal, int PeriodOffset)>();
                for (int n = 0; n < 7; n++) for (int accidental = -1; accidental <= 1; accidental++) {
                    int baseDegree = Mod(nominalDegrees[n] + accidental * sharp);
                    int arrows = Mod(d - baseDegree);
                    if (arrows > divisions / 2) arrows -= divisions;
                    string suffix = accidental == 1 ? "♯" : accidental == -1 ? "♭" : string.Empty;
                    string prefix = arrows > 0 ? new string('↑', arrows) : new string('↓', -arrows);
                    int cost = Math.Abs(arrows) * 10 + Math.Abs(accidental) * 4;
                    if (accidental == 0 && arrows == 0) cost = -100;
                    int nominalOffset = (d - nominalDegrees[n] - accidental * sharp - arrows) / divisions;
                    candidates.Add((prefix + nominals[n] + suffix, cost, n, nominalOffset));
                }
                var ordered = candidates.OrderBy(c => c.Cost).ThenBy(c => c.Nominal).ThenBy(c => c.Name, StringComparer.Ordinal).ToList();
                profile.Names[d] = ordered[0].Name;
                if (ordered[0].PeriodOffset != 0) profile.PeriodOffsets[d] = ordered[0].PeriodOffset;
                profile.Aliases[d] = ordered.Where(c => c.Cost <= ordered[0].Cost + 10).Select(c => c.Name).Distinct().ToList();
            }
            return profile;
        }
        public static NotationProfile QuarterTone() {
            var profile = new NotationProfile { Id = "szg", Name = "Stein–Zimmermann (24-EDO)" };
            var semitones = new[] { "C", "C♯", "D", "E♭", "E", "F", "F♯", "G", "A♭", "A", "B♭", "B" };
            for (int i = 0; i < 24; i++) {
                if (i % 2 == 0) profile.Names[i] = semitones[i / 2];
                else {
                    bool afterSharp = semitones[i / 2].EndsWith("♯", StringComparison.Ordinal);
                    string nominal = semitones[i / 2].Substring(0, 1);
                    profile.Names[i] = nominal;
                    profile.AccidentalIds[i] = afterSharp ? "accidentalThreeQuarterTonesSharpStein" : "accidentalQuarterToneSharpStein";
                }
            }
            // Flat-based half steps need the following natural's quarter-flat rather than losing the flat.
            foreach (var item in new[] { (7, "E"), (17, "A"), (21, "B") }) {
                profile.Names[item.Item1] = item.Item2;
                profile.AccidentalIds[item.Item1] = "accidentalQuarterToneFlatStein";
            }
            return profile;
        }
    }
}
