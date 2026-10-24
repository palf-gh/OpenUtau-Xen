using OpenUtau.Core.Microtonal;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Core {
    /// <summary>Changes a note's exact tuning degree without semitone quantisation.</summary>
    public sealed class SetNotePitchAddressCommand : NoteCommand {
        readonly PitchAddress? before;
        readonly PitchAddress after;
        readonly int oldTone;
        public SetNotePitchAddressCommand(UVoicePart part, UNote note, PitchAddress address) : base(part, note) {
            before = note.pitchAddress?.Clone();
            after = address.Clone();
            oldTone = note.tone;
        }
        public override string ToString() => "Change note pitch";
        public override void Execute() => Apply(after);
        public override void Unexecute() {
            if (before == null) Notes[0].tone = oldTone;
            Apply(before);
        }
        void Apply(PitchAddress? address) {
            lock (Part) {
                var note = Notes[0];
                note.pitchAddress = address?.Clone();
                var project = DocManager.Inst.Project;
                if (Part.trackNo >= 0 && Part.trackNo < project.tracks.Count) {
                    note.RefreshPitch(project, project.tracks[Part.trackNo]);
                }
            }
        }
    }
}
