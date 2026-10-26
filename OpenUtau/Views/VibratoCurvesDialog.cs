using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;

namespace OpenUtau.App.Views {
    public sealed class VibratoCurvesDialog : Window {
        readonly UVoicePart part;
        readonly UNote[] notes;
        readonly NumericUpDown length;
        readonly VibratoCurvePanel depth;
        readonly VibratoCurvePanel rate;

        public VibratoCurvesDialog(UVoicePart part, IEnumerable<UNote> selectedNotes) {
            this.part = part;
            notes = selectedNotes.ToArray();
            if (notes.Length == 0) {
                throw new ArgumentException("Select a note.", nameof(selectedNotes));
            }
            XenDialogStyles.Apply(this);
            Title = XenStrings.Get("vibrato.curves");
            Width = 670;
            MinWidth = 540;
            Height = 690;
            MinHeight = 590;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var seed = notes[0].vibrato;
            length = Number(0, 100, seed.length > 0 ? (decimal)seed.length : 66);
            length.Name = "VibratoLength";
            length.Width = 100;
            depth = new VibratoCurvePanel("vibrato.depth", seed.depthCurve,
                seed.depth, 0, UVibrato.MaxDepthCents, 100);
            rate = new VibratoCurvePanel("vibrato.rate", seed.rateCurve,
                1000f / seed.period, UVibrato.MinRateHz, UVibrato.MaxRateHz, 10);
            length.ValueChanged += (_, _) => UpdateStart();
            UpdateStart();
            var apply = new Button { Content = XenStrings.Get("vibrato.applySelected") };
            apply.Click += (_, _) => Apply(false);
            var remove = new Button { Content = XenStrings.Get("vibrato.removeCurves") };
            remove.Click += (_, _) => Apply(true);
            var close = new Button { Content = XenStrings.Get("action.close") };
            close.Click += (_, _) => Close();
            Content = new ScrollViewer {
                Content = new StackPanel {
                    Margin = new Thickness(20),
                    Spacing = 16,
                    Children = {
                        new StackPanel {
                            Orientation = Orientation.Horizontal,
                            Spacing = 12,
                            Children = {
                                new TextBlock {
                                    Text = XenStrings.Get("vibrato.length"),
                                    VerticalAlignment = VerticalAlignment.Center,
                                },
                                length,
                            },
                        },
                        depth,
                        rate,
                        new StackPanel {
                            Orientation = Orientation.Horizontal,
                            Spacing = 10,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Children = { remove, close, apply },
                        },
                    },
                },
            };
        }

        void UpdateStart() {
            float start = 1 - (float)(length.Value ?? 0) / 100;
            depth.Graph.VibratoStart = start;
            rate.Graph.VibratoStart = start;
        }

        void Apply(bool remove) {
            if (!DocManager.Inst.Project.parts.Contains(part)
                || notes.Any(note => !part.notes.Contains(note))) {
                Close();
                return;
            }
            DocManager.Inst.StartUndoGroup("command.vibrato.edit");
            DocManager.Inst.ExecuteCmd(new SetVibratoCurvesCommand(part, notes,
                remove ? null : rate.Graph.Points,
                remove ? null : depth.Graph.Points,
                remove ? null : (float?)(length.Value ?? 0)));
            DocManager.Inst.EndUndoGroup();
            Close();
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
        readonly TextBlock maximum;
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
            maximum = new TextBlock { HorizontalAlignment = HorizontalAlignment.Right };
            var add = new Button { Content = XenStrings.Get("vibrato.addPoint") };
            add.Click += (_, _) => Graph.AddPoint(0.5f, Graph.Sample(0.5f));
            var remove = new Button { Content = XenStrings.Get("vibrato.removePoint") };
            remove.Click += (_, _) => Graph.RemoveSelected();
            var reset = new Button { Content = XenStrings.Get("vibrato.resetCurve") };
            reset.Click += (_, _) => Graph.ResetCurve();
            Graph.Changed += Update;
            position.ValueChanged += (_, _) => UpdatePoint();
            value.ValueChanged += (_, _) => UpdatePoint();
            Children.Add(new TextBlock { Text = XenStrings.Get(label), FontWeight = FontWeight.SemiBold });
            Children.Add(maximum);
            Children.Add(Graph);
            Children.Add(new StackPanel {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Children = {
                    new TextBlock {
                        Text = XenStrings.Get("vibrato.position"),
                        VerticalAlignment = VerticalAlignment.Center,
                    }, position, value,
                },
            });
            Children.Add(new StackPanel {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Children = { add, remove, reset },
            });
            Update();
        }

        void Update() {
            updating = true;
            var selected = Graph.Selected;
            position.IsEnabled = value.IsEnabled = selected != null;
            if (selected != null) {
                position.Value = (decimal)(selected.x * 100);
                value.Value = (decimal)selected.y;
            }
            maximum.Text = Graph.Maximum.ToString("0.##", XenStrings.InputCulture);
            updating = false;
        }

        void UpdatePoint() {
            if (!updating && Graph.Selected != null) {
                Graph.MoveSelected((float)(position.Value ?? 0) / 100, (float)(value.Value ?? 0));
            }
        }
    }

    public sealed class VibratoCurveGraph : Control {
        const double Padding = 9;
        readonly float fallback;
        readonly float minimum;
        readonly float limit;
        readonly float initialMaximum;
        float vibratoStart;
        bool dragging;
        public readonly List<VibratoPoint> Points;
        public VibratoPoint? Selected { get; private set; }
        public event Action? Changed;
        public float Maximum => Math.Min(limit,
            Math.Max(initialMaximum, Math.Max(fallback, Points.Count == 0 ? 0 : Points.Max(point => point.y)) * 1.15f));
        public float VibratoStart {
            get => vibratoStart;
            set { vibratoStart = value; InvalidateVisual(); }
        }

        public VibratoCurveGraph(IEnumerable<VibratoPoint>? points, float fallback,
            float minimum, float limit, float initialMaximum) {
            this.fallback = fallback;
            this.minimum = minimum;
            this.limit = limit;
            this.initialMaximum = initialMaximum;
            Points = points?.Select(point => new VibratoPoint { x = point.x, y = point.y }).ToList()
                ?? new List<VibratoPoint>();
            Height = 145;
            Focusable = true;
            Cursor = new Cursor(StandardCursorType.Cross);
        }

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

        Point ToPoint(float x, float y) => new Point(
            Padding + x * Math.Max(1, Bounds.Width - 2 * Padding),
            Padding + (Maximum - y) / (Maximum - minimum) * Math.Max(1, Bounds.Height - 2 * Padding));

        (float x, float y) FromPoint(Point p) => (
            Math.Clamp((float)((p.X - Padding) / Math.Max(1, Bounds.Width - 2 * Padding)), 0, 1),
            Math.Clamp(Maximum - (float)((p.Y - Padding) / Math.Max(1, Bounds.Height - 2 * Padding))
                * (Maximum - minimum), minimum, limit));

        public override void Render(DrawingContext context) {
            base.Render(context);
            var rect = new Rect(Bounds.Size).Deflate(1);
            context.DrawRectangle(ThemeManager.BackgroundBrush, ThemeManager.NeutralAccentPen, rect);
            var start = ToPoint(vibratoStart, minimum).X;
            context.DrawRectangle(ThemeManager.NeutralAccentBrushSemi, null,
                new Rect(Padding, Padding, Math.Max(0, start - Padding), Math.Max(0, Bounds.Height - 2 * Padding)));
            for (int i = 1; i < 4; i++) {
                float x = i / 4f;
                context.DrawLine(ThemeManager.NeutralAccentPenSemi,
                    ToPoint(x, minimum), ToPoint(x, Maximum));
            }
            Point previous = ToPoint(0, Sample(0));
            foreach (var point in Points) {
                var next = ToPoint(point.x, point.y);
                context.DrawLine(ThemeManager.AccentPen1Thickness2, previous, next);
                previous = next;
            }
            context.DrawLine(ThemeManager.AccentPen1Thickness2, previous, ToPoint(1, Sample(1)));
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
