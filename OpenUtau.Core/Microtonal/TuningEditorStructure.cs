using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenUtau.Core.Microtonal {
    /// <summary>Rebuilds detached editor data when a tuning's degree structure changes.</summary>
    public static class TuningEditorStructure {
        public static bool RequiresReset(TuningDefinition source, int divisions, int explicitCount, PitchInterval period) {
            int count = divisions > 0 ? divisions : explicitCount;
            return count != source.DegreeCount || (source.Divisions == 0) != (divisions == 0)
                || source.Divisions > 0 && Math.Abs(source.PeriodCents - period.ToCents()) > 1e-8;
        }

        public static TuningDefinition Rebuild(TuningDefinition source, int divisions, int explicitCount, PitchInterval period) {
            int count = divisions > 0 ? divisions : explicitCount;
            if (divisions < 0 || count <= 0 || count > 65536) throw new ArgumentOutOfRangeException(nameof(divisions));
            if (!RequiresReset(source, divisions, explicitCount, period)) {
                var unchanged = source.Clone();
                unchanged.Period = period.Clone();
                unchanged.Validate();
                return unchanged;
            }
            var rebuilt = TuningPresets.EqualDivision(count, period);
            var originalPreset = TuningPresets.All.FirstOrDefault(preset => preset.Id == source.Id);
            bool customName = originalPreset == null || source.Name != originalPreset.Name;
            if (customName) {
                rebuilt.Name = source.Name;
                rebuilt.Description = source.Description;
                rebuilt.Source = source.Source;
            }
            bool automaticReference = source.Divisions > 0
                ? source.Reference.Degree == TuningPresets.EqualDivision(source.Divisions, source.Period).Reference.Degree
                : originalPreset != null && source.Reference.Degree == originalPreset.Reference.Degree;
            rebuilt.Reference.Frequency = source.Reference.Frequency;
            if (!automaticReference) rebuilt.Reference.Degree = source.Reference.Degree;
            rebuilt.Keyboard.Mode = source.Keyboard.Mode;
            if (divisions == 0) {
                rebuilt.Id = "explicit-" + count;
                if (!customName) rebuilt.Name = source.Name;
                rebuilt.Divisions = 0;
                rebuilt.Degrees = source.Divisions == 0 && count <= source.Degrees.Count
                    ? source.Degrees.Take(count).Select(interval => interval.Clone()).ToList()
                    : Enumerable.Range(0, count).Select(degree => PitchInterval.FromCents(period.ToCents() * degree / count)).ToList();
                rebuilt.Scales = new List<ScaleDefinition> {
                    new ScaleDefinition { Degrees = Enumerable.Range(0, count).ToList() },
                };
                rebuilt.Notations.Clear();
                rebuilt.Keyboard.PrimaryNotation = "degree";
                rebuilt.Keyboard.SecondaryNotation = null;
                rebuilt.Keyboard.ActiveScaleId = "all";
                rebuilt.Keyboard.Classes = Enumerable.Range(0, count).ToDictionary(degree => degree, _ => "natural");
            }
            rebuilt.Validate();
            return rebuilt;
        }
    }
}
