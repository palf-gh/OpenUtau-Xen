using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpenUtau.App.Views;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.App {
    internal static class VibratoCurvesDialogTest {
        public static void Verify() {
            VerifyGraphs();
            VerifyPreview();
        }

        static (VibratoCurveGraph depth, VibratoCurveGraph rate, ComboBox preset, NumericUpDown length) Controls(Window dialog) {
            var graphs = dialog.GetVisualDescendants().OfType<VibratoCurveGraph>().ToArray();
            return (graphs.Single(graph => graph.Name == "VibratoDepthGraph"),
                graphs.Single(graph => graph.Name == "VibratoRateGraph"),
                dialog.GetVisualDescendants().OfType<ComboBox>().Single(box => box.Name == "VibratoPreset"),
                dialog.GetVisualDescendants().OfType<NumericUpDown>().Single(box => box.Name == "VibratoLength"));
        }

        static void VerifyGraphs() {
            var part = new UVoicePart();
            var note = new UNote {
                duration = 480,
                vibrato = new UVibrato {
                    length = 50, period = 180, depth = 40,
                    // Whole-note positions; the graphs show the vibrato section from 0.5 to 1.
                    depthCurve = new List<VibratoPoint> { new VibratoPoint(0.25f, 10), new VibratoPoint(0.75f, 60) },
                },
            };
            var dialog = new VibratoCurvesDialog(part, new[] { note });
            try {
                dialog.Show();
                Dispatcher.UIThread.RunJobs();
                var (depth, rate, preset, length) = Controls(dialog);
                Assert.True(depth.Bounds.Width > 400);
                Assert.True(rate.Bounds.Height > 100);
                Assert.Equal(240, depth.VibratoTicks);
                Assert.Equal(new[] { 0f, 0.5f }, depth.Points.Select(point => point.x));
                Assert.Equal(35, depth.Points[0].y, 4);
                Assert.Empty(rate.Points);
                Assert.Equal(1000f / 180, rate.Sample(0.5f), 4);
                Assert.Null(depth.BaseLine);
                depth.AddPoint(0.75f, 80);
                depth.MoveSelected(0.8f, 90);
                Assert.Equal(3, depth.Points.Count);
                Assert.Equal(90, depth.Sample(0.8f));
                depth.RemoveSelected();
                depth.ResetCurve();
                Assert.Empty(depth.Points);
                Assert.Equal(40, depth.Sample(0.8f));
                Assert.Equal(17, preset.ItemCount);
                Assert.Equal(0, preset.SelectedIndex);
                preset.SelectedIndex = 8;
                Dispatcher.UIThread.RunJobs();
                // [Extreme] Type 4 over the vibrato section: hold, fade from 60% to 90%.
                Assert.Equal(new[] { 0f, 0.6f, 0.9f }, depth.Points.Select(point => point.x).ToArray(),
                    (a, b) => Math.Abs(a - b) < 1e-5);
                Assert.Equal(300, depth.Sample(0.3f), 3);
                Assert.Equal(300f, depth.BaseLine);
                Assert.Equal(3.9f, rate.BaseLine);
                length.Value = 100;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(480, depth.VibratoTicks);
                Assert.Equal(0.6f, depth.Points[1].x, 5);
                string? directory = Environment.GetEnvironmentVariable("OPENUTAU_KEYBOARD_CAPTURES");
                if (!string.IsNullOrEmpty(directory)) {
                    Directory.CreateDirectory(directory);
                    using var frame = dialog.CaptureRenderedFrame();
                    frame?.Save(Path.Combine(directory, "vibrato-properties.png"));
                }
                // The OK result lays the section's points out over the note for the chosen length.
                length.Value = 50;
                var command = Assert.IsType<SetVibratoCurvesCommand>(GetCommand(dialog));
                command.Execute();
                Assert.Equal(8, note.vibrato.preset);
                Assert.Equal(new[] { 0.5f, 0.8f, 0.95f }, note.vibrato.depthCurve!.Select(point => point.x).ToArray(),
                    (a, b) => Math.Abs(a - b) < 1e-5);
            } finally {
                dialog.Close();
            }
        }

        static UCommand GetCommand(VibratoCurvesDialog dialog) => dialog.BuildCommand(false);

        static void VerifyPreview() {
            var previousProject = DocManager.Inst.Project;
            var project = Ustx.Create();
            var part = new UVoicePart { trackNo = 0, position = 0, Duration = 1920 };
            project.parts.Add(part);
            var note = project.CreateNote(60, 0, 480);
            note.vibrato.length = 50;
            part.notes.Add(note);
            project.timeAxis.BuildSegments(project);
            DocManager.Inst.ExecuteCmd(new LoadProjectNotification(project));
            try {
                // Cancel discards the previewed edits.
                var dialog = new VibratoCurvesDialog(part, new[] { note });
                dialog.Show();
                Dispatcher.UIThread.RunJobs();
                var (depth, _, preset, _) = Controls(dialog);
                preset.SelectedIndex = 5;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(5, note.vibrato.preset);
                Assert.Equal(300, note.vibrato.SampleDepth(0.75f), 3);
                depth.AddPoint(0.5f, 150);
                Assert.Equal(150, note.vibrato.SampleDepth(0.75f), 3);
                dialog.Close();
                Dispatcher.UIThread.RunJobs();
                Assert.Null(note.vibrato.preset);
                Assert.False(note.vibrato.HasCurves);
                // OK keeps them as one undoable edit.
                dialog = new VibratoCurvesDialog(part, new[] { note });
                dialog.Show();
                Dispatcher.UIThread.RunJobs();
                (depth, _, preset, _) = Controls(dialog);
                preset.SelectedIndex = 13;
                depth.AddPoint(0.5f, 20);
                dialog.GetVisualDescendants().OfType<Button>().Single(button => button.IsDefault)
                    .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(13, note.vibrato.preset);
                Assert.Equal(20, note.vibrato.SampleDepth(0.75f), 3);
                DocManager.Inst.Undo();
                Assert.Null(note.vibrato.preset);
                Assert.False(note.vibrato.HasCurves);
            } finally {
                DocManager.Inst.ExecuteCmd(new LoadProjectNotification(previousProject));
            }
        }
    }
}
