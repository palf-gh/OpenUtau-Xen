using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace OpenUtau.App.Controls {
    /// <summary>Uses Xen Accidentals for accidental runs (SMuFL and ♯ ♭ ♮); names keep the application font.</summary>
    internal static class PitchLabelLayout {
        /// <summary>
        /// Xen Accidentals (built by tools/build-xen-accidentals.py from Bravura Text) is sized and placed on the
        /// baseline for text, so accidentals use the same font size as the letters.
        /// </summary>
        internal static readonly FontFamily MusicFamily = new FontFamily(XenStrings.AssetRoot + "Assets/Fonts#Xen Accidentals");
        static readonly Typeface music = new Typeface(MusicFamily);
        static readonly Dictionary<(string, IBrush, double), TextLayout[]> cache = new();
        static TextLayout[] Runs(string text, IBrush brush, double fontSize) {
            var key = (text, brush, fontSize);
            if (!cache.TryGetValue(key, out var runs)) {
                if (cache.Count > 1024) cache.Clear();
                var layouts = new List<TextLayout>();
                int start = 0;
                while (start < text.Length) {
                    bool musical = IsMusic(text[start]);
                    int end = start + 1;
                    while (end < text.Length && IsMusic(text[end]) == musical) end++;
                    string run = text.Substring(start, end - start);
                    layouts.Add(musical ? new TextLayout(run, music, fontSize, brush, TextAlignment.Left, TextWrapping.NoWrap)
                        : TextLayoutCache.Get(run, brush, fontSize));
                    start = end;
                }
                runs = layouts.ToArray();
                cache[key] = runs;
            }
            return runs;
        }

        /// <summary>The size of a pitch name; its height follows the letters.</summary>
        public static Size MeasureLine(string text, IBrush brush, double fontSize) {
            var letters = TextLayoutCache.Get("C", brush, fontSize);
            return new Size(Runs(text, brush, fontSize).Sum(r => r.Width), letters.Height);
        }

        /// <summary>Draws a pitch name with every run on the letters' baseline, from the top left corner.</summary>
        public static void DrawLine(DrawingContext context, string text, IBrush brush, double fontSize, Point origin) {
            double baseline = origin.Y + TextLayoutCache.Get("C", brush, fontSize).Baseline;
            double x = origin.X;
            foreach (var run in Runs(text, brush, fontSize)) {
                using (context.PushTransform(Matrix.CreateTranslation(x, baseline - run.Baseline))) {
                    run.Draw(context, new Point());
                }
                x += run.Width;
            }
        }

        public static void Draw(DrawingContext context, string text, IBrush brush, double fontSize,
            double right, double top, double availableHeight, bool centre = false) {
            var runs = Runs(text, brush, fontSize);
            if (runs.Length == 0) return;
            double baseline = runs.Max(r => r.Baseline);
            double height = baseline + runs.Max(r => r.Height - r.Baseline);
            double x = Math.Max(4, right - runs.Sum(r => r.Width) * (centre ? 0.5 : 1));
            double y = top + (availableHeight - height) / 2;
            foreach (var run in runs) {
                using (context.PushTransform(Matrix.CreateTranslation(x, y + baseline - run.Baseline))) {
                    run.Draw(context, new Point());
                }
                x += run.Width;
            }
        }
        internal static bool IsMusic(char value) => value >= '\ue000' && value <= '\uf8ff' || value >= '\u266d' && value <= '\u266f';
    }
}
