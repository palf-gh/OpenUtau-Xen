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
            } finally {
                dialog.Close();
            }
        }
    }
}
