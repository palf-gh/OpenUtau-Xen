using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenUtau.Core.Ustx {
    /// <summary>
    /// One of the 16 VOCALOID-style vibrato types. Values use VOCALOID's 0-127 scale, where 64 is the
    /// family's base rate and depth. The bases approximate the installed editor's recorded vibratos.
    /// </summary>
    public sealed class VibratoPreset {
        public int Id { get; }
        public string Name { get; }
        public float BaseRate { get; }
        public float BaseDepth { get; }
        public int Rate { get; }
        public int Depth { get; }
        /// <summary>Type 4 holds its values for 60% of the vibrato, then fades both to 0 by 90%.</summary>
        public bool FadesOut { get; }

        public VibratoPreset(int id, string name, float baseRate, float baseDepth, int rate, int depth, bool fadesOut) {
            Id = id;
            Name = name;
            BaseRate = baseRate;
            BaseDepth = baseDepth;
            Rate = rate;
            Depth = depth;
            FadesOut = fadesOut;
        }

        // VR doubles the rate every 64 units; VD is linear. Value 64 is the base.
        public double RateHz(double value) => RateHz(BaseRate, value);
        public double DepthCents(double value) => DepthCents(BaseDepth, value);
        public static double RateHz(double baseRate, double value) => baseRate * Math.Pow(2, (value - 64) / 64);
        public static double DepthCents(double baseDepth, double value) => baseDepth * value / 64;
        public static double RateValue(double baseRate, double hz) => 64 + 64 * Math.Log(hz / baseRate, 2);
        public static double DepthValue(double baseDepth, double cents) => cents * 64 / baseDepth;

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

        // Rate (Hz) and depth (cents) at value 64, rounded from the sustained part of each family's
        // recording: Normal, Extreme, Fast and Slight.
        static readonly (string name, float rate, float depth)[] families = {
            ("Normal", 4.85f, 75), ("Extreme", 3.9f, 300), ("Fast", 5.2f, 110), ("Slight", 4.85f, 35),
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
                    var (name, rate, depth) = families[family];
                    result.Add(new VibratoPreset(family * 4 + type + 1, $"[{name}] Type {type + 1}", rate, depth,
                        values[family][type, 0], values[family][type, 1], type == 3));
                }
            }
            return result;
        }

        public static VibratoPreset? Get(int? id) => id is >= 1 and <= 16 ? All[id.Value - 1] : null;
        public static float BaseRate(int? id) => Get(id)?.BaseRate ?? GenericRate;
        public static float BaseDepth(int? id) => Get(id)?.BaseDepth ?? GenericDepth;

        /// <summary>Sets the preset and its curves, laid out for the vibrato's current length.</summary>
        public static void Apply(UVibrato vibrato, int id) {
            var preset = Get(id) ?? throw new ArgumentOutOfRangeException(nameof(id));
            var (rate, depth) = preset.Curves(vibrato.length);
            vibrato.preset = id;
            vibrato.rateCurve = rate;
            vibrato.depthCurve = depth;
        }
    }
}
