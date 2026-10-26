# Drag creation

## 2026-10-26

Purpose: keep the modern OpenUtau workspace while adopting the requested
VOCALOID4-style drawing gesture. An isolated click on empty track space or with
the piano-roll pen must not insert a part or note.

Implementation: both gestures capture the press position and wait for at least
four horizontal device-independent pixels of movement. Creation starts at the
press position. Part drawing sets its extent from the initial and current grid
cells and supports dragging in either direction. Note drawing retains the
existing pitch, duration, overlap and audition behaviour once dragging begins.
Undo groups, pitch gestures and audition begin only after the drag threshold;
a click creates no document command or undo group.

Verification: reviewed the press/move/release call paths, including their
immediate update at the press point and their final update on release.
Solution build and the serial regression suite are recorded in PRODUCTION.md.

Open issues: native interactive pointer checks remain to be performed. The
threshold intentionally measures horizontal movement so a vertical pointer
movement cannot create a note or part accidentally.
