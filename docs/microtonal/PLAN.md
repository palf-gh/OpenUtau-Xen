# OpenUtau Xen implementation plan

Status: implementation in progress. Initial upstream: stakira/OpenUtau,
`3eb9dd26459fa51dc2c5bf46693a9492d817c130` (2026-09-03).
Designed in a private planning conversation; implementation authorised 2026-10-24.

## Product and formats

OpenUtau Xen is a local derivative at `<working copy>`.
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

Each coherent change is tested and committed with a production record. `main`
retains the initial baseline; tested changes integrate into `develop`. No remote
publication is authorised by this plan. External engines and voices require a
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
