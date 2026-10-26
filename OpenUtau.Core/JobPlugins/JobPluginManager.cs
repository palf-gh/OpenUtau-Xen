using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenUtau.Core.Util;

namespace OpenUtau.Core.JobPlugins {
    /// <summary>Catalogue of JOB plugins from the folders saved in the local preferences.</summary>
    public sealed class JobPluginManager {
        public static JobPluginManager Inst { get; } = new JobPluginManager();

        public JobPluginCatalogResult Catalog { get; private set; } = new JobPluginCatalogResult();
        public event EventHandler? CatalogChanged;

        JobPluginManager() { }

        public void Rescan() {
            Catalog = JobPluginCatalog.Scan(Preferences.Default.JobPluginSearchPaths);
            foreach (var diagnostic in Catalog.Diagnostics) {
                Serilog.Log.Warning("JOB plugin: {0}", diagnostic);
            }
            CatalogChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Adds folders after existing entries, skipping paths already present.</summary>
        public void AddFolders(IEnumerable<string> folders) {
            var paths = Preferences.Default.JobPluginSearchPaths;
            var comparer = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            bool changed = false;
            foreach (var folder in folders) {
                if (string.IsNullOrWhiteSpace(folder)) {
                    continue;
                }
                string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
                if (!paths.Any(p => comparer.Equals(Path.TrimEndingDirectorySeparator(p), full))) {
                    paths.Add(full);
                    changed = true;
                }
            }
            if (changed) {
                Preferences.Save();
                Rescan();
            }
        }

        public void RemoveFolder(string folder) {
            if (Preferences.Default.JobPluginSearchPaths.Remove(folder)) {
                Preferences.Save();
                Rescan();
            }
        }
    }
}
