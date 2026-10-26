using System;
using System.Linq;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpenUtau.App.Controls;
using OpenUtau.App.ViewModels;
using OpenUtau.Core.Util;
using Xunit;

namespace OpenUtau.App {
    internal static class RhythmMenuTest {
        public static void Verify() {
            var original = Preferences.Default;
            try {
                Preferences.Default = new Preferences.SerializablePreferences();
                VerifyCore();
            } finally {
                Preferences.Default = original;
                Preferences.Save();
            }
        }

        private static void VerifyCore() {
            var backgroundNotes = System.Threading.Tasks.Task.Run(() => new NotesViewModel()).GetAwaiter().GetResult();
            Dispatcher.UIThread.RunJobs();
            Assert.NotEmpty(backgroundNotes.SnapDivs);
            var model = new PianoRollViewModel { NotesViewModel = backgroundNotes };
            var vm = model.NotesViewModel;
            MenuItemViewModel[] Choices() => vm.SnapDivs.OfType<MenuItemViewModel>().ToArray();
            MenuItemViewModel[] Tuplets() => Choices().Where(item => item.Command == vm.SetSnapModifierCommand).ToArray();
            vm.SetSnapUnitCommand.Execute(1004).Subscribe();
            Assert.Contains("1/4", vm.SnapDivText);
            Assert.Contains(XenStrings.Get("snap.dotted"), vm.SnapDivText);
            Assert.Equal(720, vm.SnapUnit);
            Assert.Single(Choices().Where(item => item.IsChecked && item.Command == vm.SetSnapUnitCommand));
            vm.SetSnapModifierCommand.Execute(3).Subscribe();
            Assert.Equal(480, vm.SnapUnit);
            Assert.Contains("3:2", vm.SnapDivText);
            Assert.Single(Tuplets().Where(item => item.IsChecked));
            vm.SetSnapModifierCommand.Execute(3).Subscribe();
            Assert.Equal(720, vm.SnapUnit);
            Assert.Empty(Tuplets().Where(item => item.IsChecked));
            vm.SetSnapModifierCommand.Execute(5).Subscribe();
            Assert.Equal(576, vm.SnapUnit);
            vm.SetSnapModifierCommand.Execute(7).Subscribe();
            Assert.Single(Tuplets().Where(item => item.IsChecked));
            Assert.Equal(7, Tuplets().Single(item => item.IsChecked).CommandParameter);
            vm.SetSnapUnitCommand.Execute(8).Subscribe();
            Assert.DoesNotContain(XenStrings.Get("snap.dotted"), vm.SnapDivText);
            Assert.Single(Choices().Where(item => item.IsChecked && item.Command == vm.SetSnapUnitCommand));
            Assert.Equal(new[] { 3, 5, 7 }, Tuplets().Select(item => (int)item.CommandParameter!).ToArray());
            Assert.All(Choices(), item => Assert.Null(item.Items));
            Assert.Equal(3, vm.SnapDivs.OfType<Separator>().Count());
            Assert.DoesNotContain(Choices(), item => item.Header == "1/6" || item.Header == "1/12");
            Assert.All(Choices().Where(item => item.IsChecked), item =>
                Assert.Equal("●", Assert.IsType<TextBlock>(item.Icon).Text));

            var roll = new PianoRoll(model);
            var window = new Window { Width = 1100, Height = 800, Content = roll };
            try {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                var menu = roll.FindControl<ContextMenu>("SnapDivMenu")!;
                menu.PlacementTarget = roll.GetVisualDescendants().OfType<Button>().Single(button => button.ContextMenu == menu);
                menu.Open();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(3, menu.GetVisualDescendants().OfType<Separator>().Count());
                var selected = menu.GetVisualDescendants().OfType<MenuItem>()
                    .Where(item => item.DataContext is MenuItemViewModel choice && choice.IsChecked).ToArray();
                Assert.Equal(2, selected.Length);
                Assert.All(selected, item => {
                    var marker = Assert.IsType<TextBlock>(item.Icon);
                    Assert.Equal("●", marker.Text);
                    Assert.True(marker.IsEffectivelyVisible);
                    Assert.True(marker.Bounds.Width > 0);
                });
                string? directory = Environment.GetEnvironmentVariable("OPENUTAU_KEYBOARD_CAPTURES");
                if (!string.IsNullOrEmpty(directory)) {
                    System.IO.Directory.CreateDirectory(directory);
                    using var frame = TopLevel.GetTopLevel(menu)!.CaptureRenderedFrame();
                    Assert.NotNull(frame);
                    frame.Save(System.IO.Path.Combine(directory, "tuplet-menu.png"));
                }
                menu.Close();
                for (int repeat = 0; repeat < 3; repeat++) {
                    vm.SetSnapModifierCommand.Execute(repeat == 1 ? 5 : 3).Subscribe();
                    vm.TickWidth *= 1.1;
                    Dispatcher.UIThread.RunJobs();
                    menu.Open();
                    Dispatcher.UIThread.RunJobs();
                    Assert.Equal(3, menu.GetVisualDescendants().OfType<Separator>().Count());
                    Assert.Contains(menu.GetVisualDescendants().OfType<MenuItem>(), item =>
                        item.Icon is TextBlock marker && marker.Text == "●" && marker.IsEffectivelyVisible);
                    menu.Close();
                }
            } finally {
                window.Close();
            }
        }
    }
}
