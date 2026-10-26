# JOB plugin source-contract inventory

Date: 2026-10-26. This is a read-only inventory for compatibility design, before implementation. It records interface names and observed calling conventions, not plugin implementations. No external script body, editor binary or licence text has been copied into this repository.

## Evidence boundary

The inspected corpus contains 30 `.lua` files: ten in `C:/Users/kaito/Desktop/JOBPLUGINA` (A), six in `C:/Users/kaito/Desktop/JOBPLUGIND` (D), four in `C:/Users/kaito/Desktop/JOBPLUGINC` (C), and ten in `C:/Program Files (x86)/VOCALOID4/Editor/JobPlugins` (V). The V folder has no subdirectories and no local SDK reference was found within it. Each desktop folder also contains `jobplugin_common_eula.txt`; those licences were not reproduced. Merely locating or reading a user's plugin does not grant redistribution rights.

All 30 source files decode as strict UTF-8 without a UTF-8 BOM. This is an observation about this corpus, not a universal encoding requirement. The implementation should separately specify how other encodings and invalid input are handled.

Twenty-three manifests declare API `3.0.0.1`; three declare `3.0.1.0` (A/ConvertPhonemes.lua, C/ShiftLyricsL.lua and C/ShiftLyricsR.lua); four declare `4.2.0.1` (A/TrimPartLeftSide.lua, D/InterpPIT.lua, C/AutoScoopUpHighNotes.lua and C/SplitNote.lua). A declared version alone does not prove full API coverage or compatibility. The observed corpus uses 38 distinct `VS*` entry points.

Evidence below uses `folder/file:line`, with the A/D/C/V roots defined above. Findings are source observations. They do not claim execution in VOCALOID or establish undocumented host semantics.

## Entry points and environment

The scripts expose `manifest()` returning a table with `name`, `comment`, `author`, `pluginID`, `pluginVersion` and `apiVersion`; they expose `main(processParam, envParam)` as the processing entry point. See A/ConvertPhonemes.lua:21–46 and V/ConnectNotes.lua:13–43. Ordinary success is returned as `0`, while an error is returned as `1`; user cancellation commonly returns `0` without applying an operation. See A/QuantizeNoteLength.lua:99–106 and V/Lyrics2PartName.lua:69–83.

`processParam` reads use `beginPosTick`, `endPosTick` and `songPosTick`, described in the source comments as local ticks. `envParam` reads use `scriptDir`, `scriptName` and `tempDir`; scripts expect directory values to include a trailing delimiter. The API-version helper in A/ConvertPhonemes.lua:123–137 also searches `envParam` for `apiVersion`.

The common note-filter idiom is `beginPosTick <= note.posTick < endPosTick`, after seeking and iterating all part notes, rather than relying on host iteration to apply the processing range. See A/SineVibrato.lua:109–117. Other scripts deliberately process every note; selection and processing-range policy therefore must not silently truncate the iterator.

## Observed API signatures

The signatures below use `status` for the numerical success flag, `table` for a Lua record and `value` for a scalar. Calls checked by the scripts use `1` for success and `0` for failure or exhausted iteration. These signatures describe observed use; the exact distinction between an empty iterator and an API error is not recoverable from every call site.

### Notes — eight entry points

- `VSSeekToBeginNote() -> status`. Resets note iteration; the return is often ignored. A/ConvertPhonemes.lua:86.
- `VSGetNextNote() -> status, note`. A/ConvertPhonemes.lua:88.
- `VSGetNextNoteEx() -> status, note`. A/SineVibrato.lua:113.
- `VSInsertNote(note) -> status`. A/MetronomeJPNPiPo.lua:156.
- `VSInsertNoteEx(note) -> status`. C/AutoScoopUpHighNotes.lua:178.
- `VSUpdateNote(note) -> status`. A/ConvertPhonemes.lua:101.
- `VSUpdateNoteEx(note) -> status`. A/SineVibrato.lua:372.
- `VSRemoveNote(note) -> status`. A/MetronomeJPNPiPo.lua:91; return use is visible in other removal sites.

Observed note fields are `posTick`, `durTick`, `noteNum`, `velocity`, `lyric`, `phonemes`, `phLock`, `objID`, `accent`, `decay`, `vibratoLength` and `vibratoType`. A/MetronomeJPNPiPo.lua:137–147 constructs notes with velocity `64`. A/ConvertPhonemes.lua:142–151 tests for `phLock` before assigning it, providing evidence of an optional/version-dependent field. C/AutoScoopUpHighNotes.lua:248–256 uses `objID` to identify existing notes, and its later search discovers IDs of inserted notes. Compatibility must therefore specify stable host-assigned identities and must not use mutable pitch/start-time pairs as identities.

The corpus does not establish every valid range, mandatory field or omitted-field default. In particular, MIDI-like `noteNum`/`velocity` ranges must be corroborated with a reference or explicitly declared as implementation policy. It also does not establish whether `VSInsertNoteEx` mutates the supplied table with a new identity.

### Part and singer — three entry points

- `VSGetMusicalPart() -> status, part`. A/MetronomeJPNPiPo.lua:97.
- `VSUpdateMusicalPart(part) -> status`. A/TrimPartLeftSide.lua:172 and V/Lyrics2PartName.lua:76.
- `VSGetMusicalPartSinger() -> status, singer`. C/AutoScoopUpHighNotes.lua:56.

Observed part fields are `posTick`, `playTime` and `name`. The `durTick` token occurs in a commented alternative in A/TrimPartLeftSide.lua; active processing uses `playTime`. V/Lyrics2PartName.lua:75 updates `part.name`. A/TrimPartLeftSide.lua:170–172 changes the part start and playable length together. Source use treats part position as absolute song ticks but note/control positions as local part ticks.

The only observed singer field is `vBS`, used by C/AutoScoopUpHighNotes.lua:56–57 to choose a language-specific setting. Its exact enumeration and relation to OpenUtau singers remain unresolved; returning a guessed language would be an unsupported approximation.

### Control curves — eight entry points

- `VSSeekToBeginControl(type) -> status`. A/NyanNote.lua:164.
- `VSGetNextControl(type) -> status, control`. A/NyanNote.lua:165.
- `VSGetControlAt(type, posTick) -> status, value`. A/SineVibrato.lua:198.
- `VSGetDefaultControlValue(type) -> status, value`. A/TrimPartLeftSide.lua:126–127 stores the second return directly as the default value, not as a record.
- `VSInsertControl(control) -> status`. A/NyanNote.lua:119.
- `VSUpdateControl(control) -> status`. A/TrimPartLeftSide.lua:150; note that changing `posTick` must preserve the original event identity.
- `VSUpdateControlAt(type, posTick, value) -> status`. A/SineVibrato.lua:322.
- `VSRemoveControl(control) -> status`. A/NyanNote.lua:168.

Control records use `type`, `posTick` and `value`. Observed type identifiers are `PIT`, `PBS`, `DYN`, `BRE`, `BRI`, `CLE`, `GEN`, `POR`, `XSY` and `GWL`. Read-only strings or a local variable named `control` must not be mistaken for a Lua record: the `*At` and default getters return scalar values in the observed scripts.

The scripts assume seeking each control type has an independent useful iterator and allow updating/removing events while subsequently continuing iteration. Stable snapshot iteration is a proposed host policy, not proven editor internals. Source call sites alone do not establish interpolation/hold rules, duplicate-event handling, legal ranges, default values or the precise semantics of inserting at an existing tick. These require explicit specification and tests.

### Tempo and metre — six entry points

- `VSSeekToBeginTempo() -> status`. D/AutoVibrato.lua:275.
- `VSGetNextTempo() -> status, tempo`. D/AutoVibrato.lua:277.
- `VSGetTempoAt(posTick) -> status, value`. A/SineVibratoedit3b.lua:299.
- `VSSeekToBeginTimeSig() -> status`. A/MetronomeJPNPiPo.lua:101.
- `VSGetNextTimeSig() -> status, timeSig`. A/MetronomeJPNPiPo.lua:102.
- `VSGetTimeSigAt(posTick) -> status, numerator, denominator`. A/MetronomeJPNPiPo.lua:133 explicitly adds `musicalPart.posTick` to the local tick before calling it.

Tempo records use `posTick` and `tempo`. Metre records use `posTick`, `numerator` and `denominator`. D/AutoVibrato.lua:293 converts a tick difference to milliseconds using `125 / tempo`, which implies BPM rather than BPM × 100 at 480 ticks per quarter note. This is stronger source evidence than an inferred type name, but it remains a corpus-derived contract. The common constants `1920`, `960`, `480`, `240` in A/QuantizeNoteLength.lua:35–41 also establish a 480-PPQ assumption. The adapter must convert if the OpenUtau project resolution differs.

Absolute/local timing for `VSGetTempoAt` is not established by its one observed sample loop; it should follow the authoritative contract when available and be covered by a non-zero-part-offset test. The metre getter's absolute-song use is explicit.

### V4 per-note expression curves — five entry points

- `VSSeekToBeginNoteExpression(note, type) -> status`. C/AutoScoopUpHighNotes.lua:267.
- `VSGetNextNoteExpression(note, type) -> status, expression`. C/AutoScoopUpHighNotes.lua:268.
- `VSGetNoteExpressionAt(note, type, posNorm) -> status, value`. C/AutoScoopUpHighNotes.lua:291.
- `VSInsertNoteExpression(note, expression) -> status`. C/AutoScoopUpHighNotes.lua:330.
- `VSRemoveNoteExpression(note, expression) -> status`. C/AutoScoopUpHighNotes.lua:363.

Observed expression records contain `type`, `posNorm` and `value`. Both C/AutoScoopUpHighNotes.lua:27 and C/SplitNote.lua:10 enumerate only `VR` and `VD`. C/AutoScoopUpHighNotes.lua:287 calculates `posNorm` as elapsed note ticks divided by note duration; its split logic renormalises expression positions for both fragments and inserts a boundary value where required. This establishes normalised note-relative positions, not raw project ticks. The valid value domains and interpolation rules remain unresolved from the local source alone.

### Dialogs and messages — eight entry points

- `VSDlgSetDialogTitle(title) -> status`. A/ConvertPhonemes.lua:49; return often ignored.
- `VSDlgAddField(field) -> status`. A/ConvertPhonemes.lua:56.
- `VSDlgDoModal() -> dialogStatus`. A/QuantizeNoteLength.lua:99–106 checks `1` for accepted, `2` for cancelled and other values for an error.
- `VSDlgGetIntValue(name) -> status, integer`. A/MetronomeJPNPiPo.lua:75.
- `VSDlgGetBoolValue(name) -> status, value`. A/QuantizeNoteLength.lua:113; the observed scripts use numerical boolean values, so Lua truthiness is not a sufficient bridge.
- `VSDlgGetFloatValue(name) -> status, number`. V/PartDetune.lua:79.
- `VSDlgGetStringValue(name) -> status, string`. A/ConvertPhonemes.lua:76.
- `VSMessageBox(text, flags) -> result`. A/ConvertPhonemes.lua:115 uses flags `0`; exact result-code and button/icon meanings are not proved by that call.

Field records use `name`, `caption`, `initialVal` and `type`. Observed type codes are `0` integer (A/MetronomeJPNPiPo.lua:56–59), `1` checkbox with `0`/`1` initial value (A/QuantizeNoteLength.lua:91–95), `2` floating point (V/PartDetune.lua:53–56), `3` text (A/ConvertPhonemes.lua:52–56), and `4` a comma-separated choice list in `initialVal` (A/QuantizeNoteLength.lua:79–88). Scripts reuse and mutate a single `field` table before multiple `VSDlgAddField` calls: the host must copy each field at registration rather than retaining the same mutable table reference. The exact treatment of commas inside choices, default selection and validation limits remains a reference question.

## Lua support and loading side effects

Observed built-ins include `pairs`, numeric/generic loops, multiple return values, string concatenation, `#`, `table.getn`, `table.insert`, `string.find`, `string.gmatch`, `string.gsub`, `math.abs`, `math.ceil`, `math.floor`, `math.log10`, `math.pi`, `math.pow`, `math.random`, `math.randomseed`, `math.sin` and `os.time`. No `require`, `dofile`, `loadfile`, `loadstring`, `setfenv` or `getfenv` call was found in this bounded corpus. Absence here is not proof that third-party JOB plugins never use modules or those functions.

A/SineVibratoedit3b.lua has top-level `os.getenv("APPDATA")` at line 33 and `os.execute` invoking directory creation at line 35, before its processing entry point. It reads and writes a settings file via `io.open` at lines 63 and 425. Metadata discovery that executes a script to call `manifest()` can therefore run side effects before manifest discovery. A safe, deterministic discovery policy must not expose shell, filesystem or CLR access merely to inspect a manifest. A runtime that restricts those facilities must mark this plugin's settings-file behaviour unsupported or provide a deliberately scoped replacement; pretending that all 30 scripts are equivalent pure transformations would be incorrect.

## Items still requiring a reference or declared policy

- Host integer ranges, omitted-field defaults, all error codes and `VSMessageBox` flags/results.
- Note/control identity and iterator mutation semantics, including duplicate ticks and newly inserted objects.
- Sampling and interpolation of control and per-note expression curves.
- All `vBS`, vibrato-type, accent/decay and expression-value enumerations; OpenUtau semantic equivalence must not be assumed.
- Which time APIs require song ticks versus part-local ticks, especially tempo-at-time.
- Complete API coverage beyond these 38 observed functions, Lua VM/version details and capability restrictions for external resources.
- No plugin was executed against the editor or a compatibility implementation during this inventory. A successful metadata read or synthetic fixture will not establish full behavioural compatibility for the supplied plugins.
