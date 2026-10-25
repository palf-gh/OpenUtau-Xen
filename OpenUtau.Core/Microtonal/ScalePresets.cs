using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenUtau.Core.Microtonal {
    /// <summary>
    /// Scales offered for a tuning, independent of its keyboard. A scale lists degrees counted from the tonic;
    /// presets are resolved by identifier, so only user-made scales are stored in the tuning.
    /// </summary>
    public static class ScalePresets {
        public const string All = "all";
        public const string Prefix = "preset.";
        /// <summary>Joins the keys of presets that have the same notes in a tuning into one identifier.</summary>
        public const char KeySeparator = '+';

        // Spellings as fifth indices from the tonic (C = 0, G = 1, F = −1); they map to any fifth-generated tuning.
        static readonly (string Id, string Name, int[] Fifths)[] Spelled = {
            ("major", "Major", new[] { 0, 2, 4, -1, 1, 3, 5 }),
            ("minor", "Natural minor", new[] { 0, 2, -3, -1, 1, -4, -2 }),
            ("harmonic-minor", "Harmonic minor", new[] { 0, 2, -3, -1, 1, -4, 5 }),
            ("melodic-minor", "Melodic minor", new[] { 0, 2, -3, -1, 1, 3, 5 }),
            ("dorian", "Dorian", new[] { 0, 2, -3, -1, 1, 3, -2 }),
            ("phrygian", "Phrygian", new[] { 0, -5, -3, -1, 1, -4, -2 }),
            ("lydian", "Lydian", new[] { 0, 2, 4, 6, 1, 3, 5 }),
            ("mixolydian", "Mixolydian", new[] { 0, 2, 4, -1, 1, 3, -2 }),
            ("locrian", "Locrian", new[] { 0, -5, -3, -1, -6, -4, -2 }),
            ("major-pentatonic", "Major pentatonic", new[] { 0, 2, 4, 1, 3 }),
            ("minor-pentatonic", "Minor pentatonic", new[] { 0, -3, -1, 1, -2 }),
            ("blues", "Blues", new[] { 0, -3, -1, 6, 1, -2 }),
            ("whole-tone", "Whole tone", new[] { 0, 2, 4, 6, 8, 10 }),
            ("miyakobushi", "Miyako-bushi", new[] { 0, -5, -1, 1, -4 }),
            ("ritsu", "Ritsu", new[] { 0, 2, -1, 1, 3 }),
            ("minyo", "Min'yō", new[] { 0, -3, -1, 1, -2 }),
            ("ryukyu", "Ryūkyū", new[] { 0, 4, -1, 1, 5 }),
            // Further Japanese scales; some share notes with the presets above but are listed under their own names.
            ("yonanuki-major", "Yonanuki major", new[] { 0, 2, 4, 1, 3 }),
            ("yonanuki-minor", "Yonanuki minor", new[] { 0, 2, -3, 1, -4 }),
            ("yo", "Yō", new[] { 0, 2, -1, 1, 3 }),
            ("in", "In", new[] { 0, -5, -1, 1, -4 }),
            ("hirajoshi", "Hirajōshi", new[] { 0, 2, -3, 1, -4 }),
            ("kumoi", "Kumoi", new[] { 0, 2, -3, 1, 3 }),
            ("iwato", "Iwato", new[] { 0, -5, -1, -6, -2 }),
        };

        // Arabic maqamat in 24-EDO quarter tones; the Rast preset tuning selects the first.
        static readonly (string Id, string Name, int[] Degrees)[] Maqamat = {
            ("maqam-rast", "Maqam Rast", new[] { 0, 4, 7, 10, 14, 18, 21 }),
            ("maqam-bayati", "Maqam Bayati", new[] { 0, 3, 6, 10, 14, 16, 20 }),
            ("maqam-hijaz", "Maqam Hijaz", new[] { 0, 2, 8, 10, 14, 16, 20 }),
            ("maqam-saba", "Maqam Saba", new[] { 0, 3, 6, 8, 14, 16, 20 }),
            ("maqam-nahawand", "Maqam Nahawand", new[] { 0, 4, 6, 10, 14, 16, 22 }),
            ("maqam-kurd", "Maqam Kurd", new[] { 0, 2, 6, 10, 14, 16, 20 }),
            ("maqam-ajam", "Maqam Ajam", new[] { 0, 4, 8, 10, 14, 18, 22 }),
        };

        /// <summary>
        /// All scales for the tuning: all degrees, every preset the tuning can express, then saved scales. Presets with
        /// the same notes become one entry whose identifier joins their keys (preset.major-pentatonic+yonanuki-major)
        /// and whose name lists their names.
        /// </summary>
        public static IReadOnlyList<ScaleDefinition> For(TuningDefinition tuning) {
            var list = new List<ScaleDefinition> { new ScaleDefinition { Id = All, Name = "All pitches", Degrees = Enumerable.Range(0, tuning.DegreeCount).ToList() } };
            foreach (var group in PresetsFor(tuning).GroupBy(scale => string.Join(",", scale.Degrees))) {
                var first = group.First();
                list.Add(new ScaleDefinition {
                    Id = Prefix + string.Join(KeySeparator, group.Select(Key)),
                    Name = string.Join(" / ", group.Select(scale => scale.Name)),
                    Degrees = first.Degrees,
                });
            }
            list.AddRange(tuning.Scales.Where(scale => scale.Id != All && list.All(preset => preset.Id != scale.Id)));
            return list;
        }

        public static bool IsPreset(string? id) => id == All || id != null && id.StartsWith(Prefix, StringComparison.Ordinal);

        /// <summary>The preset keys of an identifier: one for a single preset, several for a merged entry.</summary>
        public static string[] Keys(string id) => id.StartsWith(Prefix, StringComparison.Ordinal)
            ? id[Prefix.Length..].Split(KeySeparator) : Array.Empty<string>();

        static string Key(ScaleDefinition preset) => preset.Id[Prefix.Length..];

        /// <summary>
        /// Finds a saved scale or preset. A preset identifier also matches the merged entry that contains its key, so
        /// a scale chosen before entries were merged, or merged differently in another tuning, still resolves.
        /// </summary>
        public static ScaleDefinition? Find(TuningDefinition tuning, string? id) {
            if (id == null) return null;
            var saved = tuning.Scales.FirstOrDefault(scale => scale.Id == id);
            if (saved != null) return saved;
            var choices = For(tuning);
            var keys = Keys(id);
            return choices.FirstOrDefault(scale => scale.Id == id)
                ?? choices.FirstOrDefault(scale => keys.Length > 0 && Keys(scale.Id).Intersect(keys).Any());
        }

        /// <summary>The active scale's degrees in the period, counted from the tonic; null shows every degree.</summary>
        public static IReadOnlyList<int>? ActiveDegrees(TuningDefinition tuning) {
            var scale = Find(tuning, tuning.Keyboard.ActiveScaleId);
            if (scale == null || scale.Degrees.Count == 0) return null;
            int n = tuning.DegreeCount;
            int tonic = tuning.Keyboard.Tonic;
            return scale.Degrees.Select(d => ((tonic + d) % n + n) % n).Distinct().OrderBy(d => d).ToList();
        }

        static IEnumerable<ScaleDefinition> PresetsFor(TuningDefinition tuning) {
            int n = tuning.DegreeCount;
            bool octave = Math.Abs(tuning.PeriodCents - 1200) < 1e-6;
            int? fifth = null;
            var theory = KeyboardTheory.For(tuning);
            if (octave && theory != null && theory.IsValid && theory.ReferenceDivision == 31) fifth = theory.Generator;
            else if (octave && tuning.Divisions == 0 && n == 12) fifth = 7;
            if (fifth is int g) {
                foreach (var (id, name, fifths) in Spelled) {
                    var degrees = MapFifths(n, g, fifths);
                    if (degrees != null) yield return Preset(id, name, degrees);
                }
            }
            if (octave && n == 24) {
                foreach (var (id, name, degrees) in Maqamat) yield return Preset(id, name, degrees);
            }
            if (n == 13 && Math.Abs(tuning.PeriodCents - 1200 * Math.Log2(3)) < 1e-6) {
                yield return Preset("lambda", "Lambda", new[] { 0, 2, 3, 4, 6, 7, 9, 10, 12 });
            }
            if (tuning.Id == TuningPresets.PelogId) {
                yield return Preset("pelog-nem", "Pathet nem / lima", new[] { 0, 1, 2, 4, 5 });
                yield return Preset("pelog-barang", "Pathet barang", new[] { 1, 2, 4, 5, 6 });
            }
            if (tuning.Id == TuningPresets.AeuId) {
                yield return Preset("makam-rast", "Makam Rast", new[] { 0, 4, 7, 10, 14, 18, 21 });
            }
        }

        /// <summary>
        /// Maps a spelling through the tuning's fifth; null when two notes collide or the letters fall out of the
        /// order they have in twelve-note music, so a preset is left out rather than shown wrongly.
        /// </summary>
        static int[]? MapFifths(int n, int fifth, int[] fifths) {
            int Mod(long value, int m) => (int)((value % m + m) % m);
            var ordered = fifths.OrderBy(k => Mod(7L * k, 12)).ToArray();
            var degrees = ordered.Select(k => Mod((long)k * fifth, n)).ToArray();
            for (int i = 1; i < degrees.Length; i++) if (degrees[i] <= degrees[i - 1]) return null;
            return degrees;
        }

        static ScaleDefinition Preset(string id, string name, IEnumerable<int> degrees) =>
            new ScaleDefinition { Id = Prefix + id, Name = name, Degrees = degrees.ToList() };
    }
}
