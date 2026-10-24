using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using YamlDotNet.Serialization;

namespace OpenUtau.Core.Microtonal {
    /// <summary>An exact positive rational interval, or an explicit interval in cents.</summary>
    public sealed class PitchInterval {
        public string? Ratio { get; set; }
        public double? Cents { get; set; }
        public static PitchInterval FromRatio(string ratio) => new PitchInterval { Ratio = NormaliseRatio(ratio) };
        public static PitchInterval FromCents(double cents) => new PitchInterval { Cents = cents };
        public static string NormaliseRatio(string value) {
            var parts = value.Split('/');
            if (parts.Length > 2 || !BigInteger.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var numerator)
                    || numerator <= 0) throw new FormatException("An interval ratio needs a positive numerator.");
            var denominator = BigInteger.One;
            if (parts.Length == 2 && (!BigInteger.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out denominator)
                    || denominator <= 0)) throw new FormatException("An interval ratio needs a positive denominator.");
            var gcd = BigInteger.GreatestCommonDivisor(numerator, denominator);
            return $"{numerator / gcd}/{denominator / gcd}";
        }
        public double ToCents() {
            if ((Ratio == null) == !Cents.HasValue) throw new FormatException("Specify exactly one of ratio or cents.");
            if (Cents.HasValue) {
                if (!double.IsFinite(Cents.Value)) throw new FormatException("An interval must be finite.");
                return Cents.Value;
            }
            var parts = NormaliseRatio(Ratio!).Split('/');
            return 1200 * (BigInteger.Log(BigInteger.Parse(parts[0], CultureInfo.InvariantCulture), 2)
                - BigInteger.Log(BigInteger.Parse(parts[1], CultureInfo.InvariantCulture), 2));
        }
        public PitchInterval Clone() => new PitchInterval { Ratio = Ratio, Cents = Cents };
    }

    public sealed class TuningReference {
        public int Degree { get; set; } = 9;
        public double Frequency { get; set; } = 440;
        public TuningReference Clone() => new TuningReference { Degree = Degree, Frequency = Frequency };
    }

    public sealed class PitchAddress {
        public int Degree { get; set; }
        public double ResidualCents { get; set; }
        public string? Spelling { get; set; }
        public PitchAddress Clone() => new PitchAddress { Degree = Degree, ResidualCents = ResidualCents, Spelling = Spelling };
    }

    public enum PitchDisplayMode { Full, ScaleHighlight, Compact }

    public sealed class KeyboardLayout {
        public PitchDisplayMode Mode { get; set; } = PitchDisplayMode.Full;
        public string PrimaryNotation { get; set; } = "degree";
        public string? SecondaryNotation { get; set; }
        public string? ActiveScaleId { get; set; }
        public Dictionary<int, string> Classes { get; set; } = new Dictionary<int, string>();
        public KeyboardLayout Clone() => new KeyboardLayout {
            Mode = Mode, PrimaryNotation = PrimaryNotation, SecondaryNotation = SecondaryNotation,
            ActiveScaleId = ActiveScaleId, Classes = new Dictionary<int, string>(Classes),
        };
    }

    public sealed class NotationProfile {
        public string Id { get; set; } = "degree";
        public string Name { get; set; } = "Degree";
        public Dictionary<int, string> Names { get; set; } = new Dictionary<int, string>();
        public Dictionary<int, List<string>> Aliases { get; set; } = new Dictionary<int, List<string>>();
        public Dictionary<int, string> AccidentalIds { get; set; } = new Dictionary<int, string>();
        public Dictionary<int, int> PeriodOffsets { get; set; } = new Dictionary<int, int>();
        public NotationProfile Clone() => new NotationProfile {
            Id = Id, Name = Name, Names = new Dictionary<int, string>(Names),
            Aliases = Aliases.ToDictionary(p => p.Key, p => new List<string>(p.Value)),
            AccidentalIds = new Dictionary<int, string>(AccidentalIds),
            PeriodOffsets = new Dictionary<int, int>(PeriodOffsets),
        };
    }

    public sealed class ScaleDefinition {
        public string Id { get; set; } = "all";
        public string Name { get; set; } = "All pitches";
        public List<int> Degrees { get; set; } = new List<int>();
        public int? GeneratorDegree { get; set; }
        public int Mode { get; set; }
        public ScaleDefinition Clone() => new ScaleDefinition {
            Id = Id, Name = Name, Degrees = new List<int>(Degrees), GeneratorDegree = GeneratorDegree, Mode = Mode,
        };
    }

    /// <summary>Scala keyboard mapping. Unmapped entries are null; the display keyboard is independent.</summary>
    public sealed class InputMapping {
        public bool IsScala { get; set; }
        public int MapSize { get; set; }
        public int FirstMidi { get; set; }
        public int LastMidi { get; set; } = 127;
        public int MiddleMidi { get; set; } = 60;
        public int ReferenceMidi { get; set; } = 69;
        public double ReferenceFrequency { get; set; } = 440;
        public int FormalOctave { get; set; } = 12;
        public List<int?> Mapping { get; set; } = new List<int?>();
        public int? MapDegree(int midiKey, bool respectRange = true) {
            if (midiKey < 0 || midiKey > 127 || (respectRange && (midiKey < FirstMidi || midiKey > LastMidi))) return null;
            long offset = (long)midiKey - MiddleMidi;
            if (MapSize == 0) return checked((int)offset);
            if (MapSize < 0 || Mapping.Count != MapSize || FormalOctave <= 0) throw new FormatException("Invalid keyboard mapping.");
            var cycle = (long)Math.Floor((double)offset / MapSize);
            var index = (int)(offset - cycle * MapSize);
            return Mapping[index].HasValue ? checked((int)(cycle * FormalOctave + Mapping[index]!.Value)) : null;
        }
        public void Validate() {
            if (FirstMidi < 0 || LastMidi > 127 || FirstMidi > LastMidi || MiddleMidi < 0 || MiddleMidi > 127
                    || ReferenceMidi < 0 || ReferenceMidi > 127 || !double.IsFinite(ReferenceFrequency) || ReferenceFrequency <= 0
                    || MapSize < 0 || MapSize > 65536 || FormalOctave < 0 || Mapping == null || (MapSize > 0 && Mapping.Count != MapSize))
                throw new FormatException("Invalid Scala keyboard mapping header or entries.");
        }
        public InputMapping Clone() => new InputMapping {
            IsScala = IsScala, MapSize = MapSize, FirstMidi = FirstMidi, LastMidi = LastMidi,
            MiddleMidi = MiddleMidi, ReferenceMidi = ReferenceMidi, ReferenceFrequency = ReferenceFrequency,
            FormalOctave = FormalOctave, Mapping = new List<int?>(Mapping),
        };
    }

    /// <summary>Degree zero is the period origin, conventionally C4. Explicit degrees include unison and exclude the period.</summary>
    public sealed class TuningDefinition {
        public string Id { get; set; } = "12edo";
        public string Name { get; set; } = "12-EDO";
        public string Description { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public int Divisions { get; set; } = 12;
        public PitchInterval Period { get; set; } = PitchInterval.FromRatio("2/1");
        public List<PitchInterval> Degrees { get; set; } = new List<PitchInterval>();
        public TuningReference Reference { get; set; } = new TuningReference();
        public InputMapping InputMapping { get; set; } = new InputMapping();
        public List<ScaleDefinition> Scales { get; set; } = new List<ScaleDefinition>();
        public List<NotationProfile> Notations { get; set; } = new List<NotationProfile>();
        public KeyboardLayout Keyboard { get; set; } = new KeyboardLayout();
        [YamlIgnore] public double PeriodCents => Period.ToCents();
        [YamlIgnore] public int DegreeCount => Divisions > 0 ? Divisions : Degrees.Count;
        public double GetCents(int degree) {
            var count = DegreeCount;
            if (count <= 0) throw new FormatException("The tuning has no degrees.");
            long cycle = (long)Math.Floor((double)degree / count);
            var index = (int)(degree - cycle * count);
            return cycle * PeriodCents + (Divisions > 0 ? index * PeriodCents / Divisions : Degrees[index].ToCents());
        }
        public void Validate() {
            if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name)) throw new FormatException("A tuning needs an identifier and name.");
            if (Period == null || Reference == null || InputMapping == null || Degrees == null || Scales == null || Notations == null || Keyboard == null)
                throw new FormatException("Tuning components must not be null.");
            if (!double.IsFinite(PeriodCents) || PeriodCents <= 0) throw new FormatException("The period must exceed unison.");
            if (Divisions < 0 || Divisions > 65536 || DegreeCount <= 0 || DegreeCount > 65536) throw new FormatException("Invalid tuning degree count.");
            if (!double.IsFinite(Reference.Frequency) || Reference.Frequency <= 0) throw new FormatException("Reference frequency must be positive and finite.");
            if (Divisions == 0) {
                double previous = double.NegativeInfinity;
                for (int i = 0; i < Degrees.Count; i++) {
                    double cents = Degrees[i].ToCents();
                    if ((i == 0 && Math.Abs(cents) > 1e-8) || cents <= previous || cents >= PeriodCents)
                        throw new FormatException("Explicit degrees must start at unison and increase strictly below the period.");
                    previous = cents;
                }
            }
            InputMapping.Validate();
            if (Scales.Any(s => s.Degrees.Any(d => d < 0 || d >= DegreeCount) || s.Degrees.Distinct().Count() != s.Degrees.Count))
                throw new FormatException("Scale degrees must be distinct and inside the period.");
            if (Scales.Select(s => s.Id).Distinct().Count() != Scales.Count || Notations.Select(n => n.Id).Distinct().Count() != Notations.Count)
                throw new FormatException("Scale and notation identifiers must be unique.");
        }
        public TuningDefinition Clone() => new TuningDefinition {
            Id = Id, Name = Name, Description = Description, Source = Source, Divisions = Divisions,
            Period = Period.Clone(), Degrees = Degrees.Select(d => d.Clone()).ToList(), Reference = Reference.Clone(),
            InputMapping = InputMapping.Clone(), Scales = Scales.Select(s => s.Clone()).ToList(),
            Notations = Notations.Select(n => n.Clone()).ToList(), Keyboard = Keyboard.Clone(),
        };
        public override string ToString() => Name;
    }
}
