using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenUtau.Core.Microtonal {
    public static class PitchNotation {
        public static string Format(TuningDefinition tuning, int degree, string? notationId = null, bool includePeriod = true) {
            int count = tuning.DegreeCount;
            int period = (int)Math.Floor((double)degree / count);
            int index = degree - period * count;
            string id = notationId ?? tuning.Keyboard.PrimaryNotation;
            var profile = tuning.Notations.FirstOrDefault(p => p.Id == id);
            string label = profile != null && profile.Names.TryGetValue(index, out string? name)
                ? name : index.ToString(CultureInfo.InvariantCulture);
            if (profile != null && profile.AccidentalIds.TryGetValue(index, out var accidental)) label += AccidentalGlyph(accidental);
            if (id == "degree" || profile == null) return includePeriod ? $"{degree}/{count}" : label;
            int nominalPeriodOffset = profile.PeriodOffsets.TryGetValue(index, out int offset) ? offset : 0;
            return includePeriod ? label + (period + 4 + nominalPeriodOffset).ToString(CultureInfo.InvariantCulture) : label;
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
