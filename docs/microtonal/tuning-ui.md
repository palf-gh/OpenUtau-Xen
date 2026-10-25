# Live tuning and MOS settings

Date: 2026-10-25. The user's latest trial feedback replaces the separate tuning
and scale pages with one live settings page. The v3 HTML remains a visual
reference; the interactions below supersede its manual-generation workflow.

## Layout and live editing

The first page contains the preset, period, division count, reference degree and
frequency, scale selection, MOS generator/cardinality/mode and one audible
keyboard. Reference parameters wrap into two rows at narrow widths. There are
no degree-colour blocks, duplicate keyboards, subset-generation button or manual
degree-table rebuild button. The remaining navigation pages contain display
metadata, note conversion and definitions/interchange.

Valid structure edits rebuild the detached tuning immediately. The normal
generator and cardinality follow the octave/tritave baseline until the user
edits those parameters explicitly. Editing a MOS parameter immediately selects
its generated subset and updates the keyboard markers. Intermediate generated
scales are replaced rather than accumulating with every spin-button operation;
scales present when the editor loaded remain available. Choosing an existing
scale or all degrees selects that definition directly.

These changes remain detached from the project until the final apply action.
Scale/display-only changes retain note pitches. A reference-only pitch-grid
change first retains addresses while moving the reference and then applies the
remaining detached metadata within one undo group. Conversion previews use the
same core command preview as application. Explicit interval-generated tunings,
custom subsets, notation/key-class editing, YAML and Scala/KBM/native interchange
remain available.

## Invalid MOS and keyboard display

Repeating generators and configurations with more than two physical gap sizes
show the localised MOS-unavailable status. Equal-step subsets remain valid.
Unavailable configurations show every tuning degree as a separate white key;
they do not leave the preceding generated scale selected. Applying the mother
tuning remains possible using all degrees, but no invalid MOS definition is
created. A corrected configuration restores the baseline keyboard and selected
subset immediately. An incomplete or invalid period instead clears the preview
and disables application until a valid tuning can be rebuilt.

The selected MOS now decides the keyboard (amendment of 2026-10-25, described in
`keyboard-mos.md`): family modes keep their traditional keys and names, and any
other MOS becomes the white keys, lettered A, B, C … without I. With no valid MOS
the white keys show degree numbers. Canonical 12-EDO and 13-EDT retain their traditional
horizontal profiles. The settings Black keys retain dedicated black backgrounds
and light text. `keyboard-mos.md` describes the classification and shape rules.

## Audible preview

Pointer press auditions the exact frequency of the selected detached degree;
dragging while held ends the previous frequency before starting the next.
Release, capture loss, focus loss, detachment and reconfiguration cancel the
held voice. Space/Enter audition a selected degree, and arrow selection can move
the held voice. Notch extensions use the same selection and audition hit areas.

`KeyboardAudition` resolves frequency through `PitchResolver`; no MIDI-integer
rounding is used. The default sink uses the existing `PlayTone`/`EndTone` path
and honours the editing-tone preference. Injectable sinks let headless tests
verify frequency and complete note-off behaviour without opening an audio
device. Native listening and high-DPI interaction remain separate checks.

## Verification and trial packaging

The application check exercises all four pages at normal and minimum widths,
live 12-/31-EDO and 13-EDT subset changes, immediate period/division/reference
updates, invalid-MOS white fallback, recovery and an unchanged project before
application. Real headless pointer events drive fake audio sinks to verify
press/drag/release, reconfiguration and close cleanup. Localisation coverage
includes the MOS-unavailable message in every registered language.

Set `OPENUTAU_KEYBOARD_CAPTURES` when running the filtered `StringsTest` for
Skia-backed light/dark captures, including Japanese unified settings and the
invalid-MOS state. Build/test and required local trial packaging results are
recorded in `PRODUCTION.md`.

## Scale card: MOS or custom scale

Following the user's review, the former "MOS" card and the separate scale menu
became one "Scale" card with two choices. **MOS** shows the generator, cardinality
and mode; the MOS decides the keyboard. **Custom scale** shows the scale menu
(all degrees, Rast, saved scales), the name/degrees fields for adding a scale
and the selected-key buttons White key, Black key and In-between key. Selecting a
key on the keyboard (which also auditions it) and pressing a button sets that
degree's class; the names and key classes page shows the same classes. Choosing
Custom scale keeps the current keys and names as the starting point; choosing
MOS rebuilds the keyboard from the MOS parameters. Unequal tunings and Rast start
with Custom scale; equal divisions with a family MOS start with MOS.

The display-metadata page is now called "Names and key classes", and the piano
roll's row-mode submenu "Piano-roll rows", replacing two ambiguous "Display"
labels. The settings for this change were checked in headless Skia captures of the
letter-named, manual and no-MOS states.
