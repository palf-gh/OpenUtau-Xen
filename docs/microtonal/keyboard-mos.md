# Baseline MOS classification and notched keyboard previews

Date: 2026-10-25. Source: the user's supplied
`docs/general_mos_microtonal_keyboard_algorithm_latest.md` and the referenced
keyboard-tool conversation. The user first chose a tuning-specific baseline MOS,
independent of the selected performance scale. The amendment of 2026-10-25 (see
"MOS-selected keyboards and letter names" below) lets the selected MOS decide the
keyboard instead.

## Purpose and theory

`MosKeyboard` accepts equal-division count N, generator step G, cardinality M and
rotation R. It generates the distinct orbit `(i * G) mod N` for `0 <= i < M`,
sorts the orbit, and measures cyclic gaps. One gap size is an equal-step MOS;
two sizes are an ordinary MOS; three or more sizes are rejected. A generator
that repeats before M distinct pitches is rejected. Rotating the gap pattern
and accumulating from zero determines the nominal degrees.

This calculation uses degree indices rather than frequencies. The same geometry
engine therefore applies to any equave supplied by the caller. The automatic
baseline policy currently retains the known seven-note C-diatonic octave family
and nine-note C-Lambda tritave family. Their generator targets remain 3/2 and
9/7 respectively. These families select a mode on the general generator orbit;
they do not implement separate region-expansion algorithms.

Other periods and invalid family configurations retain the existing fallback
roles. The general core API also accepts custom N/G/M/R combinations, including
25 divisions and cardinalities other than seven or nine. This change adds no
serialised baseline configuration or new custom-baseline settings screen.

## Keyboard heuristic

Every nominal is a White/Natural anchor. In each large gap L, chroma `c = L - s`
places the lower nominal's sharp at `x + c` and the upper nominal's flat at
`x + s`. Coincident sharp/flat anchors are Shared Black anchors. Exact
White/Black conflicts are represented as Between/Conflict.

Between adjacent anchors A and B, an interior integer offset d is assigned to
A only when `3*d < B-A`, or to B only when `3*d > 2*(B-A)`. Both exact third
boundaries and the central interval remain Between. White-to-White interiors
always remain Between. The final interval crosses the period boundary using
the same rule. Fixed spreading radii and Black short-gap closure are removed.

Cells retain their degree, role, detailed kind, owner and source anchor.
Expanded cells do not become exact anchors or acquire an accidental spelling.
Flat and Sharp runs remain distinct, even when both have the Black colour.

For the canonical 73-EDO first large gap, degrees 0–13 are:

`White 0–1; Between 2; Flat 3–5; Between 6–7; Sharp 8–10; Between 11; White 12–13`.

Unlike the earlier algorithm, 22-EDO keeps its central Black-cluster gap Between.
53-EDO expands around its anchors using the thirds rule; its baseline counts
are 17 White, 20 Black and 16 Between. 31-EDO retains 7 White, 10 Black and
14 Between, and 13-EDT retains 9 White and 4 Black.

`KeyboardRegionizer` caches the tuning's baseline model. `PitchNotation.Role`
continues to honour explicit key-class overrides. Choosing or generating a
performance MOS changes only the selected-degree markers. Existing scale
degrees, generator-scale mode semantics, note pitches and native formats are
unchanged.

## Shapes, drawing and selection

`NotchedKeyboardLayout` consumes already classified degrees in ascending order.
It groups contiguous kinds, distinguishing Natural and Black owners, then splits each run
into `ceil(length / 5)` columns. Earlier columns receive any remainder, so seven
cells split as `[4,3]` and thirteen as `[5,4,4]`. All degrees in a left column
precede those in a right column. Degrees within a column increase bottom to top.
Known neighbouring nominal owners always have separate runs, including E–F and
the adjacent Bohlen–Pierce nominals. Only microsteps belonging to the same
nominal are eligible for the same balanced Natural column.

The settings preview opts into a shared traditional-keyboard profile for
canonical 12-EDO and 13-EDT. 12-EDO has seven white and five black keys; 13-EDT
has nine white and four black keys. White fronts have equal unit widths; black keys are centred at
the appropriate natural boundaries with width 0.6 and depth 0.62. Every nominal
is a distinct full-depth key. Logical column indices remain in degree order,
with separate horizontal position/width metadata for drawing and focus scrolling.
Explicit role overrides or other division/period combinations use the generic
layout instead. Labels are centred, with the traditional white-key labels aligned
along the front of the keyboard.

In the generic layout Natural keys have depth 1, Between/Conflict keys 2/3, and Black keys 1/3. Below
each ending depth, surviving columns divide the released horizontal interval
at their centre midpoints. The finite outside edges extend to the outer frame.
Intersecting each column's resulting polygon with its original vertical degree
slices produces the full key shapes. Layouts with no Natural keys normalise
the deepest remaining role to fill the frame.

The union of all key rectangles fills the outer frame without overlaps or holes.
`HitTest` uses those same partitions, including every notch extension. Internal
boundaries have one owner; the outer right/bottom edges belong to the adjacent
key. Selection fills and outlines the whole shape. Normal drawing preserves run
outlines and horizontal degree divisions, while omitting internal column seams,
duplicate rectangle borders and a background grid. The selected pitch is exposed
through a control automation peer and an updated accessible name.

`NotchedKeyboardPreview` uses theme brushes and `PitchLabelLayout` for
Bravura accidentals. Following the user's colour review, Black keys in settings
previews and degree maps use a dedicated black background and light lettering.
`KeyboardPreviewBlackColor` and `KeyboardPreviewBlackNameColor` are present in
light, dark and custom themes and the theme editor. Piano-roll key colours keep
their existing theme settings. The preview supports pointer selection, arrow keys, Home/End and
horizontal scrolling. Visible shapes are culled and their drawing geometries
are cached. Performance-scale edits reuse the baseline layout. A single live
preview now accompanies the combined tuning and MOS controls, with an all-white
fallback for unavailable MOS configurations and exact-frequency audition on
press/drag. See `tuning-ui.md` for validation and voice cleanup. The main
piano roll retains its vertical `PitchRowMapper` geometry and receives the new
baseline role classification through the existing shared role lookup.

## Verification and remaining checks

Automated cases cover the specified octave and tritave divisions, arbitrary
cardinalities, rotation, equal-step and invalid orbits, thirds boundaries,
period boundaries, baseline independence, explicit overrides, balanced columns,
ordered degrees, complete tiling, exact boundary ownership and notch hit areas.
The headless application check also verifies that changing performance-MOS
cardinality preserves the candidate keyboard's baseline geometry.

Set `OPENUTAU_KEYBOARD_CAPTURES` to an output directory when running the filtered
`StringsTest` to enable Skia-rendered light/dark review captures for 12-, 31-,
53- and 73-EDO and 13-EDT. The ordinary test run uses the usual headless backend.
Build/test results are recorded in `PRODUCTION.md`. Native high-DPI interaction
and physical-input review remain separate checks.

## MOS-selected keyboards and letter names

Date: 2026-10-25. Following the user's review of the live settings page, the
active generator scale now decides the keyboard. `KeyboardRegionizer.LetterScale`
returns that scale unless the keyboard is manual, the scale has more than two
step sizes counted in degrees, or it is a mode of the seven-note octave or
nine-note tritave family (generator equal to the family generator or its
inversion). Family modes keep the existing C-diatonic and C-Lambda baseline,
names and traditional 12-EDO/13-EDT profiles. Any other MOS is passed to
`MosKeyboard.FromNominals`: its degrees are the White anchors, and the same
chroma, thirds and column rules apply. This works for any period and for
unequal tunings, because the geometry counts degrees.

`PitchNotation` adds the generated `mos` profile. Nominals are lettered A–H, then
J–Z (I is skipped, as in Bohlen–Pierce), then doubled letters. Other degrees use
the existing nominal speller with the MOS chroma as sharp/flat and ups and downs
for any remainder. Automatic notation prefers `mos` over stored family names,
unless the user has authored custom names. Name input accepts the letters, and
the piano-roll notation menus list the profile. Explicit key classes still
override family baselines but not a letter-named MOS.

Manual keyboards: when every degree has an explicit key class, the keyboard is
set key by key; no MOS or family baseline applies. The settings page exposes
this as the "build the keyboard from a MOS scale" switch. Switching off copies
the current key roles into key classes and the shown names into editable custom
names; switching on clears the classes and drops unedited copied names.

Unequal tunings (2026-10-25, user review): the just-intonation, Pythagorean,
meantone and well-temperament presets start with MOS off. Their key classes come
from their chromatic names (`KeyboardRegionizer.ClassesFromRoles`), so they show
a seven-white, five-black piano keyboard; any twelve-note octave whose keys follow
the piano pattern uses the traditional profile. Unequal tunings saved before
this change are given the same classes when the settings open. `MosGenerator`
now counts step sizes in degrees rather than cents, so a fifth-generated
diatonic in just intonation or a well temperament is 5L2s. With MOS on, the
default generator is the degree nearest 3/2 (`DefaultGenerator`); a
fifth-generated seven-note scale in an unequal octave keeps the conventional
C–B names and piano keys, while other MOS cardinalities take letters.

Equal-division presets now add and select their family MOS (for example 12-EDO
C major, 13-EDT C-Lambda); divisions without a valid family MOS, such as 25-EDO,
keep all degrees. `KeyboardRegionizer.DisplayDegrees` gives the piano roll's
Highlight/Compact rows: the scale degrees plus every White and Black key, so
chromatic alterations remain visible and only in-between microsteps are left out.
Scales without a generator (custom subsets, Rast) are shown as before.
