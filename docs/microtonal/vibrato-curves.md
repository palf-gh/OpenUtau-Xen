# Editable vibrato curves

## Scope

Xen retains the existing UTAU vibrato model and adds optional per-note depth and
rate curves. These are an independent OpenUtau design, not a reproduction of
VOCALOID's synthesis or its factory vibrato presets. The supplied V4 properties
screen informed the length/depth/rate arrangement only. No vendor binary,
resource, plug-in body or reverse-engineering extract is included in this change.

## Editing

Select notes in the piano roll and open **Vibrato curves** in the note context
menu. The dialogue has length (%), depth (cents), and rate (Hz). Click a graph to
add a point, drag it to move it, or select it and use its numeric position/value
controls. Delete removes the selected point. Position is a percentage of the
whole note, including the region before vibrato begins; that region is shaded.

**Apply to selected notes** applies the detached curves and length as one undo
group. Other legacy vibrato settings remain individual to each selected note.
**Remove curves** restores each note's scalar-vibrato path without switching
vibrato off. **Reset curve** clears one lane in the dialogue; an absent lane
uses that note's legacy depth or period. Closing without applying discards edits.
The earlier UTAU-style controls remain available.

## Model and rendering

Optional `rate_curve` and `depth_curve` contain whole-note positions from 0 to 1.
Values are physical Hz (0.01–200) and cents (0–1200). Values between points are
linear and the nearest endpoint is held outside the defined range. Empty lanes
are absent. Validation rejects non-finite or out-of-range points and normalises
ordering; the last point at a duplicate position wins.

Notes with no curves execute the original vibrato equations. Curve notes share
one evaluator between waveform drawing and rendering, including the existing
length, fades, phase, drift and volume link. Variable frequency is integrated
over elapsed time, with boundaries at curve points and tempo changes, so rate
changes do not reset phase. Cloning and undo deep-copy the curves. Native
playhead splitting slices the curves and carries the accumulated phase.

`.ustxen` moves from version 3 to 4. Versions 1–3 still open without converting
their legacy vibrato. `.tunxen` stays at version 3 because its tuning data has
not changed. The application version and upstream USTX version are unchanged.

## JOB interface

The following previously unsupported note-expression calls now support `VR`
(rate) and `VD` (depth):

- `VSSeekToBeginNoteExpression(note, type)`
- `VSGetNextNoteExpression(note, type)`
- `VSGetNoteExpressionAt(note, type, posNorm)`
- `VSInsertNoteExpression(note, expression)`
- `VSRemoveNoteExpression(note, expression)`

Expression records contain `type`, `posNorm`, and `value`. Positions span the
whole note. Iteration uses stable snapshots; replacing a point at an existing
position does not produce duplicate positions. Native curves are projected
lazily, so an unchanged read or remove/reinsert cycle does not quantise existing
editable data. A failure still discards the complete JOB transaction.

The adapter uses explicit OpenUtau scales, chosen for portable copy/split
behaviour rather than vendor acoustic equivalence:

- `VR`: frequency in Hz = `5 × 2^((value − 64) / 64)`.
- `VD`: depth in cents = `value × 50 / 64`.

New JOB values must be between 0 and 127. Existing native values beyond this
projection can be read and restored unchanged, but cannot be introduced as new
out-of-range JOB edits. Get-at interpolation follows the physical curves.
Type 0 disables vibrato; type 1 enables generic OpenUtau vibrato. Factory
VOCALOID vibrato types 2–16 are not asserted compatible and remain rejected.
A neutral `bendDepth = 0` reset is accepted without deleting native pitch points.

A copied note table retaining its `objID` supplies a detached native template,
including exact Xen pitch and scalar vibrato settings. New note identities stay
distinct. Changed integer note numbers apply a physical semitone delta to the
template rather than interpreting MIDI integers as tuning degrees.
A note changed through `VSUpdateNoteEx` keeps its curves. A copied table
inserted at the same position and length (for example a note removed and
reinserted to change its lyric) keeps the source's curves, including points the
script has already changed. A copy at another time does not, because the curve
positions span the whole note: until the script inserts its own points, it reads
the scalar values as a single point at position 0.

JOB has no phase or fade fields, so a script-driven split (for example the
installed SplitNote) restarts the right note's phase and keeps the fade
percentages relative to each new vibrato length. This applies to scalar
vibrato too and predates the curves. Curve values survive only where the script
copies them; SplitNote copies the points into the half that contains them and a
start value into a right half with full-length vibrato, but adds no end value
to the left half, which therefore holds its last point. Native playhead
splitting does not have these limits.

## Legacy interchange

Ordinary UTAU `VBR` data continues to load and save through the existing path.
UST and compatible USTX cannot retain the new editable curves. For those exports,
variable pitch is sampled into ordinary pitch points and exported vibrato is
disabled, preventing a second vibrato from being applied. The open native project
is retained. Compatible USTX warns before this conversion.

Baking samples at most 2 ms apart and at least 128 times per fastest vibrato
cycle, also retaining source pitch and curve boundaries. It rejects projections
requiring more than 200,000 points in a note instead of silently dropping data.
This is a pitch approximation; renderer differences and volume-link modulation
are not guaranteed to survive legacy interchange.

An unchanged UTAU plug-in pitch reply preserves the original editable curves.
If a plug-in changes the baked pitch, the changed pitch is retained without
reapplying the original variable vibrato. The original native data can be
recovered through undo. These rules are separate from the Lua JOB interface.

## Verification

Regression coverage includes legacy equations, analytic variable-rate phase,
tempo changes, clone/undo, split behaviour, render cache changes, JOB read/write
and invalid-edit rollback, exact pitch in copied notes, native version migration,
legacy export and curve-editor interaction. The opt-in corpus test
(`JobPluginCorpusTest`) gains `OPENUTAU_XEN_JOB_VIBRATO=1`, which seeds one
curved and one scalar note and reports how far the edited vibrato departs from
the original, `OPENUTAU_XEN_JOB_FILTER` to run selected scripts, and
`OPENUTAU_XEN_JOB_SONG_TICK` to move the playhead. Verification results and
remaining limitations are recorded in `PRODUCTION.md`.
