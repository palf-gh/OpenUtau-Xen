# Split notes at the playhead

Purpose: provide a keyboard equivalent of cutting notes at the current transport
position without changing the selected editing tool.

Implementation:

- In the piano roll, unmodified `S` and Edit → Split notes at playhead split
  selected notes which strictly cross the playhead. With no selection, all
  crossing notes in the current part are split. Exact starts and ends do nothing.
- The transport position is converted from project ticks to part-local ticks;
  the cut uses the exact playhead position independently of grid snapping.
- Both the shortcut and the pointer knife use `SplitNotesAtTickCommand`. The
  right fragment starts with fresh-note portamento defaults and takes the
  configured knife-tool lyric. It retains the exact pitch address, tuning and
  expression data; source-syllable phoneme overrides and cached phoneme indexes
  are omitted. The source pitch curve stays on the left, as with the knife.
- Knife dragging updates the split through the same command, retaining the
  right fragment and recording the previous state. The whole operation is one
  undo group. Undo restores the original duration and vibrato and removes the
  fragment; redo reuses that fragment.
- Vibrato splitting shares the knife's length and fade adjustments. Phase uses
  project time, including the part offset.
- Existing Ctrl+S save, Shift+S solo and Alt+S scroll shortcuts remain available.
  This application has no general shortcut reassignment settings.
- Added the menu resource key to every Xen culture overlay. The Japanese label
  passed yomiyasu run/verify with no meaning-risk findings (run
  `20261025T230451Z-f7d9c28c`). Other overlays currently use the English fallback.

Verification: added core regression cases for exact microtonal pitch cloning,
part offsets, undo/redo, non-crossing/boundary cuts, and the shared initial-cut
and knife-drag command path. Tests compare the right fragment with fresh-note
pitch defaults and compare single-cut and dragged-cut vibrato. The integration owner
runs the serial solution build and test suite and records their results in
`PRODUCTION.md`.

Open issues: a new lyric boundary and fresh right-hand portamento mean an
acoustically identical render is not guaranteed. Interactive shortcut/menu
verification remains to be recorded by the integration owner.

User review correction: copying and translating the original pitch curve into
the right fragment left long negative-time segments visible near the source
note. The shared knife implementation now creates a fresh right-hand pitch
curve, removing the duplicated segments rather than hiding their drawing.
