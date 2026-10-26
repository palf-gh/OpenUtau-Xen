using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;

namespace OpenUtau.App.Views {
    /// <summary>
    /// Vibrato properties laid out like VOCALOID4's: length, type, and depth and rate graphs over the
    /// vibrato section. Edits are previewed on the notes inside one undo group until OK or Cancel.
    /// </summary>
    public sealed class VibratoCurvesDialog : Window {
        readonly UVoicePart part;
        readonly UNote[] notes;
        readonly NumericUpDown length;
        readonly ComboBox preset;
        readonly VibratoCurvePanel depth;
        readonly VibratoCurvePanel rate;
        readonly bool preview;
        bool finished;
        bool loading = true;

        public VibratoCurvesDialog(UVoicePart part, IEnumerable<UNote> selectedNotes) {
            this.part = part;
            notes = selectedNotes.ToArray();
            if (notes.Length == 0) {
                throw new ArgumentException("Select a note.", nameof(selectedNotes));
            }
            XenDialogStyles.Apply(this);
            Title = XenStrings.Get("vibrato.properties");
            Width = 640;
            MinWidth = 520;
            Height = 700;
            MinHeight = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var seed = notes[0].vibrato;
            length = Number(1, 100, seed.length > 0 ? (decimal)seed.length : 66);
            length.Name = "VibratoLength";
            length.Width = 110;
            preset = new ComboBox { Name = "VibratoPreset", MinWidth = 190 };
            preset.Items.Add(XenStrings.Get("vibrato.presetNone"));
            foreach (var item in VibratoPresets.All) {
                preset.Items.Add(item.Name);
            }
            preset.SelectedIndex = VibratoPresets.Get(seed.preset)?.Id ?? 0;
            depth = new VibratoCurvePanel("vibrato.depth", ToVibrato(seed.depthCurve, seed),
                seed.depth, 0, UVibrato.MaxDepthCents, 100);
            rate = new VibratoCurvePanel("vibrato.rate", ToVibrato(seed.rateCurve, seed),
                1000f / seed.period, UVibrato.MinRateHz, UVibrato.MaxRateHz, 10);
            preset.SelectionChanged += (_, _) => ChoosePreset();
            length.ValueChanged += (_, _) => {
                UpdateRuler();
                Preview();
            };
            depth.Graph.Changed += Preview;
            rate.Graph.Changed += Preview;
            UpdateRuler();
            UpdateBaseLines();

            var ok = new Button { Content = ThemeManager.GetString("button.ok"), MinWidth = 80, IsDefault = true };
            ok.Click += (_, _) => Finish(false);
            var cancel = new Button { Content = ThemeManager.GetString("button.cancel"), MinWidth = 80 };
            cancel.Click += (_, _) => Close();
            var remove = new Button { Content = XenStrings.Get("vibrato.removeCurves") };
            remove.Click += (_, _) => Finish(true);
            Content = new ScrollViewer {
                Content = new StackPanel {
                    Margin = new Thickness(20),
                    Spacing = 14,
                    Children = {
                        Row("vibrato.length", length),
                        Row("vibrato.preset", preset),
                        depth,
                        rate,
                        new DockPanel {
                            Children = {
                                remove,
                                new StackPanel {
                                    Orientation = Orientation.Horizontal,
                                    Spacing = 10,
                                    HorizontalAlignment = HorizontalAlignment.Right,
                                    Children = { ok, cancel },
                                },
                            },
                        },
                    },
                },
            };
            // Preview edits on the notes themselves. A dialog for notes outside the open project
            // (such as a test fixture) only reports its result.
            preview = DocManager.Inst.Project?.parts.Contains(part) == true
                && notes.All(note => part.notes.Contains(note));
            if (preview) {
                DocManager.Inst.StartUndoGroup("command.vibrato.edit");
            }
            loading = false;
        }

        static Grid Row(string label, Control control) {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("170,*") };
            grid.Children.Add(new TextBlock { Text = XenStrings.Get(label), VerticalAlignment = VerticalAlignment.Center });
            control.HorizontalAlignment = HorizontalAlignment.Left;
            Grid.SetColumn(control, 1);
            grid.Children.Add(control);
            return grid;
        }

        /// <summary>VOCALOID-style type of the curves; null keeps the note's own scales.</summary>
        int? SelectedPreset => preset.SelectedIndex > 0 ? preset.SelectedIndex : null;
        float Length => (float)(length.Value ?? 0);

        /// <summary>Curve points over the vibrato section (0 = vibrato start, 1 = note end).</summary>
        internal static List<VibratoPoint> ToVibrato(List<VibratoPoint>? points, UVibrato vibrato) {
            var result = new List<VibratoPoint>();
            if (points == null || points.Count == 0) {
                return result;
            }
            float span = vibrato.length / 100f;
            if (span <= 0) {
                return points.Select(point => point.Clone()).ToList();
            }
            float start = 1 - span;
            foreach (var point in points.Where(point => point.x >= start)) {
                result.Add(new VibratoPoint((point.x - start) / span, point.y));
            }
            if (result.Count == 0 || result[0].x > 0) {
                result.Insert(0, new VibratoPoint(0, VibratoCurve.Sample(points, start, 0)));
            }
            return result;
        }

        /// <summary>Whole-note curve points for a vibrato of <paramref name="length"/> percent.</summary>
        internal static List<VibratoPoint>? ToNote(IEnumerable<VibratoPoint> points, float length) {
            float span = length / 100f;
            var result = points.Select(point => new VibratoPoint(1 - span + point.x * span, point.y)).ToList();
            return result.Count == 0 ? null : result;
        }

        public SetVibratoCurvesCommand BuildCommand(bool remove) => new SetVibratoCurvesCommand(part, notes,
            remove ? null : ToNote(rate.Graph.Points, Length),
            remove ? null : ToNote(depth.Graph.Points, Length),
            remove ? null : Length,
            remove ? null : SelectedPreset);

        void ChoosePreset() {
            var selected = VibratoPresets.Get(SelectedPreset);
            if (selected != null) {
                // A full-length layout is the preset's shape over the vibrato section.
                var (rateCurve, depthCurve) = selected.Curves(100);
                loading = true;
                rate.Graph.SetPoints(rateCurve);
                depth.Graph.SetPoints(depthCurve);
                loading = false;
            }
            UpdateBaseLines();
            Preview();
        }

        void UpdateBaseLines() {
            var selected = VibratoPresets.Get(SelectedPreset);
            depth.Graph.BaseLine = selected?.BaseDepth;
            rate.Graph.BaseLine = selected?.BaseRate;
        }

        void UpdateRuler() {
            double ticks = notes[0].duration * Length / 100.0;
            depth.Graph.VibratoTicks = ticks;
            rate.Graph.VibratoTicks = ticks;
        }

        void Preview() {
            if (!loading && preview && !finished) {
                DocManager.Inst.ExecuteCmd(BuildCommand(false));
            }
        }

        void Finish(bool remove) {
            if (finished) {
                return;
            }
            finished = true;
            if (preview) {
                if (remove) {
                    DocManager.Inst.ExecuteCmd(BuildCommand(true));
                }
                DocManager.Inst.EndUndoGroup();
            }
            Close();
        }

        protected override void OnClosed(EventArgs e) {
            // Cancel, Escape and the title bar discard the previewed edits.
            if (!finished) {
                finished = true;
                if (preview) {
                    DocManager.Inst.RollBackUndoGroup();
                    DocManager.Inst.EndUndoGroup();
                }
            }
            base.OnClosed(e);
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                Close();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        internal static NumericUpDown Number(decimal minimum, decimal maximum, decimal value) => new NumericUpDown {
            Minimum = minimum,
            Maximum = maximum,
            Value = value,
            Increment = 0.1m,
            FormatString = "0.##",
            NumberFormat = XenStrings.InputCulture.NumberFormat,
            ParsingNumberStyle = NumberStyles.Float,
        };
    }

    internal sealed class VibratoCurvePanel : StackPanel {
        public readonly VibratoCurveGraph Graph;
        readonly NumericUpDown position;
        readonly NumericUpDown value;
        bool updating;

        public VibratoCurvePanel(string label, IEnumerable<VibratoPoint>? points,
            float fallback, float minimum, float limit, float initialMaximum) {
            Spacing = 7;
            Graph = new VibratoCurveGraph(points, fallback, minimum, limit, initialMaximum);
            Graph.Name = label == "vibrato.depth" ? "VibratoDepthGraph" : "VibratoRateGraph";
            position = VibratoCurvesDialog.Number(0, 100, 0);
            position.Width = 100;
            value = VibratoCurvesDialog.Number((decimal)minimum, (decimal)limit, (decimal)fallback);
            value.Width = 110;
            var add = new Button { Content = XenStrings.Get("vibrato.addPoint") };
            add.Click += (_, _) => Graph.AddPoint(0.5f, Graph.Sample(0.5f));
            var remove = new Button { Content = XenStrings.Get("vibrato.removePoint") };
            remove.Click += (_, _) => Graph.RemoveSelected();
            var reset = new Button { Content = XenStrings.Get("vibrato.resetCurve") };
            reset.Click += (_, _) => Graph.ResetCurve();
            Graph.Changed += Update;
            position.ValueChanged += (_, _) => UpdatePoint();
            value.ValueChanged += (_, _) => UpdatePoint();
            var header = new DockPanel();
            header.Children.Add(new TextBlock {
                Text = XenStrings.Get(label),
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(0, 0, 10, 0),
            });
            header.Children.Add(new Rectangle {
                Height = 1,
                Fill = ThemeManager.NeutralAccentBrush,
                VerticalAlignment = VerticalAlignment.Center,
            });
            Children.Add(header);
            Children.Add(Graph);
            Children.Add(new WrapPanel {
                Orientation = Orientation.Horizontal,
                Children = {
                    new TextBlock {
                        Text = XenStrings.Get("vibrato.position"),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 10, 0),
                    }, position, Spaced(value), Spaced(add), Spaced(remove), Spaced(reset),
                },
            });
            Update();
        }

        static Control Spaced(Control control) {
            control.Margin = new Thickness(10, 0, 0, 0);
            return control;
        }

        void Update() {
            updating = true;
            var selected = Graph.Selected;
            position.IsEnabled = value.IsEnabled = selected != null;
            if (selected != null) {
                position.Value = (decimal)(selected.x * 100);
                value.Value = (decimal)selected.y;
            }
            updating = false;
        }

        void UpdatePoint() {
            if (!updating && Graph.Selected != null) {
                Graph.MoveSelected((float)(position.Value ?? 0) / 100, (float)(value.Value ?? 0));
            }
        }
    }

    /// <summary>
    /// One vibrato lane over the vibrato section: a tick ruler on top, values on the left and the
    /// curve filled down to the minimum, as VOCALOID4's property graphs show it.
    /// </summary>
    public sealed class VibratoCurveGraph : Control {
        const double Pad = 6;
        const double RulerHeight = 18;
        const double AxisWidth = 46;
        static readonly int[] RulerSteps = { 15, 30, 60, 120, 240, 480, 960, 1920 };
        readonly float fallback;
        readonly float minimum;
        readonly float limit;
        readonly float initialMaximum;
        double vibratoTicks;
        float? baseLine;
        bool dragging;
        public readonly List<VibratoPoint> Points;
        public VibratoPoint? Selected { get; private set; }
        public event Action? Changed;
        public float Maximum => Math.Min(limit, Math.Max(initialMaximum,
            Math.Max(Math.Max(fallback, baseLine ?? 0), Points.Count == 0 ? 0 : Points.Max(point => point.y)) * 1.15f));
        /// <summary>Ticks in the vibrato section, for the ruler.</summary>
        public double VibratoTicks {
            get => vibratoTicks;
            set { vibratoTicks = value; InvalidateVisual(); }
        }
        /// <summary>The selected type's value 64, drawn as a reference line.</summary>
        public float? BaseLine {
            get => baseLine;
            set { baseLine = value; InvalidateVisual(); }
        }

        public VibratoCurveGraph(IEnumerable<VibratoPoint>? points, float fallback,
            float minimum, float limit, float initialMaximum) {
            this.fallback = fallback;
            this.minimum = minimum;
            this.limit = limit;
            this.initialMaximum = initialMaximum;
            Points = points?.Select(point => new VibratoPoint { x = point.x, y = point.y }).ToList()
                ?? new List<VibratoPoint>();
            Height = 170;
            Focusable = true;
            Cursor = new Cursor(StandardCursorType.Cross);
        }

        Rect Plot => new Rect(AxisWidth, RulerHeight + Pad,
            Math.Max(1, Bounds.Width - AxisWidth - Pad), Math.Max(1, Bounds.Height - RulerHeight - 2 * Pad));

        public float Sample(float x) {
            if (Points.Count == 0) return fallback;
            if (x <= Points[0].x) return Points[0].y;
            for (int i = 1; i < Points.Count; i++) {
                if (x <= Points[i].x) {
                    var a = Points[i - 1];
                    var b = Points[i];
                    return a.y + (b.y - a.y) * (x - a.x) / Math.Max(0.000001f, b.x - a.x);
                }
            }
            return Points[^1].y;
        }

        Point ToPoint(float x, float y) {
            var plot = Plot;
            return new Point(plot.X + x * plot.Width, plot.Y + (Maximum - y) / (Maximum - minimum) * plot.Height);
        }

        (float x, float y) FromPoint(Point p) {
            var plot = Plot;
            return (Math.Clamp((float)((p.X - plot.X) / plot.Width), 0, 1),
                Math.Clamp(Maximum - (float)((p.Y - plot.Y) / plot.Height) * (Maximum - minimum), minimum, limit));
        }

        static FormattedText Text(string text, IBrush brush) => new FormattedText(text,
            CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface.Default, 10, brush);

        public override void Render(DrawingContext context) {
            base.Render(context);
            var plot = Plot;
            context.DrawRectangle(ThemeManager.BackgroundBrush, ThemeManager.NeutralAccentPen, plot);
            // Tick ruler over the vibrato section.
            context.DrawRectangle(ThemeManager.NeutralAccentBrushSemi, null, new Rect(plot.X, 0, plot.Width, RulerHeight));
            if (vibratoTicks > 0) {
                int step = RulerSteps.FirstOrDefault(s => vibratoTicks / s <= 6, RulerSteps[^1]);
                for (int tick = 0; tick <= vibratoTicks; tick += step) {
                    double x = plot.X + tick / vibratoTicks * plot.Width;
                    context.DrawLine(ThemeManager.NeutralAccentPenSemi, new Point(x, plot.Y), new Point(x, plot.Bottom));
                    context.DrawLine(ThemeManager.NeutralAccentPen, new Point(x, RulerHeight - 5), new Point(x, RulerHeight));
                    if (tick == 0 || x + 24 < plot.Right) {
                        context.DrawText(Text(tick.ToString(CultureInfo.InvariantCulture), ThemeManager.ForegroundBrush),
                            new Point(x + 3, 2));
                    }
                }
            }
            // Value axis.
            foreach (float y in new[] { minimum, (minimum + Maximum) / 2, Maximum }) {
                var p = ToPoint(0, y);
                context.DrawLine(ThemeManager.NeutralAccentPenSemi, p, new Point(plot.Right, p.Y));
                var label = Text(y.ToString(Maximum < 20 ? "0.#" : "0", XenStrings.InputCulture), ThemeManager.ForegroundBrush);
                context.DrawText(label, new Point(plot.X - label.Width - 5,
                    Math.Clamp(p.Y - label.Height / 2, plot.Y - 2, plot.Bottom - label.Height + 2)));
            }
            // The curve, filled down to the minimum.
            var top = new List<Point> { ToPoint(0, Sample(0)) };
            top.AddRange(Points.Select(point => ToPoint(point.x, point.y)));
            top.Add(ToPoint(1, Sample(1)));
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open()) {
                ctx.BeginFigure(new Point(plot.X, plot.Bottom), true);
                foreach (var point in top) {
                    ctx.LineTo(point);
                }
                ctx.LineTo(new Point(plot.Right, plot.Bottom));
                ctx.EndFigure(true);
            }
            context.DrawGeometry(ThemeManager.AccentBrush1Semi, null, geometry);
            for (int i = 1; i < top.Count; i++) {
                context.DrawLine(ThemeManager.AccentPen1Thickness2, top[i - 1], top[i]);
            }
            if (baseLine is float value && value <= Maximum) {
                var p = ToPoint(0, value);
                var pen = new Pen(ThemeManager.ForegroundBrush, 1, new DashStyle(new double[] { 4, 3 }, 0));
                context.DrawLine(pen, p, new Point(plot.Right, p.Y));
                var label = Text("64", ThemeManager.ForegroundBrush);
                context.DrawText(label, new Point(plot.Right - label.Width - 3, p.Y - label.Height - 1));
            }
            foreach (var point in Points) {
                context.DrawEllipse(ReferenceEquals(point, Selected) ? ThemeManager.AccentBrush2 : ThemeManager.AccentBrush1,
                    null, ToPoint(point.x, point.y), 4, 4);
            }
        }

        public void AddPoint(float x, float y) {
            if (Points.Count == 0) {
                Points.Add(new VibratoPoint { x = 0, y = fallback });
                Points.Add(new VibratoPoint { x = 1, y = fallback });
            }
            Selected = Points.FirstOrDefault(point => Math.Abs(point.x - x) < 0.00001f);
            if (Selected == null) {
                Selected = new VibratoPoint { x = x, y = y };
                Points.Add(Selected);
            }
            MoveSelected(x, y);
        }

        public void MoveSelected(float x, float y) {
            if (Selected == null) return;
            x = Math.Clamp(x, 0, 1);
            y = Math.Clamp(y, minimum, limit);
            Points.RemoveAll(point => !ReferenceEquals(point, Selected) && Math.Abs(point.x - x) < 0.00001f);
            Selected.x = x;
            Selected.y = y;
            Points.Sort((a, b) => a.x.CompareTo(b.x));
            InvalidateVisual();
            Changed?.Invoke();
        }

        public void RemoveSelected() {
            if (Selected != null) Points.Remove(Selected);
            Selected = null;
            InvalidateVisual();
            Changed?.Invoke();
        }

        public void SetPoints(IEnumerable<VibratoPoint> points) {
            Points.Clear();
            Points.AddRange(points.Select(point => new VibratoPoint { x = point.x, y = point.y }));
            Selected = null;
            InvalidateVisual();
            Changed?.Invoke();
        }

        public void ResetCurve() {
            Points.Clear();
            Selected = null;
            InvalidateVisual();
            Changed?.Invoke();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e) {
            base.OnPointerPressed(e);
            Focus();
            var p = e.GetPosition(this);
            double DistanceSquared(VibratoPoint point) {
                var location = ToPoint(point.x, point.y);
                double dx = location.X - p.X;
                double dy = location.Y - p.Y;
                return dx * dx + dy * dy;
            }
            Selected = Points.OrderBy(DistanceSquared)
                .FirstOrDefault(point => DistanceSquared(point) <= 100);
            if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed) {
                RemoveSelected();
            } else if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) {
                if (Selected == null) {
                    var (x, y) = FromPoint(p);
                    AddPoint(x, y);
                }
                dragging = true;
                e.Pointer.Capture(this);
                Changed?.Invoke();
                InvalidateVisual();
            }
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e) {
            base.OnPointerMoved(e);
            if (dragging) {
                var (x, y) = FromPoint(e.GetPosition(this));
                MoveSelected(x, y);
                e.Handled = true;
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e) {
            base.OnPointerReleased(e);
            dragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            if (e.Key == Key.Delete || e.Key == Key.Back) {
                RemoveSelected();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }
    }
}
