# Live tuning and MOS settings

Date: 2026-10-25. The user's latest trial feedback replaces the separate tuning
and scale pages with one live settings page. The v3 HTML remains a visual
reference; the interactions below supersede its manual-generation workflow.

## Layout and live editing

The first page contains the preset, period, division count, reference degree and
frequency, the Keyboard and Scale cards (see below) and one audible keyboard. Reference parameters wrap into two rows at narrow widths. There are
no degree-colour blocks, duplicate keyboards, subset-generation button or manual
degree-table rebuild button. The remaining navigation pages contain display
metadata, note conversion and definitions/interchange.

Valid structure edits rebuild the detached tuning immediately. The normal
generator and cardinality follow the octave/tritave baseline until the user
edits those parameters explicitly. Editing a MOS parameter immediately rebuilds
the keyboard; the chosen scale is not changed. Choosing a scale only changes the
marked notes.

These changes remain detached from the project until the final apply action.
Scale/display-only changes retain note pitches. A reference-only pitch-grid
change first retains addresses while moving the reference and then applies the
remaining detached metadata within one undo group. Conversion previews use the
same core command preview as application. Explicit interval-generated tunings,
custom subsets, notation/key-class editing, YAML and Scala/KBM/native interchange
remain available.

## Invalid MOS and keyboard display

Repeating generators, repeat counts that do not divide the degree count and the
note count, and configurations with more than two physical gap sizes show the
localised MOS-unavailable status. Equal-step MOS remain valid.
Unavailable configurations show every tuning degree as a separate white key with
its degree number; the scale stays as chosen. A corrected configuration restores
the keyboard immediately. An incomplete or invalid period instead clears the
preview and disables application until a valid tuning can be rebuilt.

The keyboard MOS decides the keys (described in `keyboard-mos.md`): family modes
keep their traditional keys and names, and any other MOS becomes the white keys,
lettered A, B, C … without I. Canonical 12-EDO and 13-EDT retain their traditional
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
live 12-/31-EDO and 13-EDT keyboard MOS changes, immediate period/division/reference
updates, invalid-MOS white fallback, recovery and an unchanged project before
application. Real headless pointer events drive fake audio sinks to verify
press/drag/release, reconfiguration and close cleanup. Localisation coverage
includes the MOS-unavailable message in every registered language.

Set `OPENUTAU_KEYBOARD_CAPTURES` when running the filtered `StringsTest` for
Skia-backed light/dark captures, including Japanese unified settings and the
invalid-MOS state. Build/test and required local trial packaging results are
recorded in `PRODUCTION.md`.

## Keyboard card and scale card

Date: 2026-10-25 (supersedes the single scale card with MOS/custom-scale
choices). The tuning page has a **Keyboard** card and a **Scale** card, with the
keyboard preview between them.

- Keyboard: **MOS** shows the MOS scale list, generator, note count, repeats per
  period, mode and the
  step pattern (for example `5L2s · LLsLLLs`, or `4L4s · LsLsLsLs` for 12-EDO
  diminished with 4 repeats); the mode count is the note count divided by the
  repeats. The list groups entries under note-count headings ("7 notes"), which
  cannot be chosen. Each list entry shows the step counts, the generator in degrees and its
  cents, and the repeats when there are more than one. Choosing an entry sets
  the fields at mode 1; the default MOS keeps its own generator and mode (12-EDO
  5L2s stays generator 7, C major). Fields that give a listed MOS select it;
  others leave the list on "Custom". Improper MOS are listed only when the
  preference "Show improper MOS in the tuning MOS list" is on (off by default);
  a keyboard built from an improper MOS still works, and its step pattern ends
  with "not proper"; **Key by key** shows the selected-key buttons
  White key, Black key and In-between key. Selecting a key (which also auditions
  it) and pressing a button sets its class; the names and key classes page shows
  the same classes. MOS edits change only the keyboard.
- Scale: **Tonic** lists every degree by name; **Scale** lists all presets the
  tuning can express plus saved scales. "Click keys to add or remove notes"
  copies the current scale into a user scale on the first click and then toggles
  clicked notes; "Save scale as" names it. Scale edits change only the marks.
- Non-octave equal steps (Carlos Alpha/Beta/Gamma, 88-cent) show **Keyboard cycle
  (steps)**. Changing it keeps the step size and regroups the keyboard; as with
  any change of degree count, the tuning is rebuilt and names, scales and classes
  return to their defaults.

The piano roll's tonic button (upstream's "1=" key, now shown as "Tonic C")
names the tonic of the tuning in effect and lists every degree of that tuning. Choosing one is an undoable display change on that
tuning; for twelve-note octaves upstream's project key is set too, so
compatibility exports keep the key. Upstream's twelve-tone scale-degree display
is hidden in the View menu, because Xen rows are named by the tuning's notation.
Bravura Text draws its private-use (SMuFL) accidentals about 0.56 em tall, against
about 0.9 em for its Unicode sharp. `tools/build-xen-accidentals.py` therefore
builds Xen Accidentals (a renamed OFL Modified Version): the accidental glyphs
only, each scaled towards a common height of 780 units (between 1.3 and 1.8 times
Bravura Text, so short symbols are not cramped) and centred on the middle of a
capital letter (350 units), with side bearings, ordinary text line metrics, and ♯ ♭ ♮ mapped to the SMuFL glyphs.
Keyboards, the piano roll, the tonic list, the tonic button and its menu draw
SMuFL accidentals and ♯ ♭ ♮ in it at the letters' size on their baseline
(`PitchLabelLayout`, `PitchText`); other text uses it as an application font
fallback for the private-use range.

The display-metadata page is called "Names and key classes", and the piano
roll's row-mode submenu "Piano-roll rows". These states were checked in
headless Skia captures (letter MOS, key by key, unavailable MOS, just intonation,
Rast, AEU).
