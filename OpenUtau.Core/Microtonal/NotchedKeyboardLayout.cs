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
        /// <summary>The black anchor's owner, or -1 for runs not grouped by owner.</summary>
        public int Owner { get; }
        public IReadOnlyList<int> Degrees { get; }
        public double Depth { get; }
        internal NotchedKeyColumn(int index, int runIndex, NotchedKeyKind kind, int owner, int[] degrees, double depth) {
            Index = index; RunIndex = runIndex; Kind = kind; Owner = owner; Degrees = Array.AsReadOnly(degrees); Depth = depth;
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
    /// A horizontal keyboard tiled by notched key shapes. Coordinates use one unit per column
    /// and a total depth of one; no physical pitch-to-row conversion is performed here.
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
        public double Width => Columns.Count;
        public double Height => 1;

        NotchedKeyboardLayout(NotchedKeyColumn[] columns, NotchedKeyShape[] keys, Slab[] slabs) {
            Columns = Array.AsReadOnly(columns); Keys = Array.AsReadOnly(keys); this.slabs = slabs;
        }

        public static NotchedKeyboardLayout Create(IEnumerable<NotchedKeyStep> steps) {
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
                    && (!IsBlack(ordered[start].Kind) || ordered[end].Owner == ordered[start].Owner)) end++;
                int length = end - start;
                int count = (length + 4) / 5;
                int small = length / count, remainder = length % count;
                int position = start;
                for (int column = 0; column < count; column++) {
                    int size = small + (column < remainder ? 1 : 0);
                    var degrees = ordered.Skip(position).Take(size).Select(step => step.Degree).ToArray();
                    int index = columns.Count;
                    var kind = ordered[start].Kind;
                    columns.Add(new NotchedKeyColumn(index, runIndex, kind, IsBlack(kind) ? ordered[start].Owner : -1, degrees, Depth(kind) / deepest));
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
