using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace OpenUtau.Core.Microtonal {
    public sealed class TunxenDocument {
        public string Format { get; set; } = string.Empty;
        public int Version { get; set; }
        public TuningDefinition? Tuning { get; set; }
    }

    public static class TunxenFormat {
        public const string Extension = ".tunxen";
        /// <summary>
        /// Version 2 adds the keyboard layout, keyboard MOS and scale tonic; version 3 adds the keyboard MOS repeat
        /// count. Version 1 is upgraded on reading; version 2 reads unchanged with one repeat.
        /// </summary>
        public const int CurrentVersion = 3;
        static readonly ISerializer Serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull).Build();
        static readonly IDeserializer Deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance).Build();
        // YamlDotNet's object factory caches are not thread-safe, as in Yaml.
        static readonly object serializerLock = new object();
        static readonly object deserializerLock = new object();
        public static TuningDefinition Parse(string text) {
            TunxenDocument? parsed;
            lock (deserializerLock) parsed = Deserializer.Deserialize<TunxenDocument>(text);
            var document = parsed ?? throw new FormatException("Empty tuning document.");
            if (document.Format != "OpenUtauXenTuning" || document.Version < 1 || document.Version > CurrentVersion)
                throw new FormatException("Unsupported OpenUtau Xen tuning format or version.");
            if (document.Tuning == null) throw new FormatException("The document has no tuning.");
            if (document.Version == 1) KeyboardRegionizer.UpgradeFormat1(document.Tuning);
            document.Tuning.Validate();
            return document.Tuning;
        }
        public static string Serialize(TuningDefinition tuning) {
            tuning.Validate();
            lock (serializerLock) {
                return Serializer.Serialize(new TunxenDocument { Format = "OpenUtauXenTuning", Version = CurrentVersion, Tuning = tuning });
            }
        }
        public static TuningDefinition Load(string path) => Parse(File.ReadAllText(path));
        public static void Save(string path, TuningDefinition tuning) => File.WriteAllText(path, Serialize(tuning), new UTF8Encoding(false));
        public static TuningDefinition Read(string text) => Parse(text);
        public static string Write(TuningDefinition tuning) => Serialize(tuning);
    }

    /// <summary>Scala scale files contain the period as the final interval, without an explicit unison.</summary>
    public static class ScalaScl {
        static string RemoveComment(string line) => line.Split('!')[0].Trim();
        public static TuningDefinition Parse(string text) {
            var lines = text.Replace("\r", "").Split('\n').Where(l => !l.TrimStart().StartsWith("!", StringComparison.Ordinal)).ToList();
            if (lines.Count < 2) throw new FormatException("A Scala scale needs a description and degree count.");
            string description = RemoveComment(lines[0]);
            var data = lines.Skip(1).Select(RemoveComment).Where(l => l.Length > 0).ToList();
            if (data.Count == 0 || !int.TryParse(data[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)
                    || count <= 0 || count > 65536 || data.Count != count + 1)
                throw new FormatException("Scala scale degree count does not match its intervals.");
            var intervals = data.Skip(1).Select(ParseInterval).ToList();
            var period = intervals[^1];
            var tuning = new TuningDefinition {
                Id = "scala-" + Guid.NewGuid().ToString("N"), Name = description.Length == 0 ? "Scala scale" : description,
                Description = description, Source = "Imported Scala .scl", Divisions = 0,
                Period = period, Degrees = new List<PitchInterval> { PitchInterval.FromRatio("1/1") },
            };
            tuning.Degrees.AddRange(intervals.Take(count - 1));
            tuning.Reference.Degree = Enumerable.Range(0, count).OrderBy(d => Math.Abs(tuning.GetCents(d) - 900)).First();
            tuning.InputMapping.FormalOctave = count;
            tuning.Scales.Add(new ScaleDefinition { Degrees = Enumerable.Range(0, count).ToList() });
            tuning.Validate();
            return tuning;
        }
        static PitchInterval ParseInterval(string value) {
            string token = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
            if (token.Contains('/') || (!token.Contains('.') && !token.Contains('e') && !token.Contains('E')))
                return PitchInterval.FromRatio(token);
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double cents) || !double.IsFinite(cents))
                throw new FormatException("Invalid Scala cents interval.");
            return PitchInterval.FromCents(cents);
        }
        static string FormatInterval(PitchInterval interval) => interval.Ratio != null
            ? PitchInterval.NormaliseRatio(interval.Ratio)
            : interval.Cents!.Value.ToString("0.0################", CultureInfo.InvariantCulture);
        public static string Serialize(TuningDefinition tuning) {
            tuning.Validate();
            var writer = new StringBuilder("! OpenUtau Xen Scala export\n");
            writer.AppendLine(tuning.Name.Replace('\r', ' ').Replace('\n', ' ').Replace('!', ' '));
            writer.AppendLine(tuning.DegreeCount.ToString(CultureInfo.InvariantCulture));
            for (int i = 1; i < tuning.DegreeCount; i++) {
                writer.AppendLine(FormatInterval(tuning.Divisions > 0 ? PitchInterval.FromCents(tuning.GetCents(i)) : tuning.Degrees[i]));
            }
            writer.AppendLine(FormatInterval(tuning.Period));
            return writer.ToString();
        }
        public static TuningDefinition Read(string text) => Parse(text);
        public static string Write(TuningDefinition tuning) => Serialize(tuning);
        public static TuningDefinition Load(string path) => Parse(File.ReadAllText(path));
        public static void Save(string path, TuningDefinition tuning) => File.WriteAllText(path, Serialize(tuning), new UTF8Encoding(false));
    }

    public static class ScalaKbm {
        public static InputMapping FromTuning(TuningDefinition tuning) {
            tuning.Validate();
            if (tuning.InputMapping.IsScala) {
                var existing = tuning.InputMapping.Clone();
                var reference = existing.MapDegree(existing.ReferenceMidi) ?? throw new FormatException("Unmapped reference key.");
                existing.ReferenceFrequency = PitchResolver.ResolveFrequency(tuning, new PitchAddress { Degree = reference });
                return existing;
            }
            var mapping = new InputMapping {
                IsScala = true, MapSize = 128, FirstMidi = tuning.InputMapping.FirstMidi, LastMidi = tuning.InputMapping.LastMidi,
                MiddleMidi = 0, ReferenceMidi = tuning.InputMapping.ReferenceMidi, FormalOctave = tuning.DegreeCount,
            };
            for (int key = 0; key < 128; key++) mapping.Mapping.Add(PitchResolver.MapMidi(tuning, key)?.Degree);
            var address = PitchResolver.MapMidi(tuning, mapping.ReferenceMidi) ?? throw new FormatException("Unmapped reference key.");
            mapping.ReferenceFrequency = PitchResolver.ResolveFrequency(tuning, address);
            return mapping;
        }
        public static InputMapping Parse(string text) {
            var data = text.Replace("\r", "").Split('\n').Select(l => l.Split('!')[0].Trim()).Where(l => l.Length > 0).ToList();
            if (data.Count < 7) throw new FormatException("A Scala keyboard mapping needs seven header values.");
            int ReadInt(int index) => int.Parse(data[index], NumberStyles.Integer, CultureInfo.InvariantCulture);
            var mapping = new InputMapping {
                IsScala = true, MapSize = ReadInt(0), FirstMidi = ReadInt(1), LastMidi = ReadInt(2),
                MiddleMidi = ReadInt(3), ReferenceMidi = ReadInt(4),
                ReferenceFrequency = double.Parse(data[5], NumberStyles.Float, CultureInfo.InvariantCulture), FormalOctave = ReadInt(6),
            };
            if (mapping.MapSize < 0 || mapping.MapSize > 65536 || data.Count != 7 + mapping.MapSize)
                throw new FormatException("Scala keyboard mapping size does not match its entries.");
            for (int i = 0; i < mapping.MapSize; i++) {
                string item = data[i + 7];
                mapping.Mapping.Add(item.Equals("x", StringComparison.OrdinalIgnoreCase) ? null : int.Parse(item, NumberStyles.Integer, CultureInfo.InvariantCulture));
            }
            mapping.Validate();
            return mapping;
        }
        public static void ApplyTo(TuningDefinition tuning, InputMapping mapping) {
            mapping.Validate();
            var normalised = mapping.Clone();
            if (normalised.FormalOctave == 0) normalised.FormalOctave = tuning.DegreeCount;
            var degree = normalised.MapDegree(normalised.ReferenceMidi, false)
                ?? throw new FormatException("The reference key must be mapped.");
            tuning.InputMapping = normalised;
            tuning.InputMapping.IsScala = true;
            tuning.Reference = new TuningReference { Degree = degree, Frequency = mapping.ReferenceFrequency };
            tuning.Validate();
        }
        public static string Serialize(InputMapping mapping) {
            mapping.Validate();
            var rows = new List<string> {
                "! OpenUtau Xen keyboard mapping", mapping.MapSize.ToString(CultureInfo.InvariantCulture),
                mapping.FirstMidi.ToString(CultureInfo.InvariantCulture), mapping.LastMidi.ToString(CultureInfo.InvariantCulture),
                mapping.MiddleMidi.ToString(CultureInfo.InvariantCulture), mapping.ReferenceMidi.ToString(CultureInfo.InvariantCulture),
                mapping.ReferenceFrequency.ToString("R", CultureInfo.InvariantCulture), mapping.FormalOctave.ToString(CultureInfo.InvariantCulture),
            };
            if (mapping.MapSize > 0) rows.AddRange(mapping.Mapping.Select(v => v?.ToString(CultureInfo.InvariantCulture) ?? "x"));
            return string.Join("\n", rows) + "\n";
        }
        public static InputMapping Read(string text) => Parse(text);
        public static string Write(InputMapping mapping) => Serialize(mapping);
        public static string Serialize(TuningDefinition tuning) => Serialize(FromTuning(tuning));
        public static string Write(TuningDefinition tuning) => Serialize(tuning);
        public static InputMapping Load(string path) => Parse(File.ReadAllText(path));
        public static void Save(string path, InputMapping mapping) => File.WriteAllText(path, Serialize(mapping), new UTF8Encoding(false));
        public static void Save(string path, TuningDefinition tuning) => File.WriteAllText(path, Serialize(tuning), new UTF8Encoding(false));
    }
}
