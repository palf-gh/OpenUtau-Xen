using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

namespace OpenUtau.App {
    /// <summary>Xen-owned overlays keep upstream Crowdin dictionaries untouched.</summary>
    public static class XenStrings {
        public static CultureInfo InputCulture { get; private set; } = CultureInfo.GetCultureInfo("en-US");
        public static string PresetName(Core.Microtonal.TuningDefinition tuning) => tuning.Id switch {
            "ji-5-limit" => Get("preset.ji5"),
            "ji-7-limit" => Get("preset.ji7"),
            "pythagorean" => Get("preset.pythagorean"),
            "meantone-quarter-comma" => Get("preset.quarter"),
            "meantone-sixth-comma" => Get("preset.sixth"),
            "rast-24edo-approximation" => Get("preset.rast"),
            "bohlen-pierce-13edt" => "Bohlen–Pierce (13-EDT)",
            _ => tuning.Name,
        };
        /// <summary>Avalonia asset root of this assembly; follows the executable name.</summary>
        public static readonly string AssetRoot = $"avares://{typeof(XenStrings).Assembly.GetName().Name}/";
        static readonly Dictionary<string, IResourceDictionary> dictionaries = new();
        public static IResourceDictionary GetResources(string language) {
            if (!dictionaries.TryGetValue(language, out var resource)) {
                resource = new ResourceInclude(new Uri(AssetRoot)) {
                    Source = new Uri($"{AssetRoot}Strings/Xen.{language}.axaml"),
                }.Loaded;
                dictionaries.Add(language, resource);
            }
            return resource;
        }
        public static void SetLanguage(string language) {
            InputCulture = CultureInfo.GetCultureInfo(language);
            if (Application.Current == null) return;
            foreach (var resource in dictionaries.Values) Application.Current.Resources.MergedDictionaries.Remove(resource);
            Application.Current.Resources.MergedDictionaries.Add(GetResources("en-US"));
            if (language != "en-US") Application.Current.Resources.MergedDictionaries.Add(GetResources(language));
        }
        public static string Get(string key, params object[] arguments) {
            string fullKey = key.StartsWith("xen.", StringComparison.Ordinal) ? key : "xen." + key;
            if (!ThemeManager.TryGetString(fullKey, out var text)) {
                text = GetResources("en-US").TryGetResource(fullKey, ThemeVariant.Default, out var fallback)
                    ? (string)fallback! : fullKey;
            }
            return arguments.Length == 0 ? text : string.Format(InputCulture, text, arguments);
        }
    }
}
