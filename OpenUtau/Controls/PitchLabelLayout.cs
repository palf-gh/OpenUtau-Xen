using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace OpenUtau.App.Controls {
    /// <summary>Uses the music font only for SMuFL runs; names keep the application font.</summary>
    internal static class PitchLabelLayout {
        static readonly Typeface music = new Typeface(new FontFamily(XenStrings.AssetRoot + "Assets/Fonts#Bravura Text"));
        static readonly Dictionary<(string, IBrush, double), TextLayout[]> cache = new();
        public static void Draw(DrawingContext context, string text, IBrush brush, double fontSize,
            double right, double top, double availableHeight, bool centre = false) {
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
        static bool IsMusic(char value) => value >= '\ue000' && value <= '\uf8ff';
    }
}
