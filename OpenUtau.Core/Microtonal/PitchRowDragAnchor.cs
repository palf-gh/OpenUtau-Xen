using System;

namespace OpenUtau.Core.Microtonal {
    /// <summary>Preserves the vertical grab position, including a note's expressive tuning offset.</summary>
    public readonly struct PitchRowDragAnchor {
        readonly double grabOffset;
        public PitchRowDragAnchor(double pointerRow, double noteRow) {
            grabOffset = pointerRow - noteRow;
        }
        public int Delta(double pointerRow, double currentNoteRow) {
            double target = Math.Round(pointerRow - grabOffset, MidpointRounding.AwayFromZero);
            double current = Math.Round(currentNoteRow, MidpointRounding.AwayFromZero);
            return checked((int)(target - current));
        }
    }
}
