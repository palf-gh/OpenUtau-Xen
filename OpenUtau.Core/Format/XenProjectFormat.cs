using System;
using System.IO;
using System.Linq;
using System.Text;
using OpenUtau.Core.Microtonal;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Core.Format {
    /// <summary>Native Xen envelope. Upstream USTX versioning remains independent.</summary>
    public static class XenProjectFormat {
        public const string Identifier = "openutau-xen-project";
        /// <summary>
        /// Version 2 stores tunings with keyboard layout, keyboard MOS and scale tonic; version 3 adds the keyboard
        /// MOS repeat count. Version 1 is upgraded; version 2 reads unchanged with one repeat.
        /// </summary>
        // Version 4 adds optional note vibrato curves; older notes need no conversion.
        public const int CurrentVersion = 4;
        public const string Extension = ".ustxen";
        public sealed class Document {
            public string? Format { get; set; }
            public int Version { get; set; }
            public UProject? Project { get; set; }
        }

        public static string Serialize(UProject project) {
            project.BeforeSave();
            try { return SerializePrepared(project); }
            finally { project.AfterSave(); }
        }
        internal static string SerializePrepared(UProject project) =>
            Yaml.DefaultSerializer.Serialize(new Document {
                Format = Identifier, Version = CurrentVersion, Project = project,
            });

        public static UProject Parse(string text, string filePath = "") {
            var document = Yaml.DefaultDeserializer.Deserialize<Document>(text);
            if (document == null || document.Format != Identifier || document.Version < 1 || document.Version > CurrentVersion || document.Project == null) {
                throw new FileFormatException("Unsupported OpenUtau Xen project format or version.");
            }
            var project = document.Project;
            if (document.Version == 1) {
                if (project.pitchSystem != null) KeyboardRegionizer.UpgradeFormat1(project.pitchSystem);
                foreach (var track in project.tracks) if (track.pitchSystem != null) KeyboardRegionizer.UpgradeFormat1(track.pitchSystem);
                KeyboardRegionizer.ApplyUpstreamKey(project.pitchSystem, project.key);
                foreach (var track in project.tracks) KeyboardRegionizer.ApplyUpstreamKey(track.pitchSystem, project.key);
            }
            project.GetTuning().Validate();
            foreach (var track in project.tracks) track.pitchSystem?.Validate();
            return Ustx.InitialiseProject(project, filePath);
        }
        public static UProject Load(string path) => Parse(File.ReadAllText(path, Encoding.UTF8), path);

        public sealed class CompatibilityReport {
            public int BakedVibratoNotes { get; set; }
            public int Notes { get; internal set; }
            public double MaximumErrorCents { get; internal set; }
            public string Summary => $"Tuning definitions, scales and spellings will be removed. Maximum rounding {MaximumErrorCents:F3} cent. Variable vibrato notes baked: {BakedVibratoNotes}.";
        }

        public static CompatibilityReport GetCompatibilityReport(UProject project) {
            var report = new CompatibilityReport();
            foreach (var part in project.parts.OfType<UVoicePart>()) {
                var tuning = project.GetTuning(project.tracks[part.trackNo]);
                foreach (var note in part.notes) {
                    double midi = note.pitchAddress == null ? note.tone : PitchResolver.ResolveMidi(tuning, note.pitchAddress);
                    double cents = midi * 100 + note.tuning;
                    report.MaximumErrorCents = Math.Max(report.MaximumErrorCents, Math.Abs(cents - Math.Round(cents, MidpointRounding.AwayFromZero)));
                    report.Notes++;
                    if (note.vibrato.HasCurves && note.vibrato.length > 0) report.BakedVibratoNotes++;
                }
            }
            return report;
        }

        internal static string SerializeCompatibilityPrepared(UProject project) {
            // Work on a detached DTO. Export must not retune or change the open project.
            var copy = Yaml.DefaultDeserializer.Deserialize<UProject>(Yaml.DefaultSerializer.Serialize(project));
            copy.timeAxis.BuildSegments(copy);
            foreach (var part in copy.voiceParts ?? Enumerable.Empty<UVoicePart>()) {
                var tuning = copy.GetTuning(copy.tracks[part.trackNo]);
                foreach (var note in part.notes) {
                    note.RefreshPitch(copy, copy.tracks[part.trackNo]);
                    if (note.vibrato.HasCurves) {
                        note.pitch = VibratoCompatibility.Bake(copy, part, note, note.pitch);
                        note.vibrato.length = 0;
                        note.vibrato.rateCurve = null;
                        note.vibrato.depthCurve = null;
                    }
                    double midi = note.pitchAddress == null ? note.tone : PitchResolver.ResolveMidi(tuning, note.pitchAddress);
                    note.tone = checked((int)Math.Ceiling(midi - 0.5));
                    note.tuning += checked((int)Math.Round((midi - note.tone) * 100, MidpointRounding.AwayFromZero));
                    note.pitchAddress = null;
                }
            }
            foreach (var track in copy.tracks) track.pitchSystem = null;
            copy.pitchSystem = null!;
            copy.ustxVersion = Ustx.kUstxVersion;
            return Yaml.DefaultSerializer.Serialize(copy);
        }

        public static void ExportUstx(string path, UProject project) {
            if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(project.FilePath.Length == 0 ? path + ".unsaved" : project.FilePath), StringComparison.OrdinalIgnoreCase)) {
                throw new IOException("Choose a separate path for compatibility export.");
            }
            project.BeforeSave();
            try { WriteAtomic(path, SerializeCompatibilityPrepared(project)); }
            finally { project.AfterSave(); }
        }

        internal static void WriteAtomic(string path, string text) {
            string temporary = Path.GetFullPath(path) + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                File.WriteAllText(temporary, text, new UTF8Encoding(false));
                File.Move(temporary, path, true);
            } finally {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
