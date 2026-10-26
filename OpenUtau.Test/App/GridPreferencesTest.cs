using System;
using System.Reactive.Linq;
using Newtonsoft.Json;
using OpenUtau.App.ViewModels;
using OpenUtau.Core.Util;
using Xunit;

namespace OpenUtau.App {
    internal static class GridPreferencesTest {
        public static void Verify() {
            var original = Preferences.Default;
            try {
                Preferences.Default = new Preferences.SerializablePreferences();
                var first = new NotesViewModel();
                first.SetSnapUnitCommand.Execute(1008).Subscribe();
                first.SetSnapModifierCommand.Execute(5).Subscribe();
                first.IsSnapOn = false;
                Preferences.Default = JsonConvert.DeserializeObject<Preferences.SerializablePreferences>(
                    JsonConvert.SerializeObject(Preferences.Default))!;
                var restored = new NotesViewModel();
                Assert.Equal(8, restored.Grid.Division);
                Assert.True(restored.Grid.Dotted);
                Assert.Equal(5, restored.Grid.Tuplet);
                Assert.False(restored.IsSnapOn);
                Assert.Equal(first.SnapDivText, restored.SnapDivText);
                restored.TickWidth *= 2;
                Assert.Equal(8, Preferences.Default.XenSnapDivision);
                restored.SetSnapModifierCommand.Execute(5).Subscribe();
                Assert.Equal(1, new NotesViewModel().Grid.Tuplet);
                restored.SetSnapUnitCommand.Execute(-2).Subscribe();
                var automatic = new NotesViewModel();
                Assert.False(automatic.Grid.Dotted);
                Assert.Equal(-2, Preferences.Default.XenSnapDivision);
                Preferences.Default.XenSnapDivision = 6;
                Preferences.Default.XenSnapTuplet = 99;
                var invalid = new NotesViewModel();
                Assert.Equal(1, invalid.Grid.Tuplet);
                Assert.False(invalid.Grid.Dotted);
                Assert.Contains("(", invalid.SnapDivText);
            } finally {
                Preferences.Default = original;
                Preferences.Save();
            }
        }
    }
}
