# Pitch editing and tuning interface

## Editor review corrections

- Changing degree count or equal/explicit structure rebuilds a detached preset skeleton before updating the degree table. Its all-scale, derived scales, notation profiles, keyboard classes and MIDI mapping cannot retain out-of-range indices from a larger preset. The same check runs when applying without pressing **Rebuild degree table**. Custom names and current Hz are retained; an automatically assigned reference degree updates to the new skeleton, while a manually chosen reference degree is retained. No project notes change until an Apply action.
- Reducing an explicit tuning retains its initial exact intervals; increasing it initialises a new equal interval table for editing. Metadata dependent on the old explicit structure is reset.
- Note dragging captures the vertical pointer-to-base-pitch offset at gesture start. A horizontal drag cannot add a second copy of the note's expressive tuning offset, including in 53-EDO. The gesture continues to use the immutable mapper snapshot.
- Free-text cents accept the selected UI language's decimal separator and invariant decimal-point values. Thousands/grouping separators and non-finite values are rejected. Numeric controls use the selected input culture, with integer-only parsing for degree counts and indices. File formats remain invariant.
- `TuningEditorReviewTest` covers smaller-EDO metadata rebuilding, exact explicit intervals, reference/name/Hz retention, expressive-offset drag anchors and comma-decimal input. These tests are added for the parent's serial verification; this review did not execute builds, tests or computer use.

## Implemented

- Piano-roll **Edit > Tuning…** opens a detached tuning editor. The main-window Project menu can open the same editor with project scope selected.
- Project defaults and track overrides are separate. **Use project tuning** removes the current track override. **Apply tuning**, **Change reference** and **Apply display** use distinct undoable commands.
- Preset selection, name, equal divisions, explicit pitch count, period ratio/cents, reference degree/frequency, scale, keyboard mode and primary/secondary notation are editable controls.
- The degree table edits explicit ratios/cents, custom names, aliases and semantic key classes. **Rebuild degree table** applies the chosen number of explicit pitches. The Definition tab provides the complete `.tunxen` representation for profiles and arbitrary scale sets.
- `.tunxen`, Scala `.scl` and Scala `.kbm` can be imported/exported. KBM import applies its own reference mapping explicitly.
- The generator tab produces degree subsets with the MOS analyser, exact interval-generated tunings and named arbitrary degree subsets entered as comma-separated integers.
- **Preview changes** reports the number of affected notes and the largest tuning-change interval in cents. **Keep existing frequencies** preserves pitch with a residual-cent address.
- Full, Scale Highlight and Compact share `PitchRowMapper`. Compact includes off-scale degrees already used by notes. Both directions interpolate physical continuous MIDI pitch, including non-uniform intervals.
- Drawing, hit-testing, selection, pitch curves, final-pitch overlays, keyboard preview, note creation/movement and focus/scroll use the common mapping. A mapper snapshot remains fixed during note-edit gestures and multi-note row moves.
- Up/Down move displayed rows; Ctrl+Up/Down move one tuning period; Alt+Up/Down move one raw tuning degree.
- Copy/paste between differing tunings preserves source pitch through the nearest target degree and residual cents. Plain paste retains physical pitch while omitting expressive data.
- Keyboard labels use the application font for names and Bravura Text only for SMuFL PUA runs. Secondary labels appear beside the primary label on short rows and below it on tall rows.
- Pitch-curve segments and spline control values interpolate in physical pitch before conversion to display rows. Vibrato-control offsets remain a fixed number of display rows while depth remains a physical interval.
- The Piano-roll Edit menu exposes the MIDI-input dialog implemented by the MIDI subsystem.

## Verification

`PitchRowMapperTest` covers standard 128-row geometry, inverse interpolation in a non-uniform tuning, Compact supplemental rows without pitch changes, immutable gesture snapshots and empty-subset rejection. Serial build/test results are recorded by the integration task in the production log.

The app compiled after removing the WPF-only `DataGrid.CanUserAddRows` property. Subsequent integration builds and visual verification remain the responsibility of the parent integration run; this note does not claim those checks have passed.

## Remaining limitations and checks

- Verify keyboard labels, data-grid editing, pitch curves, vibrato handles and modal sizing at high DPI in the running application.
- Xen-owned resource overlays cover every upstream language, including Romanian.
  Tuning, MOS, MIDI, export warnings and preset names use stable resource keys;
  technical identifiers and file contents are independent of translated labels.
  Key/placeholder coverage and runtime language switching are checked in AppTest.
- The explicit-pitch table rebuilds equal initial intervals when increasing its size. Arbitrary existing ratios can be edited afterwards or loaded losslessly from `.tunxen`/Scala.
- Definition YAML is the complete editor for arbitrary profile/scale structures beyond the principal degree table and generator controls.
- Curve interpolation now uses physical pitch in drawing and hit testing, but full playback/synthesis comparisons and all interaction combinations still require integration testing.

## UI copy

Applied `stop-describing-ui` to new controls: action labels and specific numeric fields replace feature-description prose. The frequency-change preview is retained because it communicates the pitch-changing consequence.

## Workspace layout from OpenUtau Lunai

The workspace layout is reimplemented from
[OpenUtau Lunai](https://github.com/keirokeer/OpenUtau-lunai) at commit
`01be2980269b01fb2d3f90e311d1132e91588768` (MIT, same `Copyright (c) 2014 StAkira`
notice as Xen). Lunai's commits are not cherry-picked: its UI changes are mixed with
DiffSinger features (Singer Hub, quality presets, key/scale overlay, harmony, live
pitch), which Xen does not take.

### Design policy

- Purpose: adopt Lunai's card-based workspace while keeping Xen's colours, the
  92px three-tier keyboard column and the tuning menus.
- Implementation: `DESIGN.md` §1 and §5, `AGENTS.md` and the README attribution
  now describe the Lunai-based layout.
- Verification: documentation only.

### Theme foundation

- Purpose: provide the surfaces Lunai's workspace draws on, in Xen colours.
- Implementation: eight colour keys (`WorkspaceCanvasColor`, `WorkspaceCardColor`,
  `WorkspaceElevatedSurfaceColor`, `MutedIconColor`, `AppTopBarTransportStripColor`,
  `AppTopBarTransportHoverColor`, `AppTopBarValueStripColor`,
  `AppTopBarValueDividerColor`) are in the light, dark and custom themes, the custom
  theme YAML and the theme editor. Lunai's fifteen per-toggle accent hues are replaced
  by one `PianoRollSideAccentBrush` in the sub colour. A custom theme saved before
  these keys existed takes the built-in light or dark value for its mode
  (`ThemeYaml.FillWorkspaceDefaults`). `Styles/WorkspaceStyles.axaml` holds the
  workspace, welcome-screen and top-bar styles without Lunai's appearance,
  theme-editor, phoneme-panel, Singer Hub and track-colour styles. Controls now use
  Lunai's 6px corner radius.
- Not ported: `WorkspaceScrollbarHelper` and `RoundedClipBorder`, which only serve
  Lunai's overlay scrollbars and re-skinned combo boxes.
- Verification: `XenThemeTest` loads custom themes with and without the new keys.

### Welcome screen

- Purpose: Lunai's card-based start page.
- Implementation: a Start card (recovery button, New and Open tiles) above Recent
  and Template cards on the workspace canvas. Lunai's Singer Hub column is left out,
  so the two lists share the full width. `OnCarouselPageKeyDown` stops the Carousel
  from switching pages on arrow, Home and End keys that bubble up from page content;
  the keys are passed to the window's own handler first, so Home/End still move the
  playhead.
- Verification: rendered headlessly in the light theme (welcome cards, menu above).
