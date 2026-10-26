using System.Collections.Generic;
using System.Linq;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Core {
    public sealed class SetVibratoCurvesCommand : NoteCommand {
        readonly UVibrato[] oldVibratos;
        readonly UVibrato[] newVibratos;

        public SetVibratoCurvesCommand(UVoicePart part, IEnumerable<UNote> notes,
            IEnumerable<VibratoPoint>? rateCurve, IEnumerable<VibratoPoint>? depthCurve,
            float? length = null, int? preset = null) : base(part, notes.ToArray()) {
            oldVibratos = Notes.Select(note => note.vibrato.Clone()).ToArray();
            newVibratos = oldVibratos.Select(vibrato => {
                var result = vibrato.Clone();
                result.rateCurve = Copy(rateCurve);
                result.depthCurve = Copy(depthCurve);
                result.preset = preset;
                if (length.HasValue) {
                    result.length = length.Value;
                }
                result.ValidateCurves();
                return result;
            }).ToArray();
        }

        static List<VibratoPoint>? Copy(IEnumerable<VibratoPoint>? points) =>
            points?.Select(point => new VibratoPoint { x = point.x, y = point.y }).ToList();

        public override string ToString() => "Change vibrato curves";

        public override ValidateOptions ValidateOptions => new ValidateOptions {
            SkipTiming = true,
            Part = Part,
            SkipPhonemizer = true,
            SkipPhoneme = true,
        };

        public override void Execute() => Set(newVibratos);
        public override void Unexecute() => Set(oldVibratos);

        void Set(UVibrato[] vibratos) {
            lock (Part) {
                for (int i = 0; i < Notes.Length; i++) {
                    Notes[i].vibrato = vibratos[i].Clone();
                }
            }
        }
    }
}
