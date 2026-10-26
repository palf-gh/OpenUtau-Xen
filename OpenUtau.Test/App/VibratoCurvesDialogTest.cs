using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpenUtau.App.Views;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.App {
    internal static class VibratoCurvesDialogTest {
        public static void Verify() {
            var part = new UVoicePart();
            var note = new UNote { vibrato = new UVibrato { length = 65, period = 180, depth = 40 } };
            var dialog = new VibratoCurvesDialog(part, new[] { note });
            try {
                dialog.Show();
                Dispatcher.UIThread.RunJobs();
                var graphs = dialog.GetVisualDescendants().OfType<VibratoCurveGraph>().ToArray();
                Assert.Equal(2, graphs.Length);
                var depth = graphs.Single(graph => graph.Name == "VibratoDepthGraph");
                var rate = graphs.Single(graph => graph.Name == "VibratoRateGraph");
                Assert.True(depth.Bounds.Width > 400);
                Assert.True(rate.Bounds.Height > 100);
                Assert.Empty(depth.Points);
                depth.AddPoint(0.75f, 80);
                depth.MoveSelected(0.8f, 90);
                Assert.Equal(3, depth.Points.Count);
                Assert.Equal(90, depth.Sample(0.8f));
                Assert.False(note.vibrato.HasCurves);
                depth.RemoveSelected();
                Assert.Equal(2, depth.Points.Count);
                depth.ResetCurve();
                Assert.Empty(depth.Points);
                Assert.Equal(40, depth.Sample(0.8f));
                depth.AddPoint(0.25f, 15);
                depth.AddPoint(0.75f, 90);
                rate.AddPoint(0.25f, 4);
                rate.AddPoint(0.75f, 8);
                Dispatcher.UIThread.RunJobs();
                string directory = Environment.GetEnvironmentVariable("OPENUTAU_KEYBOARD_CAPTURES");
                if (!string.IsNullOrEmpty(directory)) {
                    Directory.CreateDirectory(directory);
                    using var frame = dialog.CaptureRenderedFrame();
                    frame?.Save(Path.Combine(directory, "vibrato-curves.png"));
                }
                var preset = dialog.GetVisualDescendants().OfType<ComboBox>().Single(box => box.Name == "VibratoPreset");
                var length = dialog.GetVisualDescendants().OfType<NumericUpDown>().Single(box => box.Name == "VibratoLength");
                Assert.Equal(17, preset.ItemCount);
                Assert.Equal(0, preset.SelectedIndex);
                preset.SelectedIndex = 8;
                Dispatcher.UIThread.RunJobs();
                // [Extreme] Type 4 holds its depth for 60% of a 65% vibrato, then fades by 90%.
                Assert.Equal(new[] { 0.35f, 0.35f + 0.65f * 0.6f, 0.35f + 0.65f * 0.9f },
                    depth.Points.Select(point => point.x).ToArray(), (a, b) => Math.Abs(a - b) < 1e-5);
                Assert.Equal(300, depth.Sample(0.5f), 3);
                length.Value = 100;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(0.6f, depth.Points[1].x, 5);
                rate.AddPoint(0.5f, 6);
                length.Value = 80;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(6, rate.Sample(0.5f), 4);
                if (!string.IsNullOrEmpty(directory)) {
                    using var frame = dialog.CaptureRenderedFrame();
                    frame?.Save(Path.Combine(directory, "vibrato-preset.png"));
                }
            } finally {
                dialog.Close();
            }
        }
    }
}
