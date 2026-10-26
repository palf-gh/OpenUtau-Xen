using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenUtau.Core.Ustx {
    /// <summary>
    /// One of the 16 V4-style vibrato types. Values use V4's 0-127 scale, where 64 is the
    /// family's base rate and depth. The bases, value laws, start phase and onset follow V4's
    /// measured output; the vibrato stays OpenUtau's sine, and the phase and onset become its shift
    /// and fade-in.
    /// </summary>
    public sealed class VibratoPreset {
        /// <summary>V4's vibrato type number, 1-16, shared with JOB plugins.</summary>
        public int Id { get; }
        /// <summary>Xen's own family name, such as "standard", for <c>xen.vibrato.family.*</c>.</summary>
        public string Family { get; }
        /// <summary>The type within its family, 1-4, as in V4.</summary>
        public int Number { get; }
        /// <summary>English name, such as "Standard 1".</summary>
        public string Name { get; }
        public float BaseRate { get; }
        public float BaseDepth { get; }
        public int Rate { get; }
        public int Depth { get; }
        /// <summary>Type 4 holds its values for 60% of the vibrato, then fades both to 0 by 90%.</summary>
        public bool FadesOut { get; }
        /// <summary>The fraction of a cycle the vibrato has reached when it starts.</summary>
        public float StartPhase { get; }

        public VibratoPreset(int id, string family, int number, string name, float baseRate, float baseDepth,
                int rate, int depth, bool fadesOut, float startPhase) {
            Id = id;
            Family = family;
            Number = number;
            Name = name;
            BaseRate = baseRate;
            BaseDepth = baseDepth;
            Rate = rate;
            Depth = depth;
            FadesOut = fadesOut;
            StartPhase = startPhase;
        }

        // Value 64 is the base. VR is linear on each side of it: the top value doubles the rate and 0
        // halves it. VD is linear from 0. As in V4, the top value 127 counts as 128.
        public double RateHz(double value) => RateHz(BaseRate, value);
        public double DepthCents(double value) => DepthCents(BaseDepth, value);
        static double Top(double value) => value >= 127 ? 128 : value;
        public static double RateHz(double baseRate, double value) {
            value = Top(value);
            return baseRate * (value >= 64 ? 1 + (value - 64) / 64 : 1 + (value - 64) / 128);
        }
        public static double DepthCents(double baseDepth, double value) => baseDepth * Top(value) / 64;
        public static double RateValue(double baseRate, double hz) {
            double ratio = hz / baseRate;
            return Bottom(ratio >= 1 ? 64 + 64 * (ratio - 1) : 64 + 128 * (ratio - 1));
        }
        public static double DepthValue(double baseDepth, double cents) => Bottom(cents * 64 / baseDepth);
        // Values between 127 and 128 come from the top value.
        static double Bottom(double value) => value > 127 && value <= 128 ? 127 : value;

        /// <summary>Curves laid out over a vibrato of <paramref name="length"/> percent of the note.</summary>
        public (List<VibratoPoint> rate, List<VibratoPoint> depth) Curves(float length) {
            return (Lane(length, (float)RateHz(Rate), (float)RateHz(0), UVibrato.MinRateHz),
                Lane(length, (float)DepthCents(Depth), 0, 0));
        }

        List<VibratoPoint> Lane(float length, float value, float faded, float minimum) {
            float span = Math.Clamp(length, 0, 100) / 100f;
            float start = 1 - span;
            var points = new List<VibratoPoint> { new VibratoPoint(start, value) };
            if (FadesOut) {
                points.Add(new VibratoPoint(start + span * 0.6f, value));
                points.Add(new VibratoPoint(start + span * 0.9f, Math.Max(minimum, faded)));
            } else {
                points.Add(new VibratoPoint(1, value));
            }
            return points.GroupBy(point => point.x).Select(group => group.Last()).ToList();
        }
    }

    public static class VibratoPresets {
        public const float GenericRate = 5;
        public const float GenericDepth = 50;
        // Every measured type builds up linearly to full depth over this time, whatever the tempo,
        // note or vibrato length, and has no fade at its end.
        public const double OnsetSeconds = 0.34;

        // Rate (Hz) and depth (cents) at value 64, and the mean phase at the vibrato start (about a
        // quarter of a cycle, varying by a few milliseconds between notes), measured from V4's
        // vibrato pitch converted to PIT. Depth is the amplitude of the vibrato's fundamental.
        // Xen names the families itself (V4's Normal, Extreme, Fast and Slight) and keeps the
        // numbers: types 1-4 are Standard 1-4, 5-8 Wide, 9-12 Quick and 13-16 Subtle.
        static readonly (string family, string name, float rate, float depth, float phase)[] families = {
            ("standard", "Standard", 4.6685f, 60.86f, 0.271f), ("wide", "Wide", 3.5407f, 212.77f, 0.254f),
            ("quick", "Quick", 5.0798f, 101.69f, 0.285f), ("subtle", "Subtle", 4.6677f, 34.93f, 0.257f),
        };
        // Rate and depth values of types 1-4 in each family.
        static readonly int[,] normalValues = { { 50, 64 }, { 50, 40 }, { 50, 127 }, { 50, 64 } };
        static readonly int[,] extremeValues = { { 64, 64 }, { 32, 32 }, { 50, 100 }, { 64, 64 } };
        static readonly int[,] fastValues = { { 64, 64 }, { 50, 40 }, { 70, 80 }, { 64, 64 } };
        static readonly int[,] slightValues = { { 64, 64 }, { 64, 40 }, { 64, 72 }, { 64, 64 } };

        public static IReadOnlyList<VibratoPreset> All { get; } = Build();

        static List<VibratoPreset> Build() {
            var values = new[] { normalValues, extremeValues, fastValues, slightValues };
            var result = new List<VibratoPreset>();
            for (int family = 0; family < families.Length; family++) {
                for (int type = 0; type < 4; type++) {
                    var (key, name, rate, depth, phase) = families[family];
                    result.Add(new VibratoPreset(family * 4 + type + 1, key, type + 1, $"{name} {type + 1}", rate, depth,
                        values[family][type, 0], values[family][type, 1], type == 3, phase));
                }
            }
            return result;
        }

        public static VibratoPreset? Get(int? id) => id is >= 1 and <= 16 ? All[id.Value - 1] : null;
        public static float BaseRate(int? id) => Get(id)?.BaseRate ?? GenericRate;
        public static float BaseDepth(int? id) => Get(id)?.BaseDepth ?? GenericDepth;
        /// <summary>
        /// Sets the preset and its curves, laid out for the vibrato's current length, and expresses the
        /// type's start phase and onset through the shift and fades, which stay editable. The fade-in
        /// covers <see cref="OnsetSeconds"/> of a vibrato lasting <paramref name="vibratoSeconds"/>; it is
        /// left as it is when that duration is unknown.
        /// </summary>
        public static void Apply(UVibrato vibrato, int id, double? vibratoSeconds = null) {
            var preset = Get(id) ?? throw new ArgumentOutOfRangeException(nameof(id));
            var (rate, depth) = preset.Curves(vibrato.length);
            vibrato.preset = id;
            vibrato.rateCurve = rate;
            vibrato.depthCurve = depth;
            vibrato.shift = preset.StartPhase * 100;
            vibrato.@out = 0;
            if (vibratoSeconds > 0) {
                vibrato.@in = (float)Math.Min(100, OnsetSeconds / vibratoSeconds.Value * 100);
            }
        }
    }
}
