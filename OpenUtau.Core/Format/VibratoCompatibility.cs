using System;
using System.Collections.Generic;
using System.Linq;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;

namespace OpenUtau.Core.Format {
    /// <summary>Projects editable vibrato curves onto legacy pitch points without changing the source.</summary>
    public static class VibratoCompatibility {
        public static UPitch Bake(UProject project, UVoicePart part, UNote note, UPitch pitch) {
            if (!note.vibrato.HasCurves || note.vibrato.length <= 0) {
                return pitch.Clone();
            }
            var vibrato = note.vibrato.Clone();
            vibrato.ValidateCurves();
            double origin = project.timeAxis.TickPosToMsPos(part.position + note.position);
            double end = project.timeAxis.TickPosToMsPos(part.position + note.End) - origin;
            float maxRate = Math.Max(1000f / vibrato.period,
                vibrato.rateCurve?.Max(p => p.y) ?? 0);
            double step = Math.Min(2, 1000.0 / (128 * maxRate));
            double first = Math.Min(0, pitch.data.Count > 0 ? pitch.data[0].X : 0);
            double last = Math.Max(end, pitch.data.Count > 0 ? pitch.data[^1].X : end);
            if ((last - first) / step + pitch.data.Count > 200000) {
                throw new InvalidOperationException("Vibrato compatibility export would require more than 200,000 pitch points in one note.");
            }
            var positions = new SortedSet<double>(pitch.data.Select(p => (double)p.X));
            positions.Add(0);
            positions.Add(end);
            double start = project.timeAxis.TickPosToMsPos(part.position + note.position
                + note.duration * vibrato.NormalizedStart) - origin;
            positions.Add(start);
            foreach (var point in (vibrato.rateCurve ?? new List<VibratoPoint>())
                .Concat(vibrato.depthCurve ?? new List<VibratoPoint>())) {
                positions.Add(project.timeAxis.TickPosToMsPos(part.position + note.position
                    + note.duration * point.x) - origin);
            }
            for (double x = first; x < last; x += step) {
                positions.Add(x);
            }
            var result = new UPitch { snapFirst = false };
            foreach (double x in positions) {
                double tick = project.timeAxis.MsPosToNonExactTickPos(origin + x) - part.position;
                double deviation = x >= 0 && x <= end
                    ? (vibrato.EvaluateAtTick(project, part, note, tick).Y - note.AdjustedTone) * 10
                    : 0;
                result.data.Add(new PitchPoint((float)x, (float)(Sample(pitch, x) + deviation), PitchPointShape.l));
            }
            return result;
        }

        static double Sample(UPitch pitch, double x) {
            var points = pitch.data;
            if (points.Count == 0) return 0;
            if (x <= points[0].X) return points[0].Y;
            if (x >= points[^1].X) return points[^1].Y;
            int i = 0;
            while (i + 1 < points.Count && points[i + 1].X < x) ++i;
            var left = points[i];
            var right = points[i + 1];
            if (left.shape == PitchPointShape.sp && points.Count > 2) {
                var before = i > 0 ? points[i - 1] : left;
                var after = i + 2 < points.Count ? points[i + 2] : right;
                return new CubicSplineSegment(before.X, before.Y, left.X, left.Y,
                    right.X, right.Y, after.X, after.Y).GetY(x);
            }
            return MusicMath.InterpolateShape(left.X, right.X, left.Y, right.Y, x, left.shape);
        }
    }
}
