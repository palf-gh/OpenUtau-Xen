using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using OpenUtau.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace OpenUtau.App.Controls {
    /// <summary>
    /// Draws the planned pitch-follow camera-centre trajectory over the piano roll (from OpenUtau Lunai).
    /// </summary>
    class PitchFollowPathCanvas : Control {
        public static readonly StyledProperty<bool> ShowPathProperty =
            AvaloniaProperty.Register<PitchFollowPathCanvas, bool>(nameof(ShowPath));
        public static readonly DirectProperty<PitchFollowPathCanvas, double> TickWidthProperty =
            AvaloniaProperty.RegisterDirect<PitchFollowPathCanvas, double>(
                nameof(TickWidth),
                o => o.TickWidth,
                (o, v) => o.TickWidth = v);
        public static readonly DirectProperty<PitchFollowPathCanvas, double> TickOffsetProperty =
            AvaloniaProperty.RegisterDirect<PitchFollowPathCanvas, double>(
                nameof(TickOffset),
                o => o.TickOffset,
                (o, v) => o.TickOffset = v);
        public static readonly DirectProperty<PitchFollowPathCanvas, double> TrackHeightProperty =
            AvaloniaProperty.RegisterDirect<PitchFollowPathCanvas, double>(
                nameof(TrackHeight),
                o => o.TrackHeight,
                (o, v) => o.TrackHeight = v);
        public static readonly DirectProperty<PitchFollowPathCanvas, double> TrackOffsetProperty =
            AvaloniaProperty.RegisterDirect<PitchFollowPathCanvas, double>(
                nameof(TrackOffset),
                o => o.TrackOffset,
                (o, v) => o.TrackOffset = v);
        public static readonly DirectProperty<PitchFollowPathCanvas, double> ViewportTracksProperty =
            AvaloniaProperty.RegisterDirect<PitchFollowPathCanvas, double>(
                nameof(ViewportTracks),
                o => o.ViewportTracks,
                (o, v) => o.ViewportTracks = v);
        public static readonly DirectProperty<PitchFollowPathCanvas, NotesViewModel?> NotesViewModelProperty =
            AvaloniaProperty.RegisterDirect<PitchFollowPathCanvas, NotesViewModel?>(
                nameof(NotesViewModel),
                o => o.NotesViewModel,
                (o, v) => o.NotesViewModel = v);

        public bool ShowPath {
            get => GetValue(ShowPathProperty);
            set => SetValue(ShowPathProperty, value);
        }
        public double TickWidth {
            get => tickWidth;
            private set => SetAndRaise(TickWidthProperty, ref tickWidth, value);
        }
        public double TickOffset {
            get => tickOffset;
            private set => SetAndRaise(TickOffsetProperty, ref tickOffset, value);
        }
        public double TrackHeight {
            get => trackHeight;
            private set => SetAndRaise(TrackHeightProperty, ref trackHeight, value);
        }
        public double TrackOffset {
            get => trackOffset;
            private set => SetAndRaise(TrackOffsetProperty, ref trackOffset, value);
        }
        public double ViewportTracks {
            get => viewportTracks;
            private set => SetAndRaise(ViewportTracksProperty, ref viewportTracks, value);
        }
        public NotesViewModel? NotesViewModel {
            get => notesViewModel;
            set => SetAndRaise(NotesViewModelProperty, ref notesViewModel, value);
        }

        double tickWidth;
        double tickOffset;
        double trackHeight;
        double trackOffset;
        double viewportTracks;
        NotesViewModel? notesViewModel;
        IDisposable? previewSubscription;

        static PitchFollowPathCanvas() {
            AffectsRender<PitchFollowPathCanvas>(
                ShowPathProperty,
                TickWidthProperty,
                TickOffsetProperty,
                TrackHeightProperty,
                TrackOffsetProperty,
                ViewportTracksProperty,
                NotesViewModelProperty);
        }

        public PitchFollowPathCanvas() {
            ClipToBounds = true;
            IsHitTestVisible = false;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
            base.OnAttachedToVisualTree(e);
            previewSubscription = MessageBus.Current.Listen<PitchFollowPathPreviewChangedEvent>()
                .Subscribe(_ => InvalidateVisual());
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
            base.OnDetachedFromVisualTree(e);
            previewSubscription?.Dispose();
            previewSubscription = null;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
            base.OnPropertyChanged(change);
            if (change.Property == ShowPathProperty && ShowPath) {
                InvalidateVisual();
            }
        }

        public override void Render(DrawingContext context) {
            if (!ShowPath || notesViewModel == null) {
                return;
            }

            var samples = notesViewModel.PitchFollowPathSamples;
            if (samples.Count < 2 || TickWidth <= 0 || TrackHeight <= 0) {
                return;
            }

            double verticalPosition = PitchFollowScrollMath.DefaultVerticalPosition;
            var pathPen = ThemeManager.AccentPen2Thickness2;

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open()) {
                bool started = false;
                foreach (var sample in samples) {
                    var point = ToCanvasPoint(sample.Tick, sample.TrackOffset, verticalPosition);
                    if (!started) {
                        ctx.BeginFigure(point, false);
                        started = true;
                    } else {
                        ctx.LineTo(point);
                    }
                }
            }
            context.DrawGeometry(null, pathPen, geometry);
        }

        Point ToCanvasPoint(double localTick, double pathTrackOffset, double verticalPosition) {
            double x = (localTick - TickOffset) * TickWidth;
            double y = (pathTrackOffset + ViewportTracks * verticalPosition - TrackOffset) * TrackHeight;
            return new Point(x, y);
        }
    }
}
