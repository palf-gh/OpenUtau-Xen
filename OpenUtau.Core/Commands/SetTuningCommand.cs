using System;
using System.Collections.Generic;
using System.Linq;
using OpenUtau.Core.Microtonal;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Core {
    /// <summary>One atomic, reversible tuning/reference/layout change.</summary>
    public sealed class SetTuningCommand : UCommand {
        readonly UProject project;
        readonly UTrack? track;
        readonly TuningDefinition? before;
        readonly TuningDefinition? after;
        readonly List<(UNote note, UTrack track, PitchAddress? before, int tone, PitchAddress? after)> changes = new();
        public IReadOnlyList<(double Before, double After)> Preview { get; }

        public SetTuningCommand(UProject project, UTrack? track, TuningDefinition? tuning,
            bool preserveFrequency = false, bool referenceOnly = false, bool displayOnly = false) {
            this.project = project;
            this.track = track;
            before = track == null ? project.pitchSystem?.Clone() : track.pitchSystem?.Clone();
            after = tuning?.Clone();
            if (tuning != null && (referenceOnly || displayOnly)) {
                var current = project.GetTuning(track).Clone();
                if (referenceOnly) current.Reference = tuning.Reference.Clone();
                if (displayOnly) {
                    current.Keyboard = tuning.Keyboard.Clone();
                    current.Scales = tuning.Scales.Select(s => s.Clone()).ToList();
                    current.Notations = tuning.Notations.Select(n => n.Clone()).ToList();
                }
                after = current;
            }
            if (track == null && after == null) {
                throw new ArgumentException("A project must have a tuning.", nameof(tuning));
            }
            var target = after ?? project.GetTuning();
            target.Validate();
            var preview = new List<(double Before, double After)>();
            foreach (var part in project.parts.OfType<UVoicePart>()) {
                var partTrack = project.tracks[part.trackNo];
                if (track != null ? partTrack != track : partTrack.pitchSystem != null) {
                    continue;
                }
                var source = project.GetTuning(partTrack);
                bool retainAddress = displayOnly || SamePitchGrid(source, target);
                foreach (var note in part.notes) {
                    double midi = note.pitchAddress == null ? note.tone : PitchResolver.ResolveMidi(source, note.pitchAddress);
                    PitchAddress? address = retainAddress ? note.pitchAddress?.Clone()
                        : referenceOnly ? note.pitchAddress?.Clone() ?? PitchResolver.NearestAddress(source, midi, true)
                        : PitchResolver.NearestAddress(target, midi, preserveFrequency);
                    changes.Add((note, partTrack, note.pitchAddress?.Clone(), note.tone, address));
                    preview.Add((midi, address == null ? midi : PitchResolver.ResolveMidi(target, address)));
                }
            }
            Preview = preview;
        }

        public override void Execute() {
            if (track == null) project.pitchSystem = after!.Clone();
            else track.pitchSystem = after?.Clone();
            foreach (var change in changes) {
                change.note.pitchAddress = change.after?.Clone();
                change.note.RefreshPitch(project, change.track);
            }
        }

        public override void Unexecute() {
            if (track == null) project.pitchSystem = before!.Clone();
            else track.pitchSystem = before?.Clone();
            foreach (var change in changes) {
                change.note.pitchAddress = change.before?.Clone();
                change.note.tone = change.tone;
                change.note.RefreshPitch(project, change.track);
            }
        }

        public override string ToString() => "Change tuning";

        static bool SamePitchGrid(TuningDefinition left, TuningDefinition right) {
            if (left.DegreeCount != right.DegreeCount || Math.Abs(left.PeriodCents - right.PeriodCents) > 1e-9)
                return false;
            if (Math.Abs(PitchResolver.ResolveMidi(left, new PitchAddress())
                - PitchResolver.ResolveMidi(right, new PitchAddress())) > 1e-11) return false;
            for (int degree = 0; degree < left.DegreeCount; degree++) {
                if (Math.Abs(left.GetCents(degree) - right.GetCents(degree)) > 1e-9) return false;
            }
            return true;
        }
    }
}
