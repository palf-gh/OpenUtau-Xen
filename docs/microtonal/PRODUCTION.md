# Production record

## 2026-10-25 — Separate nominal keys in 12-EDO and Bohlen–Pierce previews

- User review found that adjacent White nominals, including E/F in 12-EDO and
  D/E/F in 13-EDT, were being stacked within a single generic run. Natural runs
  now retain their nominal owner, so only microsteps around the same nominal
  can share a balanced column. Explicit class-override owners use a separate
  range to avoid accidental merging with baseline nominals.
- Added an opt-in shared traditional layout for the exact canonical 12-EDO and
  13-EDT role patterns. Their seven or nine white fronts are equal-width,
  full-depth keys, with five or four black accidentals respectively. Black keys
  have width 0.6 and depth 0.62. The full-frame slab partition continues to supply
  drawing, notch selection and exact-boundary ownership. Custom role patterns
  and other periods retain the generic layout.
- Physical column position/width are separate from logical degree order.
  Settings previews use those coordinates for focus scrolling, align traditional
  white labels along the front, and centre the labels. Piano-roll text keeps
  its existing alignment. No pitch, baseline-MOS, colour or format changes.
- The targeted regression run passed 44 cases. Serial solution build and the
  Skia-backed application check succeeded. Full suite: 441 passed, one opt-in
  external-voice test skipped. Light/dark captures for 12-, 31-,
  53-, 73-EDO and 13-EDT are in ignored `work/traditional-keyboard-review/`.
  Geometry tests cover equal fronts, distinct nominals, accidental positions,
  complete tiling, notch ownership, exact boundaries and profile fallback guards.
- Native high-DPI interaction and first-release renderer checks remain open.
- Published code commit `bfc9e21` as a self-contained Windows x64 Release trial
  to `work/OpenUtau-Xen-staged-win-x64/`. The regular trial was running as process
  170032 at preflight, so its folder was not replaced and no user session was
  terminated. The staged executable matches its generated apphost; application
  and core DLLs match the ReadyToRun outputs by SHA-256. Native interactive launch
  was not checked. Replacement of the regular trial remains pending.

## 2026-10-25 — Mandatory trial packaging at task completion

- Added an always-applicable completion rule to `AGENTS.md`: refresh the existing
  self-contained Windows x64 trial, preserve local data, verify the executable
  and application DLL, record the result and link the trial executable in the
  completion report. Local packaging is authorised; remote publication is not.
- Documented staging/reporting when a running user session blocks replacement.
  This is an instruction-only change; verification consists of a diff review
  and a trial publish. The existing trial is running as process 170032 and locked
  the application DLLs, so replacement failed and a staged build was produced in
  `work/OpenUtau-Xen-staged-win-x64/`. No user session was terminated.
  Interactive launch is not part of this change's checks.

## 2026-10-25 — Updated local Windows trial executable

- Published commit `81a06312` as a self-contained Windows x64 Release build to
  `work/OpenUtau-Xen-81a06312-win-x64/`, then overlaid the published files into
  the existing `work/OpenUtau-Xen-win-x64/` trial folder without deleting its
  local data. This includes the baseline-MOS algorithm, notched previews and
  black settings keys. No public release or remote upload was performed.
- Compared SHA-256 hashes of the published files against the trial copies.
  Native interactive launch remains a separate check.

## 2026-10-25 — Baseline MOS regions and notched settings keyboards

- Implemented the user's latest general-MOS keyboard specification. A general
  N/G/M/R orbit engine produces equal-step or two-gap MOS nominals, accidental
  anchors, owners and detailed cell kinds. Region allocation uses exact integer
  central-third comparisons, leaves White-to-White interiors Between and removes
  the former fixed dilation radius and Black short-gap closure.
- Applied the user's explicit choice of tuning-specific baseline MOS. Octave
  C-diatonic and tritave C-Lambda presets feed the same engine; active performance
  scales change degree markers without changing baseline roles or shapes.
  Existing explicit key classes still take precedence. The piano roll receives
  the updated roles through the shared lookup; its row mapper is unchanged.
- Replaced all three settings key lists with interlocking horizontal previews.
  Runs use balanced columns of at most five degrees, preserving left-to-right
  and bottom-to-top degree order. Notch extensions tile the full frame and share
  the exact hit-test ownership used for selection. Drawing retains run outlines
  and horizontal degree divisions while omitting internal same-run column seams.
  Selected pitches are exposed through an automation peer and accessible name.
- Following the user's colour review, Black keys in settings previews and degree
  maps now use a black background and light text. Added dedicated colours to
  light, dark and custom themes, custom-theme persistence and the theme editor.
  Recorded this settings-specific policy in `DESIGN.md`; Japanese additions
  completed yomiyasu run/verify and manual diff review.
- Serial solution build succeeded. Full suite: 432 passed and one external-voice
  case skipped. After the colour change, the Skia-backed application check passed
  again. Light/dark captures cover 12-, 31-, 53-, 73-EDO and 13-EDT. Tests cover
  third boundaries, cyclic anchors, arbitrary cardinalities, invalid orbits,
  exact hit boundaries, complete tiling, Black owners, run identity, baseline
  stability, notch selection, accessibility and the small-window page layout.
- Existing compiler, NuGet advisory and test-runner warnings remain. Native
  high-DPI/physical-input review and first-release renderer checks remain open.
  No voicebanks, engines, note-pitch data or native format versions were changed.
  See `keyboard-mos.md` for baseline/fallback scope and capture instructions.

## 2026-10-25 — Tuning and MOS editor refresh

- Used the user's v3 HTML mock as the layout reference. Replaced the three long
  tabs with five left navigation pages and a fixed scope/action area. Grouped
  tuning structure, selected degrees, generator candidates, conversion and
  interchange operations. Details and remaining checks are in `tuning-ui.md`.
- Added shared-role degree maps and virtualised vertical key previews, with
  independent scale markers and Bravura Text accidentals. The MOS controls have
  live candidate previews, cardinality bounds, one-based mode numbers and
  explicit generation. Repeating generators clear the candidate preview.
- Scale/display application preserves note pitch when the applied mother tuning
  matches. A changed mother tuning keeps the explicit tuning-application action;
  an incompatible display-only operation is rejected before any undo command.
- Kept interval-generated tunings separate from EDO subsets and gave them an
  independent pitch count. Kept piano-roll geometry and View-menu preferences
  in their existing shared implementation. No file formats were changed.
- Updated every Xen language overlay. Japanese resource changes passed the
  yomiyasu run/verify checks.
- Serial solution build succeeded. Full suite: 397 passed. The headless UI
  check covers five pages at 960 × 620 and 650 × 440, 31-EDO and 13-EDT,
  generated subset membership, repeating generators and application labels.
- Temporarily used Skia rendering in the headless harness to inspect Japanese
  light/dark previews, then restored the ordinary harness. Corrected missing
  music glyphs and initially expanded degree lists found during that inspection.
  Captures are local, ignored review artefacts under `work/`.
- Existing NuGet dependency-advisory and test-runner resolution warnings remain.
  Native high-DPI interaction and specialist translation review remain open;
  first-release renderer/hardware checks are unchanged.

## 2026-10-24 — Environment and baseline

- Created an independent local Git clone; original inspection
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
- User supplied a local voicebank folder and a UTAU engine folder.
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
- VOICEVOX is installed locally but no
  OpenUtau VOICEVOX singer was found. No ENUNU or Vogen voice exists under
  the supplied voicebank folder, the UTAU folder or existing OpenUtau installs.
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

## 2026-10-24 — Fork design and default theme colours

User request: base the fork's default theme on the palf brand (palf_strage
DESIGN.md) without breaking OpenUtau's look, documented in a dedicated DESIGN.md.

- Added `DESIGN.md` (Japanese, checked with yomiyasu). Upstream layout, pink
  keyboard and blue notes stay. Taken from the brand: canvas `#F2F2EF` and ink
  `#181818` (light), ink `#F2F2F2` (dark), and lime `#CBE960` with `#101010` text,
  reserved for Xen-specific elements. Brand blue is not used, to avoid clashing
  with upstream's note blue.
- Light and custom-theme defaults use the canvas and ink; dark keeps upstream's
  background. Between keys changed from the provisional Fokker blue to lime.
- Full suite 391 passed, 1 skipped. Trial build republished and started without
  new errors. Visual review by the user is pending.

- Revised on user review: upstream pink is OpenUtau's main colour, so it is now
  replaced by brand lime (fills `#CBE960` with `#101010` text; UI-adjusted
  `#6B8F12`/`#4F6B0B` where white text sits on it), and upstream light blue by a
  UI-adjusted deep blue `#3344D6` (white-text contrast 7.15:1) derived from brand
  `#0000FF`. Between keys use the sub blue. Purple-tinted greys became neutral.
  DESIGN.md rewritten and checked with yomiyasu. 391 passed, 1 skipped.

## 2026-10-24 — Pre-publication clean-up

- Removed local paths, the planning-conversation identifier and voicebank names
  from tracked documents. The real-voice test now takes the Classic bank,
  DiffSinger configuration, DiffSinger vocoder and UTAU engine folder as explicit
  settings (`OPENUTAU_XEN_CLASSIC_BANK`, `OPENUTAU_XEN_DIFFSINGER_BANK`,
  `OPENUTAU_XEN_DIFFSINGER_VOCODER`, `OPENUTAU_XEN_UTAU_ENGINE`); no default
  install path remains. Rerun with the same local assets and an empty cache: 3 passed.
- Removed upstream-only scheduled and maintainer workflows (Crowdin sync, stale
  issues, release clean-up, team auto-merge). The PR test now targets this fork's
  branches; manual build workflows remain and need Xen naming before releases.
- The executable and assembly are now `OpenUtau-Xen` (root namespace unchanged;
  Xen resource URIs follow the assembly name). The installer, macOS bundle
  executable and shortcuts use the new name.
- Version set to `0.1.0` (first public pre-release); versioning rules added to
  `AGENTS.md`.

## 2026-10-24 — Publication and version 0.1.0

- With the user's authorisation, forked upstream as `palf-gh/OpenUtau-Xen` (public)
  and pushed `main` and `develop`. Upstream tags and local backup branches were not
  pushed.
- Workflow for collaborators: `develop` is the default branch; work arrives by pull
  request from `feature/` or `fix/` branches; `main` fast-forwards to each versioned
  commit; `upstream-base` tags the fork point. Upstream's `master` was deleted from
  the fork. Versions are bumped during development when `develop` is worth sharing.
- Version 0.1.0 contains: exact tunings, presets, Scala interchange and native
  `.ustxen`/`.tunxen` formats; continuous pitch for every renderer (Classic,
  Worldline-R and DiffSinger verified with real voices); Full/Highlight/Compact rows;
  SZG, Ups and Downs and Bohlen–Pierce names; the three-tier keyboard; microtonal
  Tone input; MIDI input (no hardware verification); 20-language Xen UI; the palf
  palette. Still open: VOICEVOX verification, broad GUI and high-DPI review.
- README gained a bilingual "Contributing" section; the Japanese text was checked
  with yomiyasu.


## 2026-10-25 — Workspace layout from OpenUtau Lunai

- The welcome screen, docked workspace, top bar, piano-roll side toolbar and
  note-properties dock, portrait across the cards and pitch follow are reimplemented
  from OpenUtau Lunai at commit `01be2980269b01fb2d3f90e311d1132e91588768` (MIT).
  Lunai's commits are not cherry-picked. Details per step are in `ui.md`.
- `DESIGN.md` now takes Lunai's workspace as the layout reference; colours stay as
  before. Controls use a 6px corner radius. The piano roll is docked by default for
  new preferences; existing preferences keep their choice.
- Left out: Singer Hub, DiffSinger quality presets and panels, key/scale overlay,
  harmony, live pitch, Lunai's appearance and theme-editor docks, ThemeHub, track
  colours and Original themes, TikTok mode, toasts, overlay scrollbars and the
  separate phoneme card.
- Verification: build and 397 tests pass (one opt-in voice test skipped); the
  welcome screen and workspace were rendered headlessly in light and dark themes.
  Not verified: pointer dragging of the splitters, detaching and re-attaching with a
  real window manager, pitch follow during real playback, a real singer portrait,
  high DPI, macOS and Linux.

## 2026-10-25 — Version 0.2.0

- Version 0.2.0 adds the workspace layout from OpenUtau Lunai: card-based welcome
  screen, docked piano roll with an expand-in-window mode, top bar with transport,
  BPM, time signature and project tuning, piano-roll side toolbar, resizable
  note-properties dock, portrait across the cards, render progress strip with a
  stop chip, and pitch follow that works in every tuning.
- The piano roll is docked by default for new preferences; existing preferences keep
  their choice (**View > Detach piano roll** switches it).
- Still open: the checks listed in the workspace entry above, VOICEVOX verification,
  broad GUI and high-DPI review.

## 2026-10-25 — Live tuning and MOS settings with audible keyboard

- Purpose: the user's trial feedback asked for live updates without a generation
  button, an explicit "cannot generate a MOS scale" state drawn with white keys
  only, one combined tuning/MOS page, no separate degree-colour blocks, and sound
  when a keyboard key is pressed.
- Implementation: `TuningDialog` merges the tuning and scale pages into one live
  page (four navigation pages remain). Period, division, reference and MOS edits
  rebuild the detached draft immediately; invalid MOS configurations show
  `xen.status.mosUnavailable` (all 20 languages) and an all-white keyboard.
  `NotchedKeyboardPreview` auditions the exact `PitchResolver` frequency through
  the new `KeyboardAudition` state machine on press, drag, Space/Enter, and stops
  on release, capture/focus loss, detachment and reconfiguration. The default
  sink is `PlaybackManager.PlayTone`, so the piano roll's editing-tone toggle
  also mutes the audition. Details are in `tuning-ui.md`.
- Verification: build passes; 451 tests pass (one opt-in voice test skipped),
  including `KeyboardAuditionTest` and headless pointer-driven audition checks.
  Skia captures of the unified page and the invalid-MOS state were inspected in
  light and dark themes (Japanese). The ja-JP string was checked with yomiyasu.
- Open: native listening, interactive launch, high-DPI review and responsiveness
  of live edits at very large EDOs were not checked.
- Trial build: `work/OpenUtau-Xen-win-x64/` refreshed with the self-contained
  win-x64 publish (no trial was running). `OpenUtau-Xen.exe` matches the new
  `bin` output and `OpenUtau-Xen.dll` matches the new ReadyToRun output (both
  17:10:16 local time). Not launched interactively.

## 2026-10-25 — MOS-decided keyboards, letter names and manual keys

- Purpose: the user asked for nominal names that follow the MOS cardinality
  (A, B, C … without I) on white keys, a MOS on/off switch for every tuning with
  key-by-key settings when off, semitones in the piano roll's scale rows, a
  clearer label than "Display" in the View menu, and presets that start with a
  proper MOS.
- Implementation: `KeyboardRegionizer.LetterScale`/`IsManual`/`DisplayDegrees`,
  `MosKeyboard.FromNominals`, the generated `mos` notation profile and name input,
  the dialog switch with White/Black/In-between buttons, family MOS in
  equal-division presets, and the "Piano-roll rows" and "Names and key classes"
  labels (all 20 languages). Details are in `keyboard-mos.md` and `tuning-ui.md`.
- Verification: build passes; 465 tests pass (one opt-in voice test skipped),
  including `MosLetterNotationTest` and dialog checks of letters, the switch,
  manual key classes and copied names. Skia captures of the letter, manual and
  no-MOS states were inspected. The Japanese strings were checked with yomiyasu.
- Trial build: `work/OpenUtau-Xen-win-x64/` refreshed (no trial running);
  `OpenUtau-Xen.exe` and the ReadyToRun `OpenUtau-Xen.dll` match the new output
  (17:38:45 local time). Not launched interactively.
- Open: the piano roll with letter names and chromatic rows was not checked in a
  running app; unequal tunings with MOS switched on use degree-counted steps.

## 2026-10-25 — Keyboards for unequal tunings

- Purpose: the user found that the just-intonation and Pythagorean presets opened
  in MOS mode with generator 1 and could not generate a scale, and asked that
  unequal tunings get a proper keyboard.
- Implementation: unequal presets start with MOS off, with key classes taken from
  their chromatic names; older unequal tunings receive the same classes when the
  settings open. Twelve-note octaves in the piano pattern use the traditional
  keyboard. MOS step sizes are counted in degrees, the default generator is the
  degree nearest 3/2, and a fifth-generated diatonic keeps C–B names. Details are
  in `keyboard-mos.md`.
- Verification: build passes; 474 tests pass (one opt-in voice test skipped),
  including every unequal preset (piano keys and names with MOS off, 5L2s
  C major and a 5-note MOS with MOS on). Skia captures of 5-limit just intonation
  with MOS off and on were inspected.
- Trial build: `work/OpenUtau-Xen-win-x64/` refreshed (no trial running);
  `OpenUtau-Xen.exe` and the ReadyToRun `OpenUtau-Xen.dll` match the new output
  (17:51:07 local time). Not launched interactively.

## 2026-10-25 — Scale card and Rast keyboard

- Purpose: the user chose to start Rast with MOS off and asked for the "MOS"
  section to become "Scale", switching between MOS and Custom scale, which also
  answers what the separate scale menu was for.
- Implementation: Rast's seven notes are white keys, the other even (twelve-tone)
  degrees black and the remaining quarter tones in-between keys. The tuning page
  has one Scale card with MOS/Custom scale choices; the scale menu, custom-scale
  fields and key-class buttons belong to Custom scale. The "build the keyboard
  from a MOS scale" string was removed from all 20 languages.
- Verification: build passes; 475 tests pass (one opt-in voice test skipped),
  including the Rast keyboard and the dialog switch between MOS and Custom scale.
  Skia captures of both choices were inspected.
- Trial build: `work/OpenUtau-Xen-win-x64/` refreshed (no trial running);
  `OpenUtau-Xen.exe` and the ReadyToRun `OpenUtau-Xen.dll` match the new output
  (18:01:08 local time). Not launched interactively.

## 2026-10-25 — Separate keyboard and scale, tonic, more presets

- Purpose: the user asked to add the listed tuning presets (with their own note
  names where a tradition has them), to give non-octave tunings a usable keyboard,
  to keep Rast as a preset without it turning into plain 24-EDO, and to separate
  the keyboard (MOS or key by key) from the scale (tonic and notes) so presets such
  as 12-EDO pentatonic exist and upstream's "1=C" key is unified with the tonic.
- Implementation: `KeyboardLayout` gains `layout`, `mos` and `tonic`;
  `.tunxen`/`.ustxen` move to version 2 with a version-1 upgrade.
  `ScalePresets` provides scales per tuning. The dialog has Keyboard and Scale
  cards, scale editing by key clicks, a keyboard cycle for non-octave equal steps
  and an Id-first preset choice. The piano roll's "1=" menu sets the tonic, and
  the degree display labels scale rows (in the left slot on single-line rows).
  Projects saved with upstream's key (version-1 `.ustxen` and plain `.ustx`)
  carry `key` into the tonic of twelve-note octave tunings. New presets: 5, 7, 10, 15, 16, 26, 34 and
  46-EDO, third/two-sevenths-comma meantone, Ptolemy, harmonic series 8–16, just
  Bohlen–Pierce, 22 śruti, Partch 43, Turkish AEU (perde names), slendro and pelog
  (Javanese names), Carlos Alpha/Beta/Gamma and 88-cent steps, with sources.
  Strings in all 20 languages; details in `keyboard-mos.md` and `tuning-ui.md`.
- Superseded: the active generator scale no longer decides the keyboard; presets
  no longer select a family scale; Rast is no longer a manual keyboard; piano-roll
  scale rows no longer add white and black keys.
- Verification: build passes; 484 tests pass (one opt-in voice test skipped),
  including keyboard/scale independence, scale presets, format round trip and
  version-1 upgrade for both formats, the upstream key as tonic, the new presets and dialog checks of the
  keyboard cycle, key-by-key editing and key-click scale editing. Skia captures of
  Rast and AEU were inspected. The Japanese strings were checked with yomiyasu.
- Open: the piano-roll "1=" menu and degree labels were not checked in a running
  app; perde names are long for narrow black keys; gamelan and śruti presets are
  documented approximations/conventions.
- Trial build: `work/OpenUtau-Xen-win-x64/` refreshed (no trial running);
  `OpenUtau-Xen.exe` and the ReadyToRun `OpenUtau-Xen.dll` match the new output
  (19:14:04 local time). Not launched interactively.

## 2026-10-25 — Default MOS per equal division, tonic button, accidental font

- Purpose: the user found that 15-EDO could not build a MOS, asked whether
  16-EDO's MOS was right and that equal divisions get an appropriate MOS, that
  just Bohlen–Pierce show black keys, that the tonic list show its accidentals,
  that "1=" become a tonic setting and that the twelve-tone degree display go.
- Implementation: `KeyboardRegionizer.DefaultMos` searches a MOS for equal
  divisions without a family (15-EDO porcupine 1L6s, 16-EDO mavila 2L5s, 10-EDO
  3L4s; up to nine degrees all keys are white) and the dialog shows it. Unequal
  presets derive their keys after their names are added (just Bohlen–Pierce now
  has nine white and four black keys). Bravura Text is an application font
  fallback for SMuFL characters. The piano-roll button reads "Tonic C" and lists
  note names; the View menu's degree display is hidden and its row labels removed.
- Verification: build passes; 489 tests pass (one opt-in voice test skipped),
  including default MOS for 15/16/10-EDO, small divisions and just Bohlen–Pierce
  black keys. A Skia capture shows C half-sharp, C sharp and D flat in a text
  label and a closed list.
- Open: the tonic button and its menu were not checked in a running app.
- Trial build: `work/OpenUtau-Xen-win-x64/` refreshed (no trial running);
  `OpenUtau-Xen.exe` and the ReadyToRun `OpenUtau-Xen.dll` match the new output
  (19:41:29 local time). Not launched interactively.

## 2026-10-25 — Readable accidentals and a full tonic menu

- Purpose: the user found SMuFL accidentals too small and asked that the piano
  roll's tonic menu list every degree of the tuning.
- Implementation: measured in BravuraText.otf, U+E262 is 0.56 em tall against
  0.91 em for U+266F, so `PitchLabelLayout` draws SMuFL runs at 1.4 times the
  letter size. The new `PitchText` control draws pitch names on the letters'
  baseline with the letters' height; the tonic list, tonic button and tonic menu
  use it. The tonic menu lists every degree. A modified Bravura was not made,
  since its OFL reserves the font name.
- Verification: build passes; 489 tests pass (one opt-in voice test skipped).
  Skia captures show the enlarged accidentals in a text line, a closed list and
  the 31- and 53-EDO keyboards.
- Trial build: `work/OpenUtau-Xen-win-x64/` refreshed (no trial running);
  `OpenUtau-Xen.exe` and the ReadyToRun `OpenUtau-Xen.dll` match the new output
  (20:00:39 local time). Not launched interactively.

## 2026-10-25 — Xen Accidentals font and C-based letters

- Purpose: the user asked for a renamed derived font whose accidentals sit on the
  baseline at a proper size, for ♯ and ♭ to use it too, and for MOS keyboards to
  start at C.
- Implementation: `tools/build-xen-accidentals.py` builds
  `OpenUtau/Assets/Fonts/XenAccidentals.otf` (about 91 KB) from Bravura Text,
  now kept unmodified in `tools/fonts/` and no longer shipped: SMuFL accidental
  glyphs U+E260–U+E4FF only, scaled 1.4, flat bowl on the baseline, 60-unit side
  bearings, ascent 900 / descent −250, U+266D–U+266F mapped to the SMuFL glyphs,
  names "Xen Accidentals" / `XenAccidentals-Regular`. The OFL with a modification
  notice is `XenAccidentals-OFL.txt`; README and DESIGN.md are updated (Japanese
  checked with yomiyasu). `PitchLabelLayout`, `PitchText` and the font fallback use
  it, and ♯ ♭ ♮ are drawn in it. Letter-named MOS use the first n letters rotated
  so degree 0 is C (C D E F G A B; nine notes C D E F G H J A B).
- Verification: build passes; 489 tests pass (one opt-in voice test skipped).
  Skia captures show C, C half-sharp, C sharp and D flat on the baseline in text,
  a closed list and the 31- and 53-EDO keyboards.
- Trial build: the trial in `work/OpenUtau-Xen-win-x64/` was running, so the new
  build was published to `work/OpenUtau-Xen-staged-win-x64/` instead; its
  `OpenUtau-Xen.exe` and ReadyToRun `OpenUtau-Xen.dll` match the new output
  (20:10:23 local time). The running trial folder was left unchanged; replacing
  it is pending until that session is closed. Not launched interactively.
- Trial replacement: after the user closed the trial, `work/OpenUtau-Xen-win-x64/`
  was refreshed; its `OpenUtau-Xen.exe` and ReadyToRun `OpenUtau-Xen.dll` match
  the new output and the staged build (20:10:23). Not launched interactively.

## 2026-10-25 — Accidentals centred on the letters

- Purpose: the user found the accidentals small and the sharp and half-sharp
  lower than the letters, and asked for each symbol to be centred on the letter
  height at a size where none looks cramped (not mechanical baseline alignment).
- Implementation: `tools/build-xen-accidentals.py` scales each glyph towards a
  common height of 780 units (limited to 1.3–1.8 times Bravura Text) and centres
  it on 350 units, the middle of a typical capital. Sharp, flat, natural and the
  Stein–Zimmermann accidentals now span −40 to 740 units.
- Verification: build passes; 489 tests pass (one opt-in voice test skipped).
  Skia captures show C, C half-sharp, C sharp, D flat and the 24-EDO names at one
  height in text, a closed list and the 53-EDO keyboard.
- Trial build: `work/OpenUtau-Xen-win-x64/` refreshed (no trial running);
  `OpenUtau-Xen.exe` and the ReadyToRun `OpenUtau-Xen.dll` match the new output
  (20:21:06 local time). Not launched interactively.

## 2026-10-25 — 15-EDO default keyboard

- Purpose: the user noted that porcupine (generator 2) leaves 15-EDO with few
  black keys and suggested generator 11 (4 from the other side).
- Implementation: the default-MOS search also scores the share of in-between keys
  (each large step holds a sharp and a flat, one key when they coincide). 15-EDO
  now uses 4L3s (generator 4): seven white and eight black keys, no in-between
  keys. 16-EDO (mavila 2L5s) and 10-EDO (3L4s) are unchanged.
- Verification: build passes; 489 tests pass (one opt-in voice test skipped),
  including 15-EDO 4L3s without in-between keys.
- Trial build: the trial was running, so the build was published to
  `work/OpenUtau-Xen-staged-win-x64/`; its `OpenUtau-Xen.exe` and ReadyToRun
  `OpenUtau-Xen.dll` match the new output (20:28:50). The trial folder still holds
  the 20:21 build (only the running app's `prefs.json` changed); replacement is
  pending. Not launched interactively.

## 2026-10-25 — Accidentals in small steps

- Purpose: the user pointed out that porcupine (generator 2) in 15-EDO should be
  able to make every other key black, and asked to adjust keyboard generation
  rather than the default generator.
- Implementation: `MosKeyboard` places the lower nominal's sharp and the upper
  nominal's flat in every step wider than the chroma, not only in large steps.
  This adds E-sharp/F-flat and B-sharp/C-flat black keys in 19-, 26- and 31-EDO
  (as on Fokker-style keyboards); 12, 17, 22, 24, 41, 53, 72-EDO and 13-EDT are
  unchanged. The default-MOS search counts black keys the same way, so 15-EDO
  returns to porcupine 1L6s, now with seven white and eight black keys.
- Verification: build passes; 490 tests pass (one opt-in voice test skipped),
  including the 31-EDO counts (7/14/10), 19-EDO E–F and 15-EDO without in-between
  keys. A Skia capture of 31-EDO shows the new E–F and B–C black keys.
- Trial build: `work/OpenUtau-Xen-win-x64/` refreshed (no trial running);
  `OpenUtau-Xen.exe` and the ReadyToRun `OpenUtau-Xen.dll` match the new output
  (20:37:19 local time), superseding the pending staged build. Not launched
  interactively.
- Correction (user review): 31-EDO's E–F and B–C should stay in-between keys.
  Small steps now hold accidentals only when the sharp is not above the flat
  (`2c <= s`, `MosKeyboard.HasAccidentals`): 19-EDO and porcupine 15-EDO keep one
  shared black key per small step, 26-EDO gets separate E-sharp/F-flat keys, and
  31-EDO returns to 7 White, 10 Black and 14 Between. 490 tests pass; a Skia
  capture confirms the 31-EDO keyboard.
- Trial build: `work/OpenUtau-Xen-win-x64/` refreshed (no trial running);
  `OpenUtau-Xen.exe` and the ReadyToRun `OpenUtau-Xen.dll` match the new output
  (22:16:14 local time). Not launched interactively.

## 2026-10-25 — Japanese scale presets

- Purpose: the user asked for Japanese scales ("日本和声") and chose the common
  set of Japanese-style scales.
- Implementation: `ScalePresets` adds yonanuki major (C D E G A), yonanuki minor
  (C D E♭ G A♭), yō (C D F G A), in (C D♭ F G A♭), hirajōshi (C D E♭ G A♭), kumoi
  (C D E♭ G A) and iwato (C D♭ F G♭ B♭), spelled by fifths so they also map to
  other fifth-generated tunings. Yonanuki major shares notes with major
  pentatonic, yō with ritsu, in with miyako-bushi, and hirajōshi with yonanuki
  minor; each is still listed under its own name. Names are localised in all 20
  languages (Japanese checked with yomiyasu).
- Verification: build passes; 497 tests pass (one opt-in voice test skipped),
  including the seven scales in 12-EDO and their presence in 31-EDO.
- Trial build: `work/OpenUtau-Xen-win-x64/` refreshed (no trial running);
  `OpenUtau-Xen.exe` and the ReadyToRun `OpenUtau-Xen.dll` match the new output
  (22:23:22 local time). Not launched interactively.

## 2026-10-25 — Merged scale entries, window titles, remaining localisation

- Purpose: the user asked that presets with the same notes be listed once with
  their names side by side, that the tuning window title be just "Tuning
  settings" like other windows, and that remaining English items be localised.
- Implementation: `ScalePresets.For` merges presets with identical degrees into
  one entry whose identifier joins their keys (for example
  `preset.major-pentatonic+yonanuki-major`); `Find` still resolves any single key,
  so earlier choices keep working. The dialog labels merged entries with all
  localised names. The tuning and MIDI input titles drop "— OpenUtau Xen".
  Generated equal-division names ("12-EDO", "13-ED (3/2)"), Werckmeister III,
  Kirnberger III, Vallotti, Young II, 13-EDT Bohlen–Pierce and the Ups and Downs,
  Stein–Zimmermann–Gould and Bohlen–Pierce notation names are localised in all
  20 languages; names the user typed are kept. Japanese checked with yomiyasu.
- Verification: build passes; 498 tests pass (one opt-in voice test skipped),
  including merged 12-/24-EDO entries, resolving a single key and the Japanese
  preset names.
- Trial build: `work/OpenUtau-Xen-win-x64/` refreshed (no trial running);
  `OpenUtau-Xen.exe` and the ReadyToRun `OpenUtau-Xen.dll` match the new output
  (22:41:28 local time). Not launched interactively.

## 2026-10-25 — Translate untranslated upstream strings in every language

- Purpose: the user noticed English left in the interface outside the tuning
  features, such as the parts ported from OpenUtau Lunai, and asked for all of
  it to be translated in all 20 languages.
- Implementation: the English items come from upstream Crowdin dictionaries that
  lack keys or keep English text. Without editing those files, the new
  `Strings/XenUpstream.<culture>.axaml` overlays (19 languages, 13 to 718 keys
  each) are merged after the upstream and Xen dictionaries by
  `XenStrings.SetLanguage`. `tools/xen-upstream-strings.py` lists untranslated
  keys and builds the overlays from `tools/xen-upstream-strings/<culture>.json`,
  keeping only keys upstream still leaves untranslated. Details in
  `localisation.md`. The Japanese strings were checked with yomiyasu.
- Verification: build passes; 498 tests pass (one opt-in voice test skipped).
  The strings test checks that every overlay key exists upstream with the same
  placeholders and line count, keeps any leading or trailing space the English
  has, and that Japanese and Korean resolve overlay strings at run time (with the
  Korean tool tip's line breaks and the singer-default prefix's trailing space).
  After the build, `missing` reports no untranslated key in any language.
- Open: translations other than Japanese were not reviewed by native speakers;
  the translated menus were not checked in a running app.
- Trial build: `work/OpenUtau-Xen-win-x64/` refreshed (no trial running);
  `OpenUtau-Xen.exe` and the ReadyToRun `OpenUtau-Xen.dll` match the new output
  (23:23:09 local time) and contain all 19 overlays. Not launched interactively.

## 2026-10-25 — Release 0.2.0

- Purpose: the user asked to complete 0.2.0, push it and publish a release for
  every platform, to make `main` GitHub's default branch, and to release every
  future version bump.
- Version 0.2.0 contains the Lunai workspace layout (see "Version 0.2.0" above)
  and the tuning work since: live tuning and MOS settings with an audible
  keyboard, separate keyboard and scale settings with a tonic, many tuning and
  scale presets (including Japanese scales), baseline MOS keyboards, the Xen
  Accidentals font, and translations of the upstream strings left in English in
  19 languages.
- Release preparation: the in-app updater now reads this repository's releases
  instead of stakira/OpenUtau's, so Xen can no longer offer upstream OpenUtau as
  an update; the bug report menu opens this repository's issues; the AppImage
  launcher starts `OpenUtau-Xen`; `appcast.py` points downloads here. The
  `build.yml` release workflow uses `vX.Y.Z` tags, "OpenUtau Xen" release names,
  `OpenUtau-Xen-<platform>` assets, finds the macOS `.app` by pattern and has a
  Xen release text; `next-version.py` accepts v-prefixed tags.
- Policy: `main` is GitHub's default branch and shows the latest release;
  pull requests still target `develop`. Every version bump is released through
  `build.yml` (`AGENTS.md`, README in both languages; Japanese checked with
  yomiyasu).
- Open: 0.1.0 builds still query upstream for updates and cannot update to Xen
  releases. macOS builds are ad-hoc signed and not notarised.
- Verification: serial build and 498 tests pass (one opt-in voice test skipped);
  the release workflow file parses as YAML. Platform packages are checked on the
  GitHub release itself.
- Trial build: the trial was running, so the build went to
  `work/OpenUtau-Xen-staged-win-x64/` (`OpenUtau-Xen.exe` and the ReadyToRun
  `OpenUtau-Xen.dll` match the new output, 23:45:04 local time); replacing
  `work/OpenUtau-Xen-win-x64/` is pending. Not launched interactively.
- Release run 1 (40204389009): all five platforms built and passed their tests,
  but the release step failed with 403 because the repository's default
  workflow token is read-only. `build.yml` now requests `contents: write`. The
  release is built from `develop` with that fix; it differs from `v0.2.0` only
  in the workflow file, and the release is attached to the `v0.2.0` tag.
- Release run 2 (40205431645, from `develop` at `427b26d1`): all five platforms
  built, passed their tests and attached their assets. Checked before
  publishing: the Windows x64 appcast points to
  `palf-gh/OpenUtau-Xen/releases/download/v0.2.0/OpenUtau-Xen-win-x64.zip` with
  version 0.2.0; the zip contains `OpenUtau-Xen.exe`, its DLL, DirectML and a
  stable-channel `prefs-default.json`; the Linux x64 archive's `OpenUtau-Xen` is
  executable. Published as
  [OpenUtau Xen 0.2.0](https://github.com/palf-gh/OpenUtau-Xen/releases/tag/v0.2.0)
  (latest, not a pre-release) with Windows x64/arm64 zips and installers, a macOS
  arm64 dmg, and Linux x64/arm64 tarballs and AppImages. None of these packages
  was launched.

## 2026-10-26 — Multi-period MOS keyboards

- Purpose: build keyboards from a MOS whose generator chain repeats several times
  in the tuning period, such as 12-EDO diminished 4L4s, 15-EDO blackwood 5L5s and
  22-EDO pajara 2L8s. Previously the chain repeated early and the MOS was reported
  unavailable.
- Implementation: `MosGenerator.Generate` takes a repeat count. The generator is
  counted inside one repeat, the note count covers the tuning period and modes
  rotate within one repeat. `KeyboardMos.Periods` stores the count (null means one
  and is not written). The regionizer's MOS cache includes it, and a multi-period
  MOS never takes family names. The tuning settings MOS card has a "Repeats per
  period" field in every Xen language; the mode maximum follows the note count
  divided by the repeats. Like generator and note count, an edited repeat count
  survives a structure change and otherwise returns to one. `.tunxen` and `.ustxen` move to version 3; version 2
  reads with one repeat. `TunxenFormat` now locks its YAML serialiser and
  deserialiser as `Yaml` already does, after a parallel test run hit YamlDotNet's
  thread-unsafe object factory cache. Default and family MOS stay single-period.
  Unequal tunings repeat in equal degree counts, not necessarily equal cents.
- Verification: serial solution build succeeded with zero errors. Full suite:
  511 passed, one opt-in external-voice test skipped. `MultiPeriodMosTest`
  covers 4L4s degrees and modes, 5L5s, 2L8s with equivalent generators, rejected
  repeat counts, shared black keys and letter names, the cache key, family
  names, validation and version 3 round trips for both formats with version 2
  reads. AppTest sets 12-EDO 4L4s, an unfitting repeat count and the return to one
  repeat in the settings dialog, and keeps an edited repeat count through a
  division change; it fails without that fix. `git diff --check` passed.
- Trial: no trial was running, so the self-contained Windows x64 Release build
  replaced `work/OpenUtau-Xen-win-x64/` in place with local data kept. It was
  built from the uncommitted working tree on top of `a6f5eca`, so its version
  string names `a6f5eca`, which does not contain this work.
  `OpenUtau-Xen.exe` matches the generated apphost; the application and core
  DLLs match the ReadyToRun outputs by SHA-256. The branch starts from `develop`,
  so this trial does not contain the unmerged JOB plug-in, rhythm grid and Alt
  key changes from `feature/job-plugin-compatibility`. Native interactive launch
  was not checked.
- Open issues: the default MOS search does not propose multi-period MOS, and
  multi-period scale presets are not offered.
