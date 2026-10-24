using System;
using System.Collections.Generic;
using System.Linq;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Core.Microtonal {
    public static class MidiInputRecording {
        /// <summary>Convert a completed take through the project's tempo map, retaining exact pitch addresses.</summary>
        public static List<UNote> BuildNotes(UProject project, UVoicePart part,
            IEnumerable<MidiInputNote> take, double recordingEpochMilliseconds, double projectStartMilliseconds) {
            if (!double.IsFinite(recordingEpochMilliseconds) || !double.IsFinite(projectStartMilliseconds)) {
                throw new ArgumentOutOfRangeException(nameof(recordingEpochMilliseconds));
            }
            return take.Select(item => {
                var startMs = projectStartMilliseconds + Math.Max(0, item.StartMilliseconds - recordingEpochMilliseconds);
                var endMs = projectStartMilliseconds + Math.Max(0, item.EndMilliseconds - recordingEpochMilliseconds);
                var startTick = project.timeAxis.MsPosToTickPos(startMs) - part.position;
                var endTick = project.timeAxis.MsPosToTickPos(endMs) - part.position;
                // This is only the legacy/phonemiser projection. The address retains the actual pitch.
                var midi = PitchResolver.CompatibilityTone(item.MidiTone);
                var note = project.CreateNote(midi, Math.Max(0, startTick), Math.Max(10, endTick - startTick));
                note.pitchAddress = item.Address.Clone();
                return note;
            }).OrderBy(note => note.position).ToList();
        }
    }
}
