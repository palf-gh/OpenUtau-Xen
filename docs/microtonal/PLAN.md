# OpenUtau Xen implementation plan

Status: implementation in progress. Initial upstream: stakira/OpenUtau,
`3eb9dd26459fa51dc2c5bf46693a9492d817c130` (2026-09-03).
Designed in a private planning conversation; implementation authorised 2026-10-24.

## Product and formats

OpenUtau Xen is a derivative of upstream OpenUtau.
The native project extension is `.ustxen` (UST + Xen); reusable tuning presets
use `.tunxen` (Tuning + Xen). `.ustx`, `.scl` and `.kbm` are interchange formats.
Native formats have their own identifiers and versions; upstream USTX versions
must not be repurposed. Native projects embed complete tuning definitions.
Opening an upstream project defaults to saving a separate native project.
Compatibility export must report lost metadata and rounding.

## Required behaviour

- Project tuning with optional track overrides; default 12-EDO.
- Arbitrary equal divisions, exact rational and cent intervals, static just
  intonation, historical temperaments and non-octave periods.
- Separate tuning, reference, input mapping, scale, notation and keyboard layout.
- Degree, residual cents and spelling on notes; integer MIDI remains an adapter
  for phonemisers and sample selection, never an EDO degree.
- Configurable reference frequency (including A4=440/442/432/415 Hz).
- Changing the reference retains degrees; replacing a tuning snaps to its nearest
  degree (lower on ties). Explicit preserve-frequency conversion is also offered.
- Scale, notation and layout changes never retune existing notes.
- Exact ratios survive serialisation; floating-point conversion happens only for
  pitch calculation. Existing curves retain their physical interval units.
- Full, scale highlight and compact layouts share one pitch/row mapping. Compact
  retains off-scale notes and continuous pitch curves.
- MOS generation and validation, explicit subsets and modes.
- SZG and Ups and Downs for verified mappings, numeric fallback and custom names;
  selectable primary/secondary labels. Preserve enharmonic spelling.
- Scala scale and keyboard mapping import/export, including unmapped keys.
- Windows MIDI device/channel selection, preview, recording, sustain and cleanup.
- Classic, Worldline, ENUNU, Vogen, DiffSinger and VOICEVOX receive continuous
  resolved pitch exactly once. Include pitch dependencies in all relevant caches.
- Preserve upstream licences. Separate app data/cache/update identity. First
  release is Windows x64 with manual distribution; preserve other build targets.
- Localise all Xen UI in every upstream language. Use independently maintained
  Xen resource overlays, invariant serialised identifiers and automated
  key/placeholder coverage checks. This requirement was added during implementation.

## Presets

12, 17, 19, 22, 24, 31, 41, 53 and 72 EDO; Pythagorean; representative 5- and
7-limit just scales; quarter/sixth-comma meantone; Werckmeister III, Kirnberger III,
Vallotti and Young II; 13-EDT Bohlen–Pierce; sourced regional examples.
Every preset records provenance, intervals, reference and approximation policy.
53-EDO uses C4=degree 0 and A4=degree 40; C-sharp and D-flat differ.

## Stages and verification

1. Isolated checkout, baseline build/tests and production records.
2. Pitch models/resolver, references, native formats, Scala, migration and undo.
3. Continuous pitch through every renderer and actual voice verification.
4. Shared piano-roll mapping, layouts, tuning editor, MOS, notation and presets.
5. MIDI hardware/file mapping, recording, compatibility export and recovery.
6. Combined tests, documentation and Windows distribution.

Each coherent change is tested and committed with a production record. Tested
changes integrate into `develop`; `main` follows the latest versioned commit and the
upstream fork point is tagged `upstream-base`. Publication on GitHub as a fork was
authorised by the user on 2026-10-24 (see `AGENTS.md` for the branch workflow). External engines and voices require a
recorded usable version and licence; missing dependencies remain unverified.

Acceptance covers old 12-EDO projects, 53-EDO spacing/enharmonics, 5/4, negative
degrees, period boundaries, arbitrary reference frequencies, non-octave tuning,
native/Scala round trips, undo/redo, compact geometry and curves, MIDI lifecycle,
all renderer inputs/cache invalidation/pitch reload, and actual rendered audio.
Internal calculation target: 1e-6 cent for representative inputs. Classic's
integer-cent adapter permits approximately +/-0.5 cent quantisation. F0 input
verification and measured voiced audio are distinct checks.

Out of scope: adaptive JI, timeline tuning events, MPE output and MTS-ESP.

## Completion policy

The complete first release requires every feature above, native and interchange
round trips, all six renderer families verified with actual voices, and no
12-EDO editing/rendering regression. Partial implementation is never recorded as
a completed first release.

Amendment (2026-10-24, user decision): ENUNU and Vogen actual-voice
verification and real MIDI hardware checks are waived for the first release
and recorded as unverified. All other completion requirements are unchanged.

Amendment (2026-10-24, user review of the first trial build):
- Key labels: Stein–Zimmermann–Gould where a tuning-specific table exists, then Ups
  and Downs for every octave equal division, then degree numbers. This replaces
  the earlier "no table → degree" rule. Old projects gain names at display time.
- Row layout and primary/secondary key labels are editor preferences in the
  piano-roll View menu, not tuning data. The tuning keeps its scale selection;
  the stored layout and label fields remain serialised for compatibility only.
- Key colour follows the note name (natural white, altered black), by degree index.
- The note Tone field accepts natural ± steps (53-EDO `C4+4`, `D4-4`), spelled
  names and relative steps, and sets exact degrees.
- Key colours use the general MOS engine described in `keyboard-mos.md` and the
  user's latest supplied general-MOS specification (2026-10-25). Each tuning's
  baseline MOS supplies the nominal and accidental anchors. The central third
  of an anchor interval remains Between; White-to-White interiors also remain
  Between. This supersedes the fixed dilation radius and short-gap closure in
  `docs/microtonal_keyboard_algorithm_spec.md`. Performance-scale selection only
  changes scale markers, never the baseline key roles or shapes. Equal divisions
  of the tritave retain Bohlen–Pierce Lambda names. Settings previews use
  interlocking notched shapes with at most five degrees in each balanced column;
  distinct nominal owners never share a column. Canonical 12-EDO and 13-EDT use
  traditional horizontal keyboards with seven or nine equal-width white fronts.
  Piano-roll row geometry remains in `PitchRowMapper`.
- Tuning and MOS controls share one live settings page. Parameter changes update
  the detached tuning and selected subset without a generation button. Invalid
  MOS configurations show an explicit status and an all-white preview. Degree
  colour blocks are omitted. Keyboard presses audition exact resolved pitch and
  stop on release, lost capture/focus, detachment or reconfiguration. Project
  mutation remains an explicit, undoable apply operation.
- Amendment (2026-10-25, user review of the live page): the MOS selected in the
  tuning settings now decides the keyboard, superseding "performance-scale
  selection only changes scale markers". The seven-note octave and nine-note
  tritave family MOS keep C-diatonic and C-Lambda names and keys; any other MOS
  makes its notes the white keys, named A, B, C … (I is skipped), with sharps and
  flats of its chroma. Without a MOS the preview shows white keys with degree
  numbers. A "build the keyboard from a MOS scale" switch, available for every
  tuning, turns this off; the keyboard is then set key by key. Equal-division
  presets start with their family MOS selected, and the piano roll's scale rows
  keep every white and black key, so semitones are not hidden.
  The tuning page's scale card switches between MOS and Custom scale; the scale
  menu belongs to Custom scale. Rast starts as a custom scale with its seven
  notes as white keys.
