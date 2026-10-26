using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using OpenUtau.App.ViewModels;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.App {
    internal static class DrawingGestureTest {
        public static void Verify() {
            var manager = DocManager.Inst;
            manager.PostOnUIThread = action => Avalonia.Threading.Dispatcher.UIThread.Post(action);
            manager.Initialize(Thread.CurrentThread, TaskScheduler.Default);
            var previousProject = manager.Project;
            var project = Ustx.Create();
            if (project.tracks.Count == 0) {
                project.tracks.Add(new UTrack(project) { TrackNo = 0 });
            }
            manager.ExecuteCmd(new LoadProjectNotification(project));
            var vm = new MainWindowViewModel();
            var tracks = vm.TracksViewModel;
            tracks.TickWidth = 1;
            tracks.TrackHeight = 64;
            var control = new Canvas();
            var pointer = new Avalonia.Input.Pointer(1, PointerType.Mouse, true);
            object State(string name, params object?[] arguments) => Activator.CreateInstance(
                typeof(MainWindowViewModel).Assembly.GetType("OpenUtau.App.Views." + name)!, arguments)!;
            void Call(object state, string method, Point point) => state.GetType()
                .GetMethod(method, BindingFlags.Public | BindingFlags.Instance)!.Invoke(state, new object[] { pointer, point });
            try {
                // A press/update/release and pointer jitter must leave the model and undo history untouched.
                var start = new Point(101, 20);
                var click = State("PartDrawEditState", control, vm);
                Call(click, "Begin", start);
                Call(click, "Update", start);
                Call(click, "Update", start + new Vector(3, 20));
                Call(click, "End", start);
                Assert.Empty(project.parts);
                Assert.False(manager.HasOpenUndoGroup);
                Assert.False(manager.GetUndoState(out _));

                // The threshold creates at the pressed grid cell; later coordinates determine the extent.
                var drag = State("PartDrawEditState", control, vm);
                Call(drag, "Begin", start);
                Call(drag, "Update", start + new Vector(4, 0));
                Assert.Single(project.parts);
                tracks.PointToLineTick(start, out int left, out _);
                var final = new Point(1200, 20);
                tracks.PointToLineTick(final, out _, out int right);
                Call(drag, "Update", final);
                Call(drag, "End", final);
                var part = Assert.IsType<UVoicePart>(Assert.Single(project.parts));
                Assert.Equal(left, part.position);
                Assert.Equal(right, part.End);
                Assert.False(manager.HasOpenUndoGroup);
                manager.Undo();
                Assert.Empty(project.parts);
                manager.Redo();
                Assert.Single(project.parts);
                manager.Undo();

                // A backwards drag keeps the initial cell as its right anchor.
                var backwards = State("PartDrawEditState", control, vm);
                Call(backwards, "Begin", final);
                Call(backwards, "Update", start);
                Call(backwards, "End", start);
                part = Assert.IsType<UVoicePart>(Assert.Single(project.parts));
                Assert.Equal(left, part.position);
                Assert.Equal(right, part.End);
                manager.Undo();

                // Note clicks must not even begin a pitch gesture, undo group, tooltip or audition.
                var noteClick = State("NoteDrawEditState", control, null, null, false);
                Call(noteClick, "Begin", start);
                Call(noteClick, "Update", start + new Vector(3, 20));
                Call(noteClick, "End", start);
                Assert.False(manager.HasOpenUndoGroup);
                Assert.Empty(project.parts);
            } finally {
                pointer.Capture(null);
                manager.RemoveSubscriber(tracks);
                manager.RemoveSubscriber(vm);
                manager.ExecuteCmd(new LoadProjectNotification(previousProject));
            }
        }
    }
}
