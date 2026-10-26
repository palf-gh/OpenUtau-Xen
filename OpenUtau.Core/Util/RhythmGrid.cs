using System;
using System.Collections.Generic;

namespace OpenUtau.Core.Util {
    /// <summary>A rational musical grid, restarted at every bar boundary.</summary>
    public sealed class RhythmGrid {
        public int Division { get; }
        public bool Dotted { get; }
        public int Tuplet { get; }
        public RhythmGrid(int division, bool dotted = false, int tuplet = 1) {
            if (division <= 0 || tuplet < 1 || tuplet > 7) {
                throw new ArgumentOutOfRangeException();
            }
            Division = division;
            Dotted = dotted;
            Tuplet = tuplet;
        }
        public double Step(int resolution) {
            int normal = Tuplet == 2 || Tuplet == 4 ? 3 : Tuplet == 3 ? 2 : 4;
            return resolution * 4.0 / Division * (Dotted ? 1.5 : 1) *
                (Tuplet == 1 ? 1 : (double)normal / Tuplet);
        }
        public IEnumerable<int> BarTicks(int start, int end, int resolution) {
            yield return start;
            double step = Math.Max(1, Step(resolution));
            int previous = start;
            for (int i = 1; ; i++) {
                int tick = start + (int)Math.Round(i * step, MidpointRounding.AwayFromZero);
                if (tick >= end) { break; }
                if (tick > previous) { yield return tick; previous = tick; }
            }
            if (end > start) { yield return end; }
        }
        public void Bounds(TimeAxis axis, int resolution, int tick, out int left, out int right) {
            axis.TickPosToBarBeat(Math.Max(0, tick), out int bar, out _, out _);
            int start = axis.BarBeatToTickPos(bar, 0);
            int end = axis.BarBeatToTickPos(bar + 1, 0);
            left = start;
            right = end;
            foreach (int candidate in BarTicks(start, end, resolution)) {
                if (candidate > tick) { right = candidate; return; }
                left = candidate;
            }
        }
        public int Nearest(TimeAxis axis, int resolution, int tick) {
            Bounds(axis, resolution, tick, out int left, out int right);
            return tick - left <= right - tick ? left : right;
        }
    }
}
