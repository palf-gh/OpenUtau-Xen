using System;
using System.Collections.Generic;
using System.Linq;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Core {
    public class SplitNotesAtTickCommand : NoteCommand {
        readonly UProject project;
        readonly int tick;
        readonly UNote[] sources;
        readonly UNote[] rightNotes;
        readonly int[] durations;
        readonly UVibrato[] vibratos;
        readonly UNote[] previousRight;
        readonly bool moving;

        public IReadOnlyList<UNote> RightNotes => rightNotes;

        public SplitNotesAtTickCommand(UProject project, UVoicePart part,
                IEnumerable<UNote> notes, int tick, string lyric) : base(part,
                    notes.Where(note => note.position < tick && tick < note.End)) {
            this.project = project;
            this.tick = tick;
            sources = Notes.Select(note => note.Clone()).ToArray();
            durations = Notes.Select(note => note.duration).ToArray();
            vibratos = Notes.Select(note => note.vibrato.Clone()).ToArray();
            previousRight = Array.Empty<UNote>();
            rightNotes = sources.Select(note => {
                // Like the knife, start a fresh note; copying the original pitch
                // curve would draw its negative-time segments before this note.
                var right = project.CreateNote(note.tone, tick, note.End - tick);
                right.pitchAddress = note.pitchAddress?.Clone();
                right.tuning = note.tuning;
                right.RefreshPitch(project, project.tracks[part.trackNo]);
                right.PhonemizerOverride = note.PhonemizerOverride;
                right.phonemeExpressions = note.phonemeExpressions.Select(exp => exp.Clone()).ToList();
                right.lyric = lyric;
                return right;
            }).ToArray();
        }

        // Continue an interactive knife cut using the same split implementation.
        public SplitNotesAtTickCommand(UProject project, UVoicePart part,
                UNote left, UNote right, UNote source, int tick) : base(part, left) {
            this.project = project;
            this.tick = tick;
            moving = true;
            sources = new[] { source };
            rightNotes = new[] { right };
            previousRight = new[] { right.Clone() };
            durations = new[] { left.duration };
            vibratos = new[] { left.vibrato.Clone() };
        }

        public override string ToString() => "Split notes";

        public override void Execute() {
            lock (Part) {
                for (int i = 0; i < Notes.Length; i++) {
                    if (moving) Part.notes.Remove(rightNotes[i]);
                    Notes[i].duration = tick - Notes[i].position;
                    rightNotes[i].position = tick;
                    rightNotes[i].duration = sources[i].End - tick;
                    SplitVibrato(sources[i], Notes[i], rightNotes[i]);
                    Part.notes.Add(rightNotes[i]);
                }
            }
        }

        void SplitVibrato(UNote source, UNote left, UNote right) {
            left.vibrato = source.vibrato.Clone();
            right.vibrato = source.vibrato.Clone();
            if (source.vibrato.HasCurves) {
                float fraction = (float)(tick - source.position) / source.duration;
                left.vibrato.rateCurve = VibratoCurve.Slice(source.vibrato.rateCurve, 0, fraction);
                left.vibrato.depthCurve = VibratoCurve.Slice(source.vibrato.depthCurve, 0, fraction);
                right.vibrato.rateCurve = VibratoCurve.Slice(source.vibrato.rateCurve, fraction, 1);
                right.vibrato.depthCurve = VibratoCurve.Slice(source.vibrato.depthCurve, fraction, 1);
            }
            float length = source.vibrato.length * source.duration / 100;
            if (length <= 0) return;
            float fadeIn = source.vibrato.@in * length / 100;
            float fadeOut = source.vibrato.@out * length / 100;
            if (length > right.duration) {
                float leftLength = length - right.duration;
                left.vibrato.length = leftLength * 100 / left.duration;
                left.vibrato.@in = Math.Clamp(fadeIn * 100 / leftLength, 0, 100);
                left.vibrato.@out = 0;
                right.vibrato.length = 100;
                right.vibrato.@in = 0;
                right.vibrato.@out = Math.Clamp(fadeOut * 100 / right.duration, 0, 100);
                double leftStartTick = Part.position + source.position + source.duration - length;
                double elapsedMs = project.timeAxis.TickPosToMsPos(Part.position + tick)
                    - project.timeAxis.TickPosToMsPos(leftStartTick);
                if (source.vibrato.period > 0) {
                    right.vibrato.shift = (float)((source.vibrato.shift
                        + elapsedMs * 100 / source.vibrato.period) % 100);
                }
                if (source.vibrato.HasCurves) {
                    right.vibrato.shift = (float)((source.vibrato.PhaseCyclesAtTick(project, Part, source, tick) * 100) % 100);
                }
            } else {
                left.vibrato.length = 0;
                right.vibrato.length = length * 100 / right.duration;
            }
        }

        public override void Unexecute() {
            lock (Part) {
                for (int i = 0; i < Notes.Length; i++) {
                    Part.notes.Remove(rightNotes[i]);
                    Notes[i].duration = durations[i];
                    Notes[i].vibrato = vibratos[i].Clone();
                    if (moving) {
                        rightNotes[i].position = previousRight[i].position;
                        rightNotes[i].duration = previousRight[i].duration;
                        rightNotes[i].vibrato = previousRight[i].vibrato.Clone();
                        Part.notes.Add(rightNotes[i]);
                    }
                }
            }
        }
    }
}
