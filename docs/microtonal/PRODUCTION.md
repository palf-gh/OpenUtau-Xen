# Production record

## 2026-10-24 — Environment and baseline

- Created an independent Git clone at `<working copy>`; original inspection
  checkout retained. No GitHub fork or remote publication performed.
- Upstream revision: `3eb9dd26459fa51dc2c5bf46693a9492d817c130`.
- Registered `upstream`; created `main` and `develop` branches.
- .NET SDK 10.0.102 is available (also 9.0.203).
- `dotnet test --no-restore` exited without test results, so it is not accepted
  as baseline validation. A restoring baseline test run was started.
- Parallel work allocated to tuning/Scala/presets, piano-roll/editor, and MIDI;
  primary work covers project persistence, command integration and rendering.

## 2026-10-24 — Tuning core and interchange

- Implemented exact rational/cent intervals, arbitrary periodic scales,
  configurable references, negative degrees and nearest-degree conversion.
- Added native `.tunxen`, Scala SCL/KBM round trips, MOS generation, sourced
  analytical presets and semantic notation profiles (including SZG53).
- Downloaded the original Bravura Text font and SIL OFL from Steinberg's
  Bravura repository; source details are recorded in `core.md` and asset licence.
- Baseline restoring test run completed: 279 passed, 0 failed.
- Core, MIDI, row mapper and native-project filtered tests passed after
  integration. Builds are now serial to avoid concurrent testhost file locks.
- First full integrated run: 326 passed, 4 failed; all failures were synthetic
  render-fixture notes missing ExtendedDuration. Fixed the fixture and reran all
  eight XenRenderTest cases successfully. A serial full rerun remains required.
- User supplied voice root `<voice folder>` and engines `<UTAU folder>`.
  Classic and DiffSinger assets were located. Real-engine smoke work is ongoing.

All first-release requirements remain open until independently verified.
