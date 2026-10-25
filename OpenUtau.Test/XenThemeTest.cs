using OpenUtau.Colors;
using OpenUtau.Core;
using Xunit;

namespace OpenUtau.Test {
    public sealed class XenThemeTest {
        [Fact]
        public void CustomThemeWithoutWorkspaceColoursTakesModeDefaults() {
            var dark = Yaml.DefaultDeserializer.Deserialize<CustomTheme.ThemeYaml>(
                "name: Old dark\nis_dark_mode: true\nbackground_color: '#303030'\n");
            dark.FillWorkspaceDefaults();
            Assert.Equal("#242424", dark.WorkspaceCanvasColor);
            Assert.Equal("#303030", dark.WorkspaceCardColor);

            var light = Yaml.DefaultDeserializer.Deserialize<CustomTheme.ThemeYaml>("name: Old light\n");
            light.FillWorkspaceDefaults();
            Assert.Equal("#E4E4DF", light.WorkspaceCanvasColor);
            Assert.Equal("#8F8F88", light.MutedIconColor);
        }

        [Fact]
        public void CustomThemeKeepsSavedWorkspaceColours() {
            var theme = Yaml.DefaultDeserializer.Deserialize<CustomTheme.ThemeYaml>(
                "name: New\nis_dark_mode: true\nworkspace_canvas_color: '#101010'\n");
            theme.FillWorkspaceDefaults();
            Assert.Equal("#101010", theme.WorkspaceCanvasColor);
            Assert.Equal("#3C3C3C", theme.WorkspaceElevatedSurfaceColor);
        }
    }
}
