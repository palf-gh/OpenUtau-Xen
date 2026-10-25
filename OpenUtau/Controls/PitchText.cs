using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace OpenUtau.App.Controls {
    /// <summary>
    /// A pitch name for lists, menus and buttons, drawn like the keyboard labels: accidentals (SMuFL and ♯ ♭ ♮) in
    /// Xen Accidentals on the letters' baseline, at the letters' size and height.
    /// </summary>
    public sealed class PitchText : Control {
        public static readonly StyledProperty<string?> PitchProperty =
            AvaloniaProperty.Register<PitchText, string?>(nameof(Pitch));
        public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<PitchText>();
        public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<PitchText>();

        static PitchText() {
            AffectsMeasure<PitchText>(PitchProperty, FontSizeProperty);
            AffectsRender<PitchText>(ForegroundProperty);
        }

        public string? Pitch {
            get => GetValue(PitchProperty);
            set => SetValue(PitchProperty, value);
        }

        public double FontSize {
            get => GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public IBrush? Foreground {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        IBrush Brush => Foreground ?? Brushes.Black;

        protected override Size MeasureOverride(Size availableSize) =>
            PitchLabelLayout.MeasureLine(Pitch ?? string.Empty, Brush, FontSize);

        public override void Render(DrawingContext context) =>
            PitchLabelLayout.DrawLine(context, Pitch ?? string.Empty, Brush, FontSize, new Point(0, 0));
    }
}
