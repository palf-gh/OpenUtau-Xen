using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;

namespace OpenUtau.App.Views {
    internal static class XenDialogStyles {
        public static void Apply(Window window) {
            window.Styles.Add(new StyleInclude(new Uri("avares://OpenUtau/")) {
                Source = new Uri("avares://OpenUtau/Styles/XenDialogs.axaml"),
            });
            window.Opened += (_, _) => {
                var screen = window.Screens.ScreenFromWindow(window);
                if (screen == null) return;
                window.MaxHeight = Math.Max(320, screen.WorkingArea.Height / window.RenderScaling - 40);
                window.MaxWidth = Math.Max(470, screen.WorkingArea.Width / window.RenderScaling - 32);
            };
        }
    }
}
