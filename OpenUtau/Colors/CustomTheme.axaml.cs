using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Media;
using OpenUtau.Core;
using OpenUtau.Core.Util;
using Serilog;

namespace OpenUtau.Colors;
public class CustomTheme {
    public static Dictionary<string, string> Themes = [];
    static HashSet<string> LocalThemes = [];
    static HashSet<string> PackageThemes = [];
    public static ThemeYaml Default;

    public static bool IsPackageTheme(string themeName) => PackageThemes.Contains(themeName);

    public static void MarkPackageTheme(string themeName) => PackageThemes.Add(themeName);

    internal static void ClearPackageThemes() {
        foreach (var key in PackageThemes) Themes.Remove(key);
        PackageThemes.Clear();
    }

    static CustomTheme() {
        Default = new ThemeYaml();
        ListThemes();
    }

    public static void Load(string themeName) {
        if (!string.IsNullOrEmpty(themeName) && Themes.TryGetValue(themeName, out var themePath) && File.Exists(themePath)) {
            try {
                Default = Yaml.DefaultDeserializer.Deserialize<ThemeYaml>(File.ReadAllText(themePath, Encoding.UTF8));
                return;
            } catch (Exception e) {
                Log.Error(e, $"Failed to parse yaml in {themePath}");
            }
        }

        Preferences.Default.ThemeName = "Light";
        Default = new ThemeYaml();
    }

    public static void ListThemes() {
        foreach (var key in LocalThemes) Themes.Remove(key);
        LocalThemes.Clear();
        Directory.CreateDirectory(PathManager.Inst.ThemesPath);
        foreach (var item in Directory.EnumerateFiles(PathManager.Inst.ThemesPath, "*.yaml")) {
            try {
                string baseName = Yaml.DefaultDeserializer.Deserialize<ThemeYaml>(File.ReadAllText(item, Encoding.UTF8)).Name;
                string themeName = baseName;
                int dupIter = 1;
                while (Themes.ContainsKey(themeName)) {
                    themeName = $"{baseName} ({dupIter})";
                    dupIter++;
                }
                Themes.Add(themeName, item);
                LocalThemes.Add(themeName);
            } catch (Exception e) {
                Log.Error(e, $"Failed to parse yaml in {item}");
            }
        }
    }

    public static void ApplyTheme(string themeName) {
        Load(themeName);

        if (Application.Current != null) {
            Application.Current.Resources["IsDarkMode"] = Default.IsDarkMode; 
            SetResourceColor("BackgroundColor", Default.BackgroundColor);
            SetResourceColor("BackgroundColorPointerOver", Default.BackgroundColorPointerOver);
            SetResourceColor("BackgroundColorPressed", Default.BackgroundColorPressed);
            SetResourceColor("BackgroundColorDisabled", Default.BackgroundColorDisabled);

            SetResourceColor("ForegroundColor", Default.ForegroundColor);
            SetResourceColor("ForegroundColorPointerOver", Default.ForegroundColorPointerOver);
            SetResourceColor("ForegroundColorPressed", Default.ForegroundColorPressed);
            SetResourceColor("ForegroundColorDisabled", Default.ForegroundColorDisabled);

            SetResourceColor("BorderColor", Default.BorderColor);
            SetResourceColor("BorderColorPointerOver", Default.BorderColorPointerOver);

            SetResourceColor("SystemAccentColor", Default.SystemAccentColor);
            SetResourceColor("SystemAccentColorLight1", Default.SystemAccentColorLight1);
            SetResourceColor("SystemAccentColorDark1", Default.SystemAccentColorDark1);

            SetResourceColor("NeutralAccentColor", Default.NeutralAccentColor);
            SetResourceColor("NeutralAccentColorPointerOver", Default.NeutralAccentColorPointerOver);
            SetResourceColor("AccentColor1", Default.AccentColor1);
            SetResourceColor("AccentColor2", Default.AccentColor2);
            SetResourceColor("AccentColor3", Default.AccentColor3);

            SetResourceColor("TickLineColor", Default.TickLineColor);
            SetResourceColor("BarNumberColor", Default.BarNumberColor);
            SetResourceColor("FinalPitchColor", Default.FinalPitchColor);
            SetResourceColor("TrackBackgroundAltColor", Default.TrackBackgroundAltColor);

            SetResourceColor("WhiteKeyColorLeft", Default.WhiteKeyColorLeft);
            SetResourceColor("WhiteKeyColorRight", Default.WhiteKeyColorRight);
            SetResourceColor("WhiteKeyNameColor", Default.WhiteKeyNameColor);

            SetResourceColor("CenterKeyColorLeft", Default.CenterKeyColorLeft);
            SetResourceColor("CenterKeyColorRight", Default.CenterKeyColorRight);
            SetResourceColor("CenterKeyNameColor", Default.CenterKeyNameColor);

            SetResourceColor("BlackKeyColorLeft", Default.BlackKeyColorLeft);
            SetResourceColor("BlackKeyColorRight", Default.BlackKeyColorRight);
            SetResourceColor("BlackKeyNameColor", Default.BlackKeyNameColor);

            SetResourceColor("BetweenKeyColorLeft", Default.BetweenKeyColorLeft);
            SetResourceColor("BetweenKeyColorRight", Default.BetweenKeyColorRight);
            SetResourceColor("BetweenKeyNameColor", Default.BetweenKeyNameColor);
        }
    }

    private static void SetResourceColor(string res, string colorStr) {
        if (Color.TryParse(colorStr, out var color)) {
            Application.Current!.Resources[res] = color;
        } else {
            Log.Error($"Failed to parse color \"{colorStr}\" in {Default.Name} custom theme");
        }
    }
    
    [Serializable]
    public class ThemeYaml {
        public string Name = "Custom YAML";
            
        public bool IsDarkMode = false;
        public string BackgroundColor = "#F2F2EF";
        public string BackgroundColorPointerOver = "#E8E8E4";
        public string BackgroundColorPressed = "#DADAD5";
        public string BackgroundColorDisabled = "#CDCDC8";

        public string ForegroundColor = "#181818";
        public string ForegroundColorPointerOver = "#181818";
        public string ForegroundColorPressed = "#2A2A2A";
        public string ForegroundColorDisabled = "#808080";
        
        public string BorderColor = "#707070";
        public string BorderColorPointerOver = "#B0B0B0";

        public string SystemAccentColor = "#3344D6";
        public string SystemAccentColorLight1 = "#8E98EE";
        public string SystemAccentColorDark1 = "#2433B0";

        public string NeutralAccentColor = "#A8A8A0";
        public string NeutralAccentColorPointerOver = "#8F8F88";
        public string AccentColor1 = "#3344D6";
        public string AccentColor2 = "#6B8F12";
        public string AccentColor3 = "#4F6B0B";

        public string TickLineColor = "#B5B5AE";
        public string BarNumberColor = "#9C9C95";
        public string FinalPitchColor = "#C0C0C0";
        public string TrackBackgroundAltColor = "#E8E8E4";

        public string WhiteKeyColorLeft = "Transparent";
        public string WhiteKeyColorRight = "Transparent";
        public string WhiteKeyNameColor = "#5C7410";
            
        public string CenterKeyColorLeft = "#EEF7C8";
        public string CenterKeyColorRight = "#E2F1A3";
        public string CenterKeyNameColor = "#4D5E0A";
            
        public string BlackKeyColorLeft = "#D9EF85";
        public string BlackKeyColorRight = "#CBE960";
        public string BlackKeyNameColor = "#101010";

        public string BetweenKeyColorLeft = "#C9CEF8";
        public string BetweenKeyColorRight = "#AEB6F4";
        public string BetweenKeyNameColor = "#1E2BA8";
    }
}

