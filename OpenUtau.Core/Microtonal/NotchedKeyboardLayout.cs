using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenUtau.Core.Microtonal {
    public enum NotchedKeyKind { Natural, Between, Flat, Sharp, Shared, Conflict }

    public readonly struct NotchedKeyStep {
        public int Degree { get; }
        public NotchedKeyKind Kind { get; }
        public int Owner { get; }
        public NotchedKeyStep(int degree, NotchedKeyKind kind, int owner = -1) {
            Degree = degree;
            Kind = kind;
            Owner = owner;
        }
    }

    public readonly struct NotchedKeyRectangle {
        public double X { get; }
        public double Y { get; }
        public double Right { get; }
        public double Bottom { get; }
        public double Width => Right - X;
        public double Height => Bottom - Y;
        public NotchedKeyRectangle(double x, double y, double width, double height) {
            X = x; Y = y; Right = x + width; Bottom = y + height;
        }
        NotchedKeyRectangle(double left, double top, double right, double bottom, bool bounds) {
            X = left; Y = top; Right = right; Bottom = bottom;
        }
        internal static NotchedKeyRectangle FromBounds(double left, double top, double right, double bottom) =>
            new NotchedKeyRectangle(left, top, right, bottom, true);
        public bool Contains(double x, double y) => x >= X && x < Right && y >= Y && y < Bottom;
    }

    public sealed class NotchedKeyColumn {
        public int Index { get; }
        public int RunIndex { get; }
        public NotchedKeyKind Kind { get; }
        /// <summary>The natural or black anchor's owner, or -1 when the owner is unknown.</summary>
        public int Owner { get; }
        public IReadOnlyList<int> Degrees { get; }
        public double Depth { get; }
        public double X { get; }
        public double Width { get; }
        internal NotchedKeyColumn(int index, int runIndex, NotchedKeyKind kind, int owner, int[] degrees, double depth,
            double? x = null, double width = 1) {
            Index = index; RunIndex = runIndex; Kind = kind; Owner = owner; Degrees = Array.AsReadOnly(degrees); Depth = depth;
            X = x ?? index; Width = width;
        }
    }

    public sealed class NotchedKeyShape {
        readonly List<NotchedKeyRectangle> rectangles = new List<NotchedKeyRectangle>();
        public int Degree { get; }
        public NotchedKeyKind Kind { get; }
        public int Owner { get; }
        public int Column { get; }
        public int RunIndex { get; }
        public IReadOnlyList<NotchedKeyRectangle> Rectangles { get; }
        internal NotchedKeyShape(int degree, NotchedKeyKind kind, int owner, int column, int runIndex) {
            Degree = degree; Kind = kind; Owner = owner; Column = column; RunIndex = runIndex;
            Rectangles = rectangles.AsReadOnly();
        }
        internal void Add(double left, double top, double right, double bottom) {
            if (rectangles.Count > 0) {
                var previous = rectangles[rectangles.Count - 1];
                if (Math.Abs(previous.X - left) < 1e-12 && Math.Abs(previous.Right - right) < 1e-12
                    && Math.Abs(previous.Bottom - top) < 1e-12) {
                    rectangles[rectangles.Count - 1] = NotchedKeyRectangle.FromBounds(left, previous.Y, right, bottom);
                    return;
                }
            }
            rectangles.Add(NotchedKeyRectangle.FromBounds(left, top, right, bottom));
        }
    }

    /// <summary>
    /// A horizontal keyboard tiled by notched key shapes. Generic layouts use one unit per
    /// column; traditional piano and Bohlen–Pierce profiles use one unit per white front. Total depth is
    /// one, and no physical pitch-to-row conversion is performed here.
    /// </summary>
    public sealed class NotchedKeyboardLayout {
        sealed class Span {
            public double Right { get; }
            public NotchedKeyShape Key { get; }
            public Span(double right, NotchedKeyShape key) { Right = right; Key = key; }
        }
        sealed class Slab {
            public double Bottom { get; }
            public Span[] Spans { get; }
            public Slab(double bottom, Span[] spans) { Bottom = bottom; Spans = spans; }
        }
        readonly Slab[] slabs;
        public IReadOnlyList<NotchedKeyColumn> Columns { get; }
        public IReadOnlyList<NotchedKeyShape> Keys { get; }
        public double Width { get; }
        public double Height => 1;
        public bool IsTraditionalKeyboard { get; }

        NotchedKeyboardLayout(NotchedKeyColumn[] columns, NotchedKeyShape[] keys, Slab[] slabs,
            double? width = null, bool traditionalKeyboard = false) {
            Columns = Array.AsReadOnly(columns); Keys = Array.AsReadOnly(keys); this.slabs = slabs;
            IsTraditionalKeyboard = traditionalKeyboard; Width = width ?? columns.Length;
        }

        public static NotchedKeyboardLayout Create(IEnumerable<NotchedKeyStep> steps, bool preferTraditionalKeyboard = false) {
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            var ordered = steps.ToArray();
            for (int i = 0; i < ordered.Length; i++) {
                if (!Enum.IsDefined(typeof(NotchedKeyKind), ordered[i].Kind)) throw new ArgumentOutOfRangeException(nameof(steps));
                if (i > 0 && ordered[i].Degree <= ordered[i - 1].Degree) {
                    throw new ArgumentException("Keyboard degrees must be strictly increasing.", nameof(steps));
                }
            }
            if (ordered.Length == 0) return new NotchedKeyboardLayout(Array.Empty<NotchedKeyColumn>(), Array.Empty<NotchedKeyShape>(), Array.Empty<Slab>());
            bool IsBlack(NotchedKeyKind kind) => kind == NotchedKeyKind.Flat || kind == NotchedKeyKind.Sharp || kind == NotchedKeyKind.Shared;
            if (preferTraditionalKeyboard && (ordered.Length == 12 || ordered.Length == 13)
                && ordered.Select(step => step.Degree).SequenceEqual(Enumerable.Range(0, ordered.Length))) {
                var naturalDegrees = ordered.Length == 12 ? new[] { 0, 2, 4, 5, 7, 9, 11 } : new[] { 0, 2, 3, 4, 6, 7, 9, 10, 12 };
                var naturals = new HashSet<int>(naturalDegrees);
                if (ordered.All(step => naturals.Contains(step.Degree) ? step.Kind == NotchedKeyKind.Natural : IsBlack(step.Kind))) {
                    return TraditionalKeyboard(ordered, naturalDegrees);
                }
            }
            double Depth(NotchedKeyKind kind) => kind == NotchedKeyKind.Natural ? 1
                : kind == NotchedKeyKind.Between || kind == NotchedKeyKind.Conflict ? 2.0 / 3 : 1.0 / 3;
            // A fallback containing no natural keys still fills its complete outer rectangle.
            double deepest = ordered.Max(step => Depth(step.Kind));
            var columns = new List<NotchedKeyColumn>();
            var keys = new List<NotchedKeyShape>();
            var byDegree = new Dictionary<int, NotchedKeyShape>();
            int runIndex = 0;
            for (int start = 0; start < ordered.Length;) {
                int end = start + 1;
                while (end < ordered.Length && ordered[end].Kind == ordered[start].Kind
                    && (!(IsBlack(ordered[start].Kind) || ordered[start].Kind == NotchedKeyKind.Natural)
                        || ordered[end].Owner == ordered[start].Owner)) end++;
                int length = end - start;
                int count = (length + 4) / 5;
                int small = length / count, remainder = length % count;
                int position = start;
                for (int column = 0; column < count; column++) {
                    int size = small + (column < remainder ? 1 : 0);
                    var degrees = ordered.Skip(position).Take(size).Select(step => step.Degree).ToArray();
                    int index = columns.Count;
                    var kind = ordered[start].Kind;
                    columns.Add(new NotchedKeyColumn(index, runIndex, kind,
                        IsBlack(kind) || kind == NotchedKeyKind.Natural ? ordered[start].Owner : -1, degrees, Depth(kind) / deepest));
                    foreach (var step in ordered.Skip(position).Take(size)) {
                        var key = new NotchedKeyShape(step.Degree, kind, step.Owner, index, runIndex);
                        keys.Add(key); byDegree.Add(step.Degree, key);
                    }
                    position += size;
                }
                start = end;
                runIndex++;
            }
            var boundaries = columns.SelectMany(column => Enumerable.Range(0, column.Degrees.Count + 1)
                .Select(i => column.Depth * i / column.Degrees.Count)).Append(1).OrderBy(value => value).ToArray();
            var depths = new List<double>();
            foreach (double value in boundaries) {
                if (depths.Count == 0 || value - depths[depths.Count - 1] > 1e-12) depths.Add(value);
            }
            var slabs = new List<Slab>();
            for (int depth = 0; depth + 1 < depths.Count; depth++) {
                double top = depths[depth], bottom = depths[depth + 1], middle = (top + bottom) / 2;
                var active = columns.Where(column => column.Depth > middle).ToArray();
                var spans = new List<Span>();
                double left = 0;
                for (int i = 0; i < active.Length; i++) {
                    var column = active[i];
                    double right = i + 1 == active.Length ? columns.Count : (column.Index + active[i + 1].Index + 1) / 2.0;
                    int fromTop = Math.Min(column.Degrees.Count - 1, (int)(middle / column.Depth * column.Degrees.Count));
                    var key = byDegree[column.Degrees[column.Degrees.Count - 1 - fromTop]];
                    key.Add(left, top, right, bottom);
                    spans.Add(new Span(right, key));
                    left = right;
                }
                slabs.Add(new Slab(bottom, spans.ToArray()));
            }
            return new NotchedKeyboardLayout(columns.ToArray(), keys.ToArray(), slabs.ToArray());
        }

        static NotchedKeyboardLayout TraditionalKeyboard(NotchedKeyStep[] steps, int[] whiteDegrees) {
            const double blackDepth = 0.62;
            var keys = steps.Select(step => new NotchedKeyShape(step.Degree, step.Kind, step.Owner, step.Degree, step.Degree)).ToArray();
            var upper = steps.Select(step => {
                int white = Array.BinarySearch(whiteDegrees, step.Degree);
                if (white < 0) {
                    double centre = ~white;
                    return NotchedKeyRectangle.FromBounds(centre - 0.3, 0, centre + 0.3, blackDepth);
                }
                double left = white, right = white + 1;
                if (step.Degree > 0 && steps[step.Degree - 1].Kind != NotchedKeyKind.Natural) left += 0.3;
                if (step.Degree + 1 < steps.Length && steps[step.Degree + 1].Kind != NotchedKeyKind.Natural) right -= 0.3;
                return NotchedKeyRectangle.FromBounds(left, 0, right, blackDepth);
            }).ToArray();
            var columns = steps.Select(step => new NotchedKeyColumn(step.Degree, step.Degree, step.Kind, step.Owner,
                new[] { step.Degree }, step.Kind == NotchedKeyKind.Natural ? 1 : blackDepth,
                upper[step.Degree].X, upper[step.Degree].Width)).ToArray();
            var top = new Span[steps.Length];
            for (int i = 0; i < keys.Length; i++) {
                keys[i].Add(upper[i].X, 0, upper[i].Right, blackDepth);
                top[i] = new Span(upper[i].Right, keys[i]);
            }
            var bottom = new Span[whiteDegrees.Length];
            for (int i = 0; i < whiteDegrees.Length; i++) {
                var key = keys[whiteDegrees[i]];
                key.Add(i, blackDepth, i + 1, 1);
                bottom[i] = new Span(i + 1, key);
            }
            return new NotchedKeyboardLayout(columns, keys, new[] { new Slab(blackDepth, top), new Slab(1, bottom) }, whiteDegrees.Length, true);
        }

        /// <summary>Returns the owning degree throughout a key, including every notch extension.</summary>
        public NotchedKeyShape? HitTest(double x, double y) {
            if (!double.IsFinite(x) || !double.IsFinite(y) || x < 0 || y < 0 || x > Width || y > Height || slabs.Length == 0) return null;
            // The outer right and bottom edges belong to their adjacent keys.
            if (x == Width) x = Math.BitDecrement(x);
            if (y == Height) y = Math.BitDecrement(y);
            int first = 0, last = slabs.Length - 1;
            while (first < last) {
                int middle = first + (last - first) / 2;
                if (y < slabs[middle].Bottom) last = middle; else first = middle + 1;
            }
            var spans = slabs[first].Spans;
            first = 0; last = spans.Length - 1;
            while (first < last) {
                int middle = first + (last - first) / 2;
                if (x < spans[middle].Right) last = middle; else first = middle + 1;
            }
            return spans[first].Key;
        }
    }
}
