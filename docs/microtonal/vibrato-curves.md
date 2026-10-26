# V4-style vibrato

## Scope

Xen retains the existing UTAU vibrato model and adds optional per-note depth and
rate curves, and 16 presets that approximate V4's vibrato types. The
curves are an OpenUtau design, a blend of UTAU's vibrato and V4's types
rather than a reproduction of V4's synthesis: a preset gives OpenUtau's sine
vibrato the type's measured rate and depth, and sets its shift and fades. The
supplied V4 properties screen informed the length/depth/rate arrangement. No
vendor binary, resource, plug-in body or reverse-engineering extract is in the
repository; how the presets were derived is described below.

## Editing

**Vibrato properties** opens from the note context menu, or by double-clicking a
note's vibrato handles or lines in the piano roll, and follows the layout of
V4's vibrato properties: vibrato length (%), vibrato type, and a depth
(cents) and a rate (Hz) graph. The graphs span the vibrato section, from its start
to the note end, with a tick ruler above and the values on the left; the curve is
filled down to the minimum. When a type is chosen, a dashed line marks its value
64. Click a graph to add a point, drag it to move it, or select it and use its
numeric position and value controls; right-click or Delete removes it. Choosing a
type fills both graphs with its preset. Positions are relative to the vibrato, so
changing the length keeps the shape. Points before the vibrato start do not show
and are not kept when the dialogue changes the curves.

Every change is previewed on the selected notes in the piano roll while the
dialogue is open; playback is not available until it closes. **OK** keeps the
changes as one undo step, and **Cancel**, Escape or closing the window discards
them. **Remove curves** clears the curves and type without switching vibrato off.
An empty lane uses each note's own depth or period. Other vibrato settings remain
individual to each selected note.

The piano-roll handles follow the curves. The depth handle sits at the depth
curve's peak and scales the whole curve; the period handle shows the first
cycle, from the rate at the vibrato start, and scales the whole rate curve; the
shift handle uses that period. Dragging the length handle, or changing the
length in the note properties, moves the curve points with the vibrato, as
V4's graphs do. Switching the vibrato off and on leaves the points where
they were, and a JOB plugin's `vibratoLength` change does not move them. Fade-in,
fade-out and shift do not change the curves. Notes without curves behave as before.

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

## Presets

The 16 types keep V4's numbers and order, under Xen's own family names:
1–4 Standard (V4's Normal), 5–8 Wide (Extreme), 9–12 Quick (Fast) and 13–16
Subtle (Slight), shown as "Standard 1" and so on. The family names are translated;
the numbers are the `vibratoType` values JOB plugins use. Each type has a rate
value and a depth value on V4's 0–127 scale, taken from the installed
editor's preset parameter files. Types 1–3 hold their values over the whole
vibrato. Type 4 holds them for 60% of the vibrato and then lowers both values to 0
by 90%; the editor's 57-point fade is reduced to that straight line.

Value 64 stands for the family's base rate and depth. V4 takes the
character of each family from a recorded vibrato, so the bases, the value laws, the
start phase and the onset were measured from V4's own output: user-supplied
projects with all 16 types, flat test values, several note and vibrato lengths and
two tempos, rendered with one voicebank and converted to PIT by the editor. The
research material stays outside the repository.

| Family | Rate at 64 | Depth at 64 | Start phase |
| --- | --- | --- | --- |
| Standard | 4.6685 Hz | 60.86 cents | 27.1% |
| Wide | 3.5407 Hz | 212.77 cents | 25.4% |
| Quick | 5.0798 Hz | 101.69 cents | 28.5% |
| Subtle | 4.6677 Hz | 34.93 cents | 25.7% |

Depth is linear in the value from 0. Rate is linear on each side of 64: the top
value doubles it and value 0 halves it, with the slope below 64 half the slope
above. As in V4, the top value 127 counts as 128. The same laws apply to the
JOB scale. Depth here is the amplitude of the vibrato's fundamental.

The vibrato stays OpenUtau's sine. V4's recorded vibratos are not pure
sines (they lean below the note and carry harmonics); that shape is deliberately
not copied, so a type's swing is a few per cent narrower than V4's and its
pitch differs by about 15% of the depth. The rest of a type's character is
expressed through the UTAU-style settings, which stay visible and editable:
choosing a type sets the shift to its start phase (V4 starts about a quarter
of a cycle in), the fade-in to V4's onset of 0.34 seconds (a linear build-up
whatever the tempo, note length or vibrato length; it covers the whole vibrato on
short notes) and the fade-out to 0. The fade-in is worked out from the note's
duration when the type is chosen. A flat rate of 0 produced no vibrato in
V4, while here it halves the rate. Nothing was compared by ear.

A preset is stored as `preset` (1–16) next to the curves it produced, which are
laid out over the vibrato section. Editing points keeps
the preset, because it also sets the JOB scale. UST and compatible USTX export
drop it together with the curves.

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

Value 64 is the base of the note's preset (see the table above), or 5 Hz and
50 cents for a note without one:

- `VR`: frequency in Hz = `base rate × (1 + (value − 64) / 64)` from 64 upwards and
  `base rate × (1 + (value − 64) / 128)` below 64.
- `VD`: depth in cents = `value × base depth / 64`.

New JOB values must be between 0 and 127. Existing native values beyond this
projection can be read and restored unchanged, but cannot be introduced as new
out-of-range JOB edits. Get-at interpolation follows the physical curves.
A note reports its preset as `vibratoType`, or 1 when its vibrato has no
preset, and 0 when vibrato is off. Writing back the same type changes nothing.
Choosing another type from 1 to 16 replaces the note's curves with that preset,
laid out with the `vibratoLength` written at the same time; type 0 switches
vibrato off. Types outside 0–16 are rejected. Because a note without a preset
already reports 1, a script choosing type 1 (Standard 1) leaves such a note as it
is; the Preset list in the dialogue applies that type to it.
A neutral `bendDepth = 0` reset is accepted without deleting native pitch points.

A copied note table retaining its `objID` supplies a detached native template,
including exact Xen pitch and scalar vibrato settings. New note identities stay
distinct. Changed integer note numbers apply a physical semitone delta to the
template rather than interpreting MIDI integers as tuning degrees.
A note changed through `VSUpdateNoteEx` keeps its curves. A copied table
inserted at the same position and length (for example a note removed and
reinserted to change its lyric) keeps the source's curves, including points the
script has already changed. A copy at another time does not, because the curve
positions span the whole note: a preset copy gets its preset laid out again for
its own length, and a copy without a preset reads the scalar values as a single
point at position 0 until the script inserts its own points. A copy or new note
with a different type gets that type's preset.

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
`OPENUTAU_XEN_JOB_SONG_TICK` to move the playhead, `OPENUTAU_XEN_JOB_PRESET` to give
the curved note a preset, and `OPENUTAU_XEN_JOB_DIALOG` to choose dialog values such
as VibratoTypeConversion's type. Verification results and
remaining limitations are recorded in `PRODUCTION.md`.
