# JOB plugin implementation record

Date: 2026-10-26. Implements `job-plugin-compatibility.md` Revision 2 on `feature/job-plugin-compatibility`.

## Purpose

Run user-installed VOCALOID JOB plugins (Lua scripts) against the current OpenUtau Xen voice part, through an independently written adapter for the observed `VS*` API. The scripts stay in their own folders; nothing from them, the editor or its voicebanks is copied into the repository or the trial.

## Implementation

- `OpenUtau.Core/JobPlugins/JobPluginCatalog.cs` scans every configured folder recursively without executing code. It skips reparse points and duplicate roots, reads strict UTF-8 (BOM accepted, 1 MiB limit), takes literal manifest fields from comment-stripped source with a filename fallback, and records unsupported `VS*` calls and restricted facilities. Duplicate names get the folder name appended. Missing folders, bad encodings and scripts without `main` become diagnostics.
- `JobPluginRuntime.cs` uses MoonSharp 2.0.0 (NeoLua stays referenced but unused here). Each run gets a fresh script with the basic, string, table, iterator, metatable, error-handling, maths and time modules only; there is no CLR interop, file loader, `io`, `os.execute`, `require` or `load`. Withheld functions raise a named error. A prelude adds the Lua 5.1 names the scripts use (`table.getn`, `table.maxn`, `unpack`, `math.pow`, `math.log10`, `math.mod`, `string.gfind`), replaces `table.sort` with a Lua heap sort so comparators stay pre-emptible, and provides a read-only `io.open` limited to the script's folder. Top-level code and `main` run in coroutines with an automatic yield every 20,000 instructions; between slices the runner checks cancellation, a 2,000,000,000-instruction budget and a two-minute limit that excludes time in dialogs.
- `JobPluginSession.cs` holds a detached copy of the part. Notes get session identities and snapshot cursors; the iterator skips removed notes and shows inserts after the next seek. Unchanged write-backs are no-ops, so scripts that update whole tables keep working. Note number changes move the physical pitch by semitones through `PitchResolver.NearestAddress(..., true)`, keeping any microtonal residual; unchanged notes keep their exact `PitchAddress`. Velocity, `vibratoType` 0 and `vibratoLength` have native mappings. Phonemes and phLock written together with a lyric change are accepted and left to the phonemiser; a phoneme change without one, and changes to the other extended fields, abort the run. A commit re-check stops a lyric that was changed and restored from carrying a phoneme edit. The melisma lyric `-` is written as OpenUtau's extender `+`, and `+` is shown to scripts as `-`. Lyrics a plugin writes entirely in katakana are stored in hiragana, keeping ー and ヴ. PIT/PBS map to `pitd` cents (`PIT × PBS × 100 / 8192`, PBS chosen from 2 to 24 so existing curves fit) and DYN to `dyn` (`200 × log10(value / 64)` tenths of a decibel, 0 as the curve minimum). Because native curves interpolate between sparse points and return to the default after the last one, they are exported as events every 5 ticks (the native curve interval) plus each stored point, with a default-valued event after the last point. Edited lanes become held steps on the native linear curve; untouched lanes keep the original points byte for byte. Other control types accept only their default values. Tempo (BPM) and time signatures are read-only song-global views. Dialog fields are copied on registration; cancelling discards the run.
- `JobPluginRunner.cs` prepares the session on the UI thread and runs the script on a worker; the piano roll builds the edit back on the UI thread after the conflict check and applies a non-empty edit as one undo group made of existing commands (`MovePartCommand`, `ResizeVoicePartCommand` without shifting content, `RenamePartCommand`, `RemoveNoteCommand`, `AddNoteCommand`, `MergedSetCurveCommand`), so the usual notifications and curve refreshes run. A failure rolls the group back and closes it. A part fingerprint taken before the run must still match, otherwise the edit is discarded. Expected stops (`JobPluginException`: unsupported operations, sandbox denials, script errors, limits) and conflicts are reported on the main window's status bar and logged as warnings, without an error dialog; unexpected internal exceptions still open the error dialog.
- `JobPluginManager.cs` keeps the catalogue built from `Preferences.JobPluginSearchPaths`; `DocManager.Initialize` scans at start-up.
- UI: the piano roll's Batch menu has a JOB Plugins submenu after the legacy plugin menu, with a Rescan entry; plugins whose static scan found unsupported or restricted features are labelled as limited but can still run. The process range is the selected notes' span, or the whole part. `JobPluginDialogWindow` builds the field form on the UI thread and `JobPluginInteraction` blocks only the worker. VSMessageBox uses the existing message box. Preferences has a JOB plugin folder list with Add folders (multiple selection), Remove and Rescan. New strings are in all 20 `Xen.<culture>.axaml` files; the Japanese strings passed yomiyasu run/verify `20261026T005953Z-ac07bc2f`.

## Verification

- `OpenUtau.Test/JobPluginTest.cs` (36 cases, synthetic fixtures): catalogue roots, subfolders, duplicates, malformed UTF-8, computed manifests and top-level code that must not run; sandbox denials and folder-confined reading; runaway loops at top level, inside `pcall` and inside a `table.sort` comparator; cancellation; exact 53-EDO pitch on lyric edits and whole-table write-backs; semitone moves with residuals; cursor mutation; a part with a nonzero origin, tempo and metre; PIT/PBS conversion and bounds; rounding of computed numbers; unsupported writes, including a strict abort that `pcall` cannot swallow; sparse curves that read and edit as rendered; lyric-driven phonemes, a restored lyric that cannot carry a phoneme edit, the `-`/`+` round trip, and katakana-to-hiragana conversion of written lyrics only; dialogs, cancellation and Win32 message codes; one undo group whose undo restores notes, curves and the part name.
- `OpenUtau.Test/JobPluginCorpusTest.cs` is an opt-in run of installed plugins (`OPENUTAU_XEN_JOB_PLUGIN_DIRS`). It accepts every dialog with its initial values and answers Yes to questions, on a synthetic eight-note part. Result for the user's four folders on 2026-10-26 (the synthetic `pitd` ramp renders 25 cents at tick 120; the probe shows the value afterwards; `lyrics` lists the resulting lyrics when they changed):

| Plugin | Static scan | Result |
| --- | --- | --- |
| Convert Phonemes | ok | ran, no change |
| LyricOnmei | ok | ran: notes −8/+8; lyrics は ら に ら ほ は ら に |
| MetoronomeJPN(PiPo) | ok | ran: notes −8/+16; lyrics ぴ ぽ ぽ ぽ ぴ ぽ ぽ ぽ ぴ ぽ ぽ ぽ ぴ ぽ ぽ ぽ |
| NyanNote | ok | ran: notes −0/+2; lyrics あ い う にゃ え ん お か き く; curves pitd; pitd@120 25→25 |
| Pitch Randomize | ok | ran: curves pitd; pitd@120 25→0 |
| QuantizeNoteLength | ok | ran: notes −8/+8 |
| SineVibrato | ok | ran: curves pitd,dyn; pitd@120 25→29 |
| SineVibratoedit3 | os.execute, os.getenv, io.open (write) | failed: =prelude:47: os.getenv is not available to JOB plugins in OpenUtau Xen |
| Staccato (selected) | ok | failed: VSUpdateNoteEx: changing note field 'accent' is not supported (no OpenUtau equivalent) |
| ThinOutPIT | ok | ran: curves pitd; pitd@120 25→24 |
| TrimPartLeftSide | ok | ran: notes −8/+5; curves pitd; pitd@120 25→0; part |
| 10centsDown | ok | ran: curves pitd; pitd@120 25→15 |
| 10centsUp | ok | ran: curves pitd; pitd@120 25→35 |
| 1centDown | ok | ran: curves pitd; pitd@120 25→24 |
| 1centUp | ok | ran: curves pitd; pitd@120 25→26 |
| AutoVibrato | ok | ran: curves pitd,dyn; pitd@120 25→25 |
| InterpPIT | ok | ran: curves pitd; pitd@120 25→0 |
| AutoScoopUpHighNotes | VSGetNextNoteExpression, VSGetNoteExpressionAt, VSInsertNoteExpression, VSRemoveNoteExpression, VSSeekToBeginNoteExpression | failed: VSSeekToBeginNoteExpression: VOCALOID4 note expressions (VR/VD) have no verified OpenUtau mapping yet |
| NudgeL | ok | ran: notes −7/+7; curves pitd; pitd@120 25→31 |
| NudgeR | ok | ran: notes −8/+8; curves pitd; pitd@120 25→19 |
| ShiftLyricsL | ok | ran: notes −7/+7 |
| ShiftLyricsR | ok | ran: notes −8/+8; lyrics + あ い う え お か き |
| Split Note | VSGetNextNoteExpression, VSGetNoteExpressionAt, VSInsertNoteExpression, VSRemoveNoteExpression, VSSeekToBeginNoteExpression | ran, no change |
| Adjust Gain | ok | ran, no change |
| Connect Notes | ok | ran, no change |
| Idol Style | ok | ran: curves pitd,dyn; pitd@120 25→150 |
| Insert Rest | ok | ran: notes −6/+6 |
| Lyrics to Part Name | ok | ran: part |
| Part Detune | ok | ran, no change |
| Staccato | ok | ran: notes −8/+8 |
| Timing Randomize | ok | ran: notes −8/+8 |
| V3KeroPitch | ok | ran: notes −8/+8; curves pitd; pitd@120 25→-900 |
| Vibrato Type Conversion | ok | ran, no change |

"Ran" means the script finished and produced the listed edit; it does not mean the edit was compared with the original editor's output. The cent plugins give a direct check: 10centsUp/10centsDown move 25 cents to 35/15 and 1centUp/1centDown to 26/24. "No change" usually reflects the dialog defaults or the synthetic part (for example, contiguous notes for Connect Notes). Split Note ran without reaching its note-expression calls with the default settings.

## Open issues

- Lyric-driven phonemes are not applied, so a pronunciation that differs from what the phonemiser derives from the lyric is lost. Lyrics written in katakana are converted only when the whole lyric is katakana; a phonemiser that needs other forms (romaji, for example) still sees the converted hiragana.
- ConvertPhonemes changes phonemes without lyrics; with its default settings it matched nothing on the synthetic part, and any match would stop the run.
- Accent and decay (StaccatoSelected) have no verified mapping to OpenUtau's `atk`/`dec`.
- V4 note expressions (AutoScoopUpHighNotes) need a rate/depth mapping onto native vibrato.
- SineVibratoedit3b creates folders and writes settings outside its folder at top level; it stays unsupported.
- Instruction pre-emption cannot interrupt a loop inside a CLR callback such as a `string.gsub` replacement function; the time limit is checked only between slices.
- Edited PIT/DYN lanes are stored as held steps with a one-tick ramp, so an edited ramp lags the native one by up to one 5-tick step; they are not sample-identical to the original editor. Non-integral numbers round to nearest even by declared policy.
- Native interactive use (dialogs, menu, undo in the running application) was not checked in this change.
