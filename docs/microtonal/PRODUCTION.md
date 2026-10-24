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

## 2026-10-24 — Native projects, rendering and real voices

- Added a versioned `.ustxen` envelope without changing upstream USTX versioning,
  atomic file writes, embedded tuning snapshots, autosave/recovery and detached
  compatibility export. Fine offsets are carried into UST pitch curves.
- Added note pitch bindings, reversible tuning/reference/display changes, native
  degree edits, frequency-preserving clipboard transport and cache invalidation.
- Added actual shared RenderPhrase tests and opt-in local-voice tests. Corrected
  a synthetic fixture's missing ExtendedDuration after its first full run.
- Real Classic/Worldline-R/DiffSinger runs passed for 440 Hz, adjacent 53-EDO
  pitch and 550 Hz. All nine measured stable vowels were within 0.515 cent.
  This is a smoke result for the recorded assets, not general model accuracy.
- Original voicebanks and UTAU binaries were not edited. Classic uses an isolated
  sample/oto bank; DiffSinger reads the original model with a working vocoder
  configuration. External assets are not tracked or redistributed.

## 2026-10-24 — Editor, MIDI and all-language localisation

- Added Full/Highlight/Compact row mapping, exact degree drawing/movement,
  physical-domain pitch/curve editing, off-scale supplement rows and frozen
  gesture geometry. Added tuning, scale/notation and MIDI dialogs.
- MIDI input has timestamped recording, sustain, retrigger/disconnect cleanup
  and shared KBM mapping. Real MIDI hardware remains unverified.
- User explicitly requested localisation in every upstream language. Added
  20 language overlays with 100 keys each, stable-ID selectors and translated
  preset names. Registered the pre-existing Romanian upstream resource.
- Compiled overlay coverage, formatting placeholders and runtime language
  switching passed. Headless tuning-dialog creation/geometry passed.
- Live Computer Use confirmed startup and revealed clipped numeric fields and
  excessive dialog height. Added dialog-local styles and bounded dimensions.
  User stopped Computer Use with Escape; no further GUI automation followed.
- Final serial automated suite at this checkpoint: 333 passed, 0 failed,
  1 explicitly skipped external-voice theory. Separate opt-in voice run: 3 passed.
- Verification above concerns the combined working tree; coherent commits are
  recorded separately so unfinished release requirements remain explicit.

## 2026-10-24 — Independent review corrections

- Review found that missing native envelope fields could default to an empty
  project. Native loading now requires explicit identifier, version and project;
  malformed/renamed legacy files are rejected. Seven rejection cases added.
- Review found that partial UTAU-plugin output could subtract a pitch offset
  from an already-relative curve. The parser now starts in exported coordinates
  and converts back exactly once. Six partial/full plugin regression cases added.
- Non-octave equal divisions no longer inherit octave-specific accidentals or
  the misleading EDO name. Targeted core/persistence/plugin run: 42 passed.
- Voice smoke tools now use a unique resampler staging directory, preserve
  preferences/callbacks and require the dedicated filtered test-process wrapper.
- UI review also identified dependent scale definitions after degree-count
  changes, non-zero-fine-tuning drag offsets and comma-decimal parsing. These
  corrections are tracked with the editor changes; TuningEditorReviewTest passes
  in the 363-case serial run.

## 2026-10-24 — MIDI lifecycle integration

- Captured key-down tuning snapshots, monotonic timing and common KBM mapping.
- Added sustain/retrigger/panic/disconnect handling and duplicate-frequency
  preview accounting. Native driver disposal runs outside its callback lock.
- Recording retains out-of-range pitch addresses and explicitly rejects
  unsupported preview rather than clamping musical data.
- Device-independent lifecycle/recording cases pass in the combined suite.
  Physical devices remain a separate unverified acceptance check.

## Remaining first-release checks

- Actual VOICEVOX voice/engine verification.
- ENUNU, Vogen and real MIDI hardware: accepted by the user as unverified for
  the first release (2026-10-24). No assets or device are available.
- Broader interactive editing, synthesis transitions, DiffSinger automatic
  pitch/retake, high-DPI and translated typography checks.
- Community/native-speaker review of specialist terminology is recommended.

The first release is not declared complete while these checks remain open.

## 2026-10-24 — Continuation checkpoint and commits

- Work resumed after the previous implementation session reached its usage limit.
- Serial solution build succeeded. Ordinary suite: 363 passed, 0 failed,
  1 explicitly skipped external-voice theory. Separate opt-in voice run
  (Classic, Worldline-R, DiffSinger): 3 passed.
- Removed a personal command-proxy dependency from the voice script and README
  build commands; both now call `dotnet`/`pwsh` directly.
- Failed saves now leave the document path and saved state untouched, and
  metadata-only tuning changes retain residual cents and spellings.
- The editor, MIDI dialog, localisation, app identity and tooling were committed
  as separate coherent changes and integrated into `develop`.
- Earlier DiffSinger smoke figures were partly served from the render cache.
  With the test-host cache cleared, all three engines passed again; the nine
  tones are within 0.746 cent (DiffSinger +0.600, +0.746, -0.297).
- Each intermediate commit (563caff, ce163b8) builds on its own.
- `dotnet publish -c Release -r win-x64 --self-contained` succeeded. The
  published build started with the title "OpenUtau Xen", used portable data
  beside the executable and logged no errors. The automatic upstream update
  check is not called, and the menu entry shows the manual-update notice.
- The NSIS installer was not built: it needs `vc_redist.x64.exe`, which has not
  been downloaded. makensis is installed.
- VOICEVOX is installed locally (`<VOICEVOX folder>`) but no
  OpenUtau VOICEVOX singer was found. No ENUNU or Vogen voice exists under
  `<voice folder>`, the UTAU folder or existing OpenUtau installs.
- `README-XEN.md` was merged into a single bilingual `README.md` (English and
  Japanese, with language links at the top). The Japanese text was polished
  with yomiyasu. Added `AGENTS.md` and `CLAUDE.md`; both require yomiyasu for
  documents.
- The remaining first-release checks listed below are unchanged.

## 2026-10-24 — Accepted unverified checks and local trial build

- User decision: ENUNU and Vogen synthesis and real MIDI hardware stay
  unverified for the first release. Their code paths and automated tests remain;
  only actual-voice and physical-device verification are waived.
- The published Windows x64 build was copied to the ignored folder
  `work/OpenUtau-Xen-win-x64/` for trial use and started successfully.

## 2026-10-24 — Trial feedback: labels, key colours, editor settings and Tone input

User feedback on the trial build: degree-only labels (`0/31`) were unreadable,
non-12 keyboards were all white, layout/label choices belonged to the editor,
and the Tone field could not express microtonal pitch. The tips overlay is
upstream first-launch behaviour (T or the "?" button); no code change.

- Added `PitchNotation.ResolvePrimary/ResolveSecondary/IsAccidental/FindProfile`.
  Automatic labels prefer custom names, then `szg`, then Ups and Downs; built-in
  profiles of octave equal divisions apply at display time without editing files.
- 31-EDO half-sharp/half-flat table (Fokker spelling, Stein glyphs as half
  accidentals) with enharmonic aliases. xen.wiki could not be consulted: its bot
  check blocked automated access, so a name-by-name comparison remains open.
- Twelve-note JI, fifth-chain and circulating presets gained chromatic names
  (fifth chains E♭–G♯; 5-limit JI by ratio function).
- Degree labels are now the index within the period plus a period number (`5·4`).
- Key colour is decided by degree index from the names, so A4 = 415 Hz no longer
  turns a 12-note keyboard white (previously decided by rounded MIDI pitch).
- Preferences `XenPitchRows`, `XenPrimaryNotation`, `XenSecondaryNotation` with
  View menu items; the tuning dialog no longer shows or writes these fields.
  New string `xen.notation.auto` in all 20 overlays.
- `PitchNameInput` formats and parses the note Tone field; edits use
  `SetNotePitchAddressCommand` in one undo group.
- `XenNotationTest`: 53-EDO C…D sequence, round trips for 12–72-EDO, spelled and
  relative input, 31-EDO names and key colours, unnamed snapshots, degree labels,
  12-note key colours at 440 and 415 Hz. One existing assertion changed from
  `5/53` to `5·4` deliberately.

- Full suite after this change: one clean run (379 passed, 1 skipped) and runs
  with one unrelated failure each (UstTest.EqualInLyric, TimeAxisTest). The
  previous commit shows the same intermittent failures (TimeAxisTest,
  XenProjectTest), so the cause predates this change. Resolved below.

## 2026-10-24 — Intermittent test failures

- Cause: `PhonemizerFactory.Get` (upstream code) cached factories in an unlocked
  static `Dictionary`. Every `UTrack` constructor calls it, so test classes
  creating projects in parallel corrupted the dictionary ("Operations that change
  non-concurrent collections must have exclusive access"). The app can hit the
  same race when projects or tracks are created off the UI thread.
- Fix: the cache, lookup by name and `BuildList` share one lock. Behaviour is
  otherwise unchanged (types without a phonemiser attribute are still not cached).
- Verification: before the fix, about half of full runs failed (different tests
  each time, all traced to this exception). After it, 10 consecutive full runs
  passed: 379 passed, 1 explicitly skipped external-voice theory.

## 2026-10-24 — Three-tier keyboard and Bohlen–Pierce names

User review: 31-EDO showed only the seven naturals as white and everything else as
black, unlike the Fokker organ. The user supplied
`docs/microtonal_keyboard_algorithm_spec.md` and chose to add a third key colour.

- `KeyboardTheory` derives generator, large, small and chromatic steps for the
  octave (3/2, C–B, reference 31) and tritave (9/7, Lambda C–B with H and J,
  reference 13) families; invalid theories (for example 7-EDO) fall back.
- `KeyboardRegionizer` places White and Black anchors, flags White/Black anchor
  conflicts as Between, dilates by floor(n / 2·reference), resolves equal
  distances to Between and joins sharp/flat regions across at most one step.
- `PitchNotation.Role` replaces the boolean accidental test: explicit key classes,
  then the regioniser, then note names.
- New theme colours `BetweenKeyColorLeft/Right/NameColor` in the light, dark and
  custom themes and the theme editor (Fokker blue). Between rows in the note area
  are drawn at half the black-row shade.
- `PitchNotation.BohlenPierce` names every valid EDT (13-EDT: C C♯ D E F F♯ G H H♯
  J A A♯ B, flats as aliases). The Bohlen–Pierce preset now uses them; the Tone
  field accepts H and J. View menu and all 20 overlays gained `xen.notation.bp`.
- `XenKeyboardTest` implements every required case of spec §25 (12, 22, 24, 31,
  41, 53, 72, 73-EDO; 13, 31, 53-EDT) plus fallback and explicit-class cases.
  A core test now uses a 5/2 period for its "no notation" case, since 3/1 has names.
- Full suite: 391 passed, 1 explicitly skipped. GUI appearance not yet checked.

