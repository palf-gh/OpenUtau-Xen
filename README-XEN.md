# OpenUtau Xen

OpenUtau Xen is a local derivative of [OpenUtau](https://github.com/stakira/OpenUtau),
initially based on commit `3eb9dd26459fa51dc2c5bf46693a9492d817c130`.
This is a development build; the full first-release acceptance plan is tracked in
[`docs/microtonal/PLAN.md`](docs/microtonal/PLAN.md).

## Editing and files

- Use **Project → Project tuning** for the project default, or the piano-roll
  tuning menu for a track override. References, including A4 frequency, are editable.
- **Apply tuning** snaps existing notes to the new tuning. **Change reference**
  keeps degrees. **Apply display** changes only notation/scale/layout. The option
  to retain existing frequencies converts notes into degree plus residual cents.
- `.ustxen` stores complete projects with embedded definitions. `.tunxen` stores
  reusable tuning, scale, notation and keyboard presets.
- Import upstream `.ustx`, Scala `.scl` and `.kbm`. Export compatible USTX as a
  separate file. MIDI export quantises to semitones; UST carries converted pitch
  offsets in its curves. Interchange cannot preserve all Xen metadata.
- Full, scale highlight and compact layouts retain continuous physical pitch.
  Compact includes supplement rows for existing off-scale notes.
- Tuning and MIDI controls, warnings and preset labels are localised in all
  20 registered languages. The source locale identifier en-US uses British English.

## Build and tests

Requires .NET 10 and the existing upstream native runtime dependencies.

```powershell
dotnet build OpenUtau
dotnet test OpenUtau.Test
dotnet publish OpenUtau -c Release -r win-x64 --self-contained true
```

External voice tests are opt-in and need the documented local banks. Run them
serially, after ordinary tests:

```powershell
pwsh -NoProfile -File tools/verify-xen-voices.ps1 -VoiceRoot '<voice folder>' -UtauEngine '<UTAU folder>'
```

The synthesis smoke checks use the supplied Classic and DiffSinger assets
without redistributing or modifying their original banks. See
[`voice-smoke.md`](docs/microtonal/voice-smoke.md) for methodology and results.

## Verification status

At the recorded checkpoint, 363 automated cases pass. The ordinary run explicitly
skips the external-voice theory; its separate Classic, Worldline-R and DiffSinger
run passes all three cases. Their nine generated tones are within 0.515 cent of
the requested stable pitches under the documented measurement method.

Actual ENUNU, Vogen and VOICEVOX synthesis, real MIDI hardware, broader UI/high-DPI
coverage and DiffSinger automatic-pitch/retake combinations remain open. GUI
automation was stopped by the user after startup/editor inspection; subsequent
style changes were checked headlessly. See the production record for details.

## Attribution

Upstream code remains under its original MIT licence. Bravura Text is included
unmodified under SIL OFL 1.1, with the licence in `OpenUtau/Assets/Fonts/`.
The font was retrieved from Steinberg's Bravura repository. Voicebanks, external
resamplers and their licences remain separate from this repository and package.
Xen's app data, installer identity and `.ustxen` association are distinct from
upstream OpenUtau; automatic upstream updates are disabled for this derivative.
