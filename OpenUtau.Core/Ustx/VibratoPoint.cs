using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenUtau.Core.Ustx {
    public class VibratoPoint {
        // Position across the whole note, from 0 to 1. Values are Hz or cents.
        public float x;
        public float y;

        public VibratoPoint() { }
        public VibratoPoint(float x, float y) {
            this.x = x;
            this.y = y;
        }
        public VibratoPoint Clone() => new VibratoPoint(x, y);
    }

    public static class VibratoCurve {
        public static float Sample(List<VibratoPoint>? points, float position, float fallback) {
            if (points == null || points.Count == 0) {
                return fallback;
            }
            if (position <= points[0].x) {
                return points[0].y;
            }
            for (int i = 1; i < points.Count; ++i) {
                if (position <= points[i].x) {
                    var left = points[i - 1];
                    var right = points[i];
                    return left.y + (right.y - left.y) * (position - left.x) / (right.x - left.x);
                }
            }
            return points[^1].y;
        }

        public static List<VibratoPoint>? Validate(List<VibratoPoint>? points, float minimum, float maximum) {
            if (points == null || points.Count == 0) {
                return null;
            }
            if (points.Count > 10000 || points.Any(point => point == null ||
                    !float.IsFinite(point.x) || point.x < 0 || point.x > 1 ||
                    !float.IsFinite(point.y) || point.y < minimum || point.y > maximum)) {
                throw new InvalidDataException("Invalid vibrato curve position or value.");
            }
            return points.GroupBy(point => point.x).OrderBy(group => group.Key)
                .Select(group => group.Last()).ToList();
        }

        public static List<VibratoPoint>? Slice(List<VibratoPoint>? points, float start, float end) {
            if (points == null || points.Count == 0) {
                return null;
            }
            var result = new List<VibratoPoint> { new VibratoPoint(0, Sample(points, start, 0)) };
            result.AddRange(points.Where(point => point.x > start && point.x < end)
                .Select(point => new VibratoPoint((point.x - start) / (end - start), point.y)));
            result.Add(new VibratoPoint(1, Sample(points, end, 0)));
            return result;
        }
    }
}
