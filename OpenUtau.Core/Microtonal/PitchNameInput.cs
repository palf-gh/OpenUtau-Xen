using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpenUtau.Core.Microtonal {
    /// <summary>
    /// Plain-text pitch names for the note property field: a natural nominal and octave plus or minus
    /// tuning steps (53-EDO: C4, C4+1 … C4+4, D4-4 … D4-1, D4). Spelled names from the tuning's
    /// notation profiles (C♯4, C#4, D♭4) and bare degree numbers are also accepted.
    /// </summary>
    public static class PitchNameInput {
        static readonly Regex Named = new Regex(@"^([A-HJ-Za-hj-z])([♯#♭b↑↓^v]*)(\d+)?(?:\s*([+-]\s*\d+))?$", RegexOptions.CultureInvariant);
        static readonly Regex Numbered = new Regex(@"^(\d+)\s*[·.]\s*(\d+)$", RegexOptions.CultureInvariant);

        /// <summary>The nearest natural with a signed step offset; ties prefer the lower natural.</summary>
        public static string Format(TuningDefinition tuning, int degree) {
            var naturals = Naturals(tuning);
            int count = tuning.DegreeCount;
            if (naturals.Count == 0) return PitchNotation.Format(tuning, degree, "degree");
            if (count == 12) {
                // Twelve-note tunings keep conventional spelled names (C♯4) instead of step offsets.
                int index = degree - FloorDiv(degree, count) * count;
                var spelled = Spellings(tuning).FirstOrDefault(sp => sp.Degree == index);
                if (spelled.Name != null) return spelled.Name + Octave(tuning, degree).ToString(CultureInfo.InvariantCulture);
            }
            var best = naturals
                .SelectMany(n => new[] { -1, 0, 1 }.Select(cycle => (n.Name, Degree: n.Degree + cycle * count)))
                .Select(n => (n.Name, n.Degree, Offset: degree - n.Degree))
                .OrderBy(n => Math.Abs(n.Offset)).ThenBy(n => n.Offset < 0).ThenBy(n => n.Degree)
                .First();
            string text = best.Name + Octave(tuning, best.Degree).ToString(CultureInfo.InvariantCulture);
            return best.Offset == 0 ? text : text + (best.Offset > 0 ? "+" : "") + best.Offset.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Parses an absolute name or a relative "+n"/"-n" step count from the current degree.</summary>
        public static bool TryParse(TuningDefinition tuning, string? text, int currentDegree, out int degree) {
            degree = currentDegree;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string input = text.Trim();
            int count = tuning.DegreeCount;
            if ((input[0] == '+' || input[0] == '-') && int.TryParse(input, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int relative)) {
                degree = checked(currentDegree + relative);
                return true;
            }
            var numbered = Numbered.Match(input);
            if (numbered.Success && int.TryParse(numbered.Groups[1].Value, out int index) && index < count
                && int.TryParse(numbered.Groups[2].Value, out int numberedOctave)) {
                degree = checked(index + (numberedOctave - 4) * count);
                return true;
            }
            var match = Named.Match(input);
            if (!match.Success) return false;
            string spelled = char.ToUpperInvariant(match.Groups[1].Value[0]) + Normalise(match.Groups[2].Value);
            var candidate = Spellings(tuning).FirstOrDefault(s => s.Name == spelled);
            if (candidate.Name == null) return false;
            int octave;
            if (match.Groups[3].Success) {
                if (!int.TryParse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out octave)) return false;
            } else {
                // Without an octave, use the occurrence of the name nearest the current pitch.
                int nearest = Enumerable.Range(-1, 3)
                    .Select(c => candidate.Degree + (FloorDiv(currentDegree, count) + c) * count)
                    .OrderBy(d => Math.Abs(d - currentDegree)).First();
                octave = Octave(tuning, nearest);
            }
            int offset = 0;
            if (match.Groups[4].Success && !int.TryParse(match.Groups[4].Value.Replace(" ", ""), NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out offset)) return false;
            degree = checked(candidate.Degree + (octave - 4 - candidate.PeriodOffset) * count + offset);
            return true;
        }

        static string Normalise(string accidentals) => accidentals.Replace('#', '♯').Replace('b', '♭').Replace('^', '↑').Replace('v', '↓');
        static int Octave(TuningDefinition tuning, int degree) {
            int count = tuning.DegreeCount;
            int index = degree - FloorDiv(degree, count) * count;
            int offset = 0;
            foreach (var spelling in Spellings(tuning)) {
                if (spelling.Degree == index) { offset = spelling.PeriodOffset; break; }
            }
            return FloorDiv(degree, count) + 4 + offset;
        }
        static int FloorDiv(int value, int divisor) => (int)Math.Floor((double)value / divisor);

        /// <summary>A C-family alias high in the period belongs to the next octave (C♭5 sounds below C5); a B-family alias low in it, to the previous.</summary>
        static int AliasPeriodOffset(string alias, int index, int count) => alias.Length == 0 ? 0
            : alias[0] == 'C' && index > count / 2 ? 1 : alias[0] == 'B' && index < count / 2 ? -1 : 0;

        static List<(string Name, int Degree)> Naturals(TuningDefinition tuning) => Spellings(tuning)
            .Where(s => s.Name.Length == 1).GroupBy(s => s.Name).Select(g => (g.Key, g.First().Degree)).ToList();

        /// <summary>Plain-text names from custom then MOS letter profiles, or custom, verified, Bohlen–Pierce and Ups and Downs profiles, in that order.</summary>
        static IEnumerable<(string Name, int Degree, int PeriodOffset)> Spellings(TuningDefinition tuning) {
            // A letter-named MOS keyboard reuses A–G, so family names would collide with it.
            var ids = PitchNotation.IsAvailable(tuning, PitchNotation.Mos)
                ? new[] { "custom", PitchNotation.Mos } : new[] { "custom", "szg", "bp", "ups-downs" };
            foreach (string id in ids) {
                var profile = PitchNotation.FindProfile(tuning, id);
                if (profile == null) continue;
                foreach (var pair in profile.Names.OrderBy(p => p.Key)) {
                    if (pair.Key < 0 || pair.Key >= tuning.DegreeCount) continue;
                    // Names completed by a SMuFL glyph have no plain-text form; their aliases still apply.
                    if (!profile.AccidentalIds.ContainsKey(pair.Key)) {
                        yield return (pair.Value, pair.Key, profile.PeriodOffsets.TryGetValue(pair.Key, out int value) ? value : 0);
                    }
                    if (profile.Aliases.TryGetValue(pair.Key, out var aliases)) {
                        foreach (string alias in aliases) yield return (alias, pair.Key, AliasPeriodOffset(alias, pair.Key, tuning.DegreeCount));
                    }
                }
            }
        }
    }
}
