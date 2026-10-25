using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using OpenUtau.Core;
using OpenUtau.Core.Microtonal;
using Serilog;

namespace OpenUtau.App.Controls {
    /// <summary>A one-equave keyboard whose drawing and pointer selection share the same notched shapes.</summary>
    public sealed class NotchedKeyboardPreview : Control {
        double ColumnWidth => KeyLayout?.IsTraditionalKeyboard == true ? 56 : 36;
        readonly Dictionary<int, StreamGeometry> geometries = new Dictionary<int, StreamGeometry>();
        TuningDefinition? tuning;
        MosKeyboard? baseline;
        bool whiteOnly;
        KeyboardPitchCell[] cells = Array.Empty<KeyboardPitchCell>();
        HashSet<int> active = new HashSet<int>();
        readonly KeyboardAudition audition;
        readonly HashSet<Key> auditionKeys = new HashSet<Key>();
        IPointer? auditionPointer;
        bool releasingCapture;
        Rect viewport;
        public NotchedKeyboardLayout? KeyLayout { get; private set; }
        public IReadOnlyList<KeyboardPitchCell> Cells => cells;
        public int ItemCount => cells.Length;
        public int? SelectedDegree { get; private set; }
        public bool IsWhiteKeysOnly => whiteOnly;

        public NotchedKeyboardPreview() : this(frequency => PlaybackManager.Inst.PlayTone(frequency),
            frequency => PlaybackManager.Inst.EndTone(frequency)) { }

        public NotchedKeyboardPreview(Action<double> playTone, Action<double> endTone) {
            audition = new KeyboardAudition(playTone, endTone);
            Focusable = true;
            Height = 200;
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            EffectiveViewportChanged += (_, e) => { viewport = e.EffectiveViewport; InvalidateVisual(); };
            ActualThemeVariantChanged += (_, _) => InvalidateVisual();
            PointerCaptureLost += (_, _) => { if (!releasingCapture) StopAudition(); };
        }

        public void SetTuning(TuningDefinition? value, IEnumerable<int>? scale = null, bool whiteKeysOnly = false) {
            StopAudition();
            var nextBaseline = value == null ? null : KeyboardRegionizer.Model(value);
            bool sameGeometry = whiteOnly == whiteKeysOnly && value != null && tuning != null && baseline != null && ReferenceEquals(baseline, nextBaseline)
                && tuning.Keyboard.Classes.Count == value.Keyboard.Classes.Count
                && tuning.Keyboard.Classes.All(pair => value.Keyboard.Classes.TryGetValue(pair.Key, out var kind) && kind == pair.Value);
            tuning = value?.Clone();
            whiteOnly = whiteKeysOnly;
            baseline = nextBaseline;
            if (!sameGeometry) geometries.Clear();
            if (tuning == null) {
                cells = Array.Empty<KeyboardPitchCell>(); KeyLayout = null; active.Clear(); SelectedDegree = null;
            } else {
                if (!sameGeometry) {
                    bool letters = KeyboardRegionizer.LetterScale(tuning) != null;
                    cells = Enumerable.Range(0, tuning.DegreeCount).Select(step => {
                        if (whiteOnly) return new KeyboardPitchCell(step, NotchedKeyKind.Natural, tuning.DegreeCount + step, step);
                        var cell = baseline?.Cells[step];
                        var role = cell != null && (letters || !tuning.Keyboard.Classes.ContainsKey(step)) ? cell.Role : PitchNotation.Role(tuning, step);
                        if (cell != null && cell.Role == role) return cell;
                        // Explicit key classes retain precedence over inferred keyboard roles.
                        var kind = role == KeyRole.White ? NotchedKeyKind.Natural
                            : role == KeyRole.Black ? NotchedKeyKind.Shared : NotchedKeyKind.Between;
                        return new KeyboardPitchCell(step, kind, tuning.DegreeCount + step, step);
                    }).ToArray();
                    // Letter-named MOS keyboards use the generic layout even in 12-EDO and 13-EDT.
                    bool traditional = !letters && !whiteOnly
                        // Unequal twelve-note octaves (just intonation, well temperaments) also use the piano profile
                        // when their keys follow the piano pattern; the layout checks the pattern itself.
                        && (tuning.DegreeCount == 12 && Math.Abs(tuning.PeriodCents - 1200) < 1e-6
                        || tuning.DegreeCount == 13 && Math.Abs(tuning.PeriodCents - 1200 * Math.Log2(3)) < 1e-6);
                    KeyLayout = NotchedKeyboardLayout.Create(cells.Select(cell => new NotchedKeyStep(cell.Step, cell.Kind, cell.Owner)), traditional);
                }
                active = new HashSet<int>((scale ?? Enumerable.Range(0, cells.Length)).Select(step => (step % cells.Length + cells.Length) % cells.Length));
                if (SelectedDegree >= cells.Length) SelectedDegree = null;
            }
            UpdateAccessibleName();
            InvalidateMeasure();
            InvalidateVisual();
        }

        protected override Size MeasureOverride(Size availableSize) => new Size((KeyLayout?.Width ?? 0) * ColumnWidth, Height);

        protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);

        void UpdateAccessibleName() {
            string name = $"{XenStrings.Get("field.display")} {ItemCount}";
            if (SelectedDegree is int degree && tuning != null) {
                name += $" · {Label(degree)} · {XenStrings.Get("field.degree")} {degree}";
            }
            AutomationProperties.SetName(this, name);
        }

        string Label(int degree) => tuning == null ? "" : whiteOnly ? PitchNotation.Format(tuning, degree, "degree")
            : PitchNotation.Format(tuning, degree, PitchNotation.ResolvePrimary(tuning, null));

        public int? SelectDegreeAt(Point point) {
            SelectedDegree = Bounds.Height <= 0 ? null : KeyLayout?.HitTest(point.X / ColumnWidth, point.Y / Bounds.Height)?.Degree;
            RunAudition(() => audition.SetSelection(tuning, SelectedDegree));
            UpdateAccessibleName();
            InvalidateVisual();
            return SelectedDegree;
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e) {
            base.OnPointerPressed(e);
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            if (auditionPointer != null && !ReferenceEquals(auditionPointer, e.Pointer)) StopPointerAudition();
            Focus();
            SelectDegreeAt(e.GetPosition(this));
            auditionPointer = e.Pointer;
            e.Pointer.Capture(this);
            RunAudition(() => audition.BeginPointer(tuning, SelectedDegree));
            e.Handled = true;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e) {
            base.OnPointerReleased(e);
            if (!ReferenceEquals(e.Pointer, auditionPointer) || e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            StopPointerAudition();
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e) {
            base.OnPointerMoved(e);
            if (ReferenceEquals(e.Pointer, auditionPointer)) {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) SelectDegreeAt(e.GetPosition(this));
                else StopPointerAudition();
            }
            var key = Bounds.Height <= 0 ? null : KeyLayout?.HitTest(e.GetPosition(this).X / ColumnWidth, e.GetPosition(this).Y / Bounds.Height);
            if (key != null && tuning != null) {
                ToolTip.SetTip(this, $"{Label(key.Degree)} · {key.Degree} · {tuning.GetCents(key.Degree).ToString("0.##", XenStrings.InputCulture)} ¢");
            }
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            base.OnKeyDown(e);
            if (ItemCount == 0) return;
            if (e.Key == Key.Space || e.Key == Key.Enter) {
                SelectedDegree ??= 0;
                auditionKeys.Add(e.Key);
                RunAudition(() => audition.BeginKeyboard(tuning, SelectedDegree));
                UpdateAccessibleName();
                InvalidateVisual();
                e.Handled = true;
                return;
            }
            int next = SelectedDegree ?? 0;
            switch (e.Key) {
                case Key.Left: case Key.Down: next--; break;
                case Key.Right: case Key.Up: next++; break;
                case Key.Home: next = 0; break;
                case Key.End: next = ItemCount - 1; break;
                default: return;
            }
            SelectedDegree = Math.Clamp(next, 0, ItemCount - 1);
            RunAudition(() => audition.SetSelection(tuning, SelectedDegree));
            UpdateAccessibleName();
            var shape = KeyLayout!.Keys[SelectedDegree.Value];
            var column = KeyLayout.Columns[shape.Column];
            this.BringIntoView(new Rect(column.X * ColumnWidth, 0, column.Width * ColumnWidth, Bounds.Height));
            InvalidateVisual(); e.Handled = true;
        }

        protected override void OnKeyUp(KeyEventArgs e) {
            base.OnKeyUp(e);
            if (!auditionKeys.Remove(e.Key)) return;
            if (auditionKeys.Count == 0) RunAudition(audition.EndKeyboard);
            e.Handled = true;
        }

        protected override void OnLostFocus(FocusChangedEventArgs e) {
            StopAudition();
            base.OnLostFocus(e);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
            StopAudition();
            base.OnDetachedFromVisualTree(e);
        }

        void RunAudition(Action action) {
            try { action(); }
            catch (Exception error) {
                StopAudition();
                Log.Error(error, "Failed to audition keyboard pitch.");
            }
        }

        void StopPointerAudition() {
            RunAudition(audition.EndPointer);
            ReleasePointerCapture();
        }

        void ReleasePointerCapture() {
            var pointer = auditionPointer;
            auditionPointer = null;
            if (pointer?.Captured != this) return;
            releasingCapture = true;
            try { pointer.Capture(null); }
            finally { releasingCapture = false; }
        }

        public void StopAudition() {
            auditionKeys.Clear();
            try { audition.Cancel(); }
            catch (Exception error) { Log.Error(error, "Failed to stop keyboard pitch audition."); }
            ReleasePointerCapture();
        }

        IBrush Brush(string key) => this.TryFindResource(key, out var value) && value is IBrush brush ? brush : Brushes.Transparent;

        IBrush KeyBrush(KeyRole role, bool name = false) => Brush(role == KeyRole.Black
            ? name ? "KeyboardPreviewBlackNameBrush" : "KeyboardPreviewBlackBrush"
            : role + (name ? "KeyNameBrush" : "KeyBrush"));

        StreamGeometry Geometry(NotchedKeyShape shape) {
            if (geometries.TryGetValue(shape.Degree, out var cached)) return cached;
            if (geometries.Count > 2048) geometries.Clear();
            var geometry = new StreamGeometry();
            using (var path = geometry.Open()) {
                foreach (var rect in shape.Rectangles) {
                    path.BeginFigure(new Point(rect.X, rect.Y), true);
                    path.LineTo(new Point(rect.Right, rect.Y));
                    path.LineTo(new Point(rect.Right, rect.Bottom));
                    path.LineTo(new Point(rect.X, rect.Bottom));
                    path.EndFigure(true);
                }
            }
            geometries[shape.Degree] = geometry;
            return geometry;
        }

        // Remove shared rectangle edges before drawing key boundaries, including unequal notch widths.
        static IEnumerable<(Point Start, Point End)> Outline(NotchedKeyShape shape) {
            var edges = new Dictionary<(bool Vertical, double Position), List<(double Start, double End)>>();
            void Add(bool vertical, double position, double start, double end) {
                if (!edges.TryGetValue((vertical, position), out var intervals)) {
                    intervals = new List<(double, double)>(); edges[(vertical, position)] = intervals;
                }
                intervals.Add((start, end));
            }
            foreach (var rect in shape.Rectangles) {
                Add(false, rect.Y, rect.X, rect.Right); Add(false, rect.Bottom, rect.X, rect.Right);
                Add(true, rect.X, rect.Y, rect.Bottom); Add(true, rect.Right, rect.Y, rect.Bottom);
            }
            foreach (var edge in edges) {
                var ends = edge.Value.SelectMany(interval => new[] { interval.Start, interval.End }).Distinct().OrderBy(x => x).ToArray();
                for (int i = 0; i + 1 < ends.Length; i++) {
                    double middle = (ends[i] + ends[i + 1]) / 2;
                    if (edge.Value.Count(interval => middle > interval.Start && middle < interval.End) % 2 == 0) continue;
                    yield return edge.Key.Vertical
                        ? (new Point(edge.Key.Position, ends[i]), new Point(edge.Key.Position, ends[i + 1]))
                        : (new Point(ends[i], edge.Key.Position), new Point(ends[i + 1], edge.Key.Position));
                }
            }
        }

        public override void Render(DrawingContext context) {
            if (tuning == null || KeyLayout == null || Bounds.Height <= 0) return;
            double height = Bounds.Height;
            double frontTop = KeyLayout.IsTraditionalKeyboard
                ? KeyLayout.Keys.Where(key => cells[key.Degree].Role == KeyRole.Black).Max(key => key.Rectangles.Max(rect => rect.Bottom)) : 0;
            var visible = viewport.Width > 0 ? viewport : new Rect(Bounds.Size);
            var shapes = KeyLayout.Keys.Where(shape => shape.Rectangles.Any(rect =>
                rect.Right * ColumnWidth >= visible.Left && rect.X * ColumnWidth <= visible.Right)).ToArray();
            using (context.PushClip(new Rect(Bounds.Size))) {
                foreach (var shape in shapes) {
                    using (context.PushTransform(Matrix.CreateScale(ColumnWidth, height))) {
                        context.DrawGeometry(KeyBrush(cells[shape.Degree].Role), null, Geometry(shape));
                        if (SelectedDegree == shape.Degree) context.DrawGeometry(Brush("AccentBrush2Semi"), null, Geometry(shape));
                    }
                }
                var lines = new HashSet<(Point, Point)>();
                foreach (var shape in shapes) {
                    foreach (var edge in Outline(shape)) {
                        var a = new Point(edge.Start.X * ColumnWidth, edge.Start.Y * height);
                        var b = new Point(edge.End.X * ColumnWidth, edge.End.Y * height);
                        if (SelectedDegree == shape.Degree) context.DrawLine(new Pen(Brush("AccentBrush2"), 2), a, b);
                        else if (active.Count != ItemCount && active.Contains(shape.Degree)) {
                            context.DrawLine(new Pen(KeyBrush(cells[shape.Degree].Role, true), 1.3), a, b);
                        }
                        else {
                            bool boundary = a.Y == b.Y;
                            if (!boundary) {
                                double x = edge.Start.X, y = (edge.Start.Y + edge.End.Y) / 2;
                                var left = KeyLayout.HitTest(x - 1e-7, y);
                                var right = KeyLayout.HitTest(x + 1e-7, y);
                                boundary = left?.RunIndex != right?.RunIndex;
                            }
                            if (boundary && lines.Add((a, b))) context.DrawLine(new Pen(Brush("NeutralAccentBrushSemi"), 0.7), a, b);
                        }
                    }
                    var largest = shape.Rectangles.OrderByDescending(rect => rect.Width * rect.Height).First();
                    var area = new Rect(largest.X * ColumnWidth + 2, largest.Y * height + 1,
                        Math.Max(1, largest.Width * ColumnWidth - 4), Math.Max(1, largest.Height * height - 2));
                    if (KeyLayout.IsTraditionalKeyboard && cells[shape.Degree].Role == KeyRole.White) {
                        double left = shape.Rectangles.Min(rect => rect.X), right = shape.Rectangles.Max(rect => rect.Right);
                        area = new Rect(left * ColumnWidth + 2, frontTop * height + 1,
                            (right - left) * ColumnWidth - 4, (1 - frontTop) * height - 2);
                    }
                    var textBrush = KeyBrush(cells[shape.Degree].Role, true);
                    var cell = cells[shape.Degree];
                    // Without a MOS there are no nominals to name, so every white key shows its degree number.
                    string label = cell.IsAnchor && !whiteOnly ? PitchNotation.Format(tuning, shape.Degree,
                        PitchNotation.ResolvePrimary(tuning, null), false) : shape.Degree.ToString(XenStrings.InputCulture);
                    using (context.PushClip(area)) {
                        PitchLabelLayout.Draw(context, label, textBrush, Math.Min(11, area.Height * 0.7),
                            area.Left + area.Width / 2, area.Top, area.Height, centre: true);
                        if (active.Count != ItemCount && active.Contains(shape.Degree)) {
                            context.DrawEllipse(textBrush, null, new Point(area.Left + 3, area.Bottom - 4), 2, 2);
                        }
                    }
                }
                context.DrawRectangle(null, new Pen(Brush("NeutralAccentBrush"), 0.8), new Rect(Bounds.Size).Deflate(0.5));
            }
        }
    }
}
