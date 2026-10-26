# JOB plugin compatibility specification

Date: 2026-10-26. Revision 1, written before implementation.

## Objective and evidence

Load user-installed Lua JOB plugins from several configurable directories and execute supported editing operations against the current OpenUtau Xen voice part. This is an independently implemented editing API adapter, not a VOCALOID synthesiser or binary compatibility layer. The existing UTAU plugin system remains separate.

The source-contract inventory is `job-plugin-api-inventory.md`; the native-model investigation is `job-plugin-model-mapping.md`. They identify 30 user-installed scripts and 38 API names, with file/line evidence and remaining unknowns. No local SDK reference was found. Observed calls establish a bounded compatibility target, not every undocumented behaviour of the original editor.

Primary references:

- Lua 5.1 language reference: https://www.lua.org/manual/5.1/manual.html
- VOCALOID's description of the part-based plugin scope: https://www.vocaloid.com/support/faq/295
- Interpreter isolation and instruction scheduling: https://www.moonsharp.org/sandbox.html and https://www.moonsharp.org/coroutines.html

No editor executable, DLL, voicebank, plugin source body or vendor asset is copied into this repository or the trial package. Tests use original synthetic Lua fixtures. Installed plugin files are read in place and remain owned by their respective authors. A source inventory and independently written adapter are not presented as legal clearance or vendor certification.

## Discovery and preferences

- Preferences expose a list of JOB plugin folders with Add, Remove and Rescan actions. Folder selection accepts more than one directory. Saved paths are local preferences, never project tuning or hard-coded personal defaults.
- The four user-supplied directories are added to the local trial preferences without replacing existing entries. The repository does not contain their scripts.
- Enumerate `.lua` files deterministically, case-insensitively on Windows. Normalise paths, deduplicate the same physical pathname, skip directory links and isolate inaccessible paths or malformed files as diagnostics.
- Discovery never executes script top-level code or `manifest()`. Extract literal manifest fields from source, with filename fallback for computed metadata. A processing entry point must be present. Preserve separate files with the same pluginID; identify entries by canonical path and distinguish duplicate names by path.
- Read strict UTF-8, accepting a BOM; reject malformed encoding explicitly. Bound source size. A manifest API version is descriptive metadata, not evidence that every operation is supported.
- Record statically observed `VS*` calls and restricted external facilities. Unsupported calls remain visible in diagnostics. Runtime checks remain authoritative; static scanning is not a proof about computed function names.

## Execution and isolation

- Use a fresh MoonSharp interpreter for every execution, with only basic language, string, table, iterator and mathematics facilities plus time queries. Provide Lua 5.1 compatibility aliases such as `table.getn`, `unpack`, `math.pow` and `math.log10` where absent.
- Do not expose CLR objects, automatic type registration, shell execution, arbitrary file I/O, network access, native modules or unrestricted file loaders. Scripts requiring these facilities fail explicitly; the known settings-file variant is not advertised as fully compatible.
- Top-level initialisation and `main` both execute through instruction-limited coroutines. Check cancellation and a finite instruction/time budget between slices. A plain Task timeout without terminating execution is insufficient.
- Invoke `main(processParam, envParam)`. Its integer result must be zero to commit; a nonzero result, exception, unsupported operation, timeout or cancellation discards all staged edits. Cancellation of a plugin input dialog also prevents commit, even if the script returns zero.
- `processParam` contains part-local `beginPosTick`, `endPosTick` and `songPosTick`. A selected note range supplies its enclosing interval; otherwise use the whole part. Iterators still expose the whole part, as scripts implement their own filtering. `envParam` identifies the original script directory/name and a per-run temporary directory; exposing these strings does not grant file access.
- Run Lua on a worker. Dialog callbacks marshal to the UI dispatcher and create controls there. Disable concurrent application of the same run; before committing, verify that the project, part and original content still match the captured transaction.

## Data and time contracts

- The host uses native 480 PPQ, matching the supplied scripts. Note/control positions are part-local; part position and tempo/time-signature lookup positions are song-global. Nonzero part origins must be tested explicitly.
- Success flags are numerical 1/0, not Lua booleans. `VSGetNext*` returns `0, nil` at exhaustion. Queries returning control defaults or values return a scalar as their second result.
- Each note exposes a stable `objID` for its transaction. Move, resize or pitch changes do not change identity. Newly inserted notes receive new identities. Returned tables are detached values; changes only take effect through an update call.
- Iteration uses stable snapshots of identities at seek time. Removed objects are skipped; inserts become visible after another seek. Control iteration is independent for each control type. This is an explicit host policy where original undocumented mutation semantics have not been established.
- Validate finite numbers, integer fields, positive durations, nonnegative note positions and supported pitch ranges. Reject invalid data without partial project changes. Updating a note's position must remove/reinsert it in sorted storage.

## Note and part mapping

- `VSSeekToBeginNote`, `VSGetNextNote`, `VSGetNextNoteEx`, `VSInsertNote`, `VSInsertNoteEx`, `VSUpdateNote`, `VSUpdateNoteEx` and `VSRemoveNote` operate on the detached note transaction.
- `posTick`, `durTick` and `lyric` map to note position, duration and lyric. `noteNum` is an integer MIDI adapter. If a plugin leaves it unchanged, retain the original `PitchAddress` and tuning exactly. If it changes by an integer delta, preserve the original residual frequency offset by applying that physical semitone delta through `PitchResolver`; do not reinterpret MIDI integers as tuning degrees. Inserted notes resolve MIDI pitch through the effective tuning.
- VOCALOID velocity is not OpenUtau's phoneme-consonant stretch. Return a stable default adapter value and reject changes unless a separately documented mapping is implemented; never silently turn it into volume or consonant timing.
- Phoneme strings, phLock, accent, decay, portamento and vibrato type are not automatically equivalent to native phonemiser data. Preserve unchanged fields. Unsupported changes fail explicitly. Extended fields may be read as declared adapter defaults; writes are not silently discarded.
- `VSGetMusicalPart` returns its global position, duration, name and comment. `VSUpdateMusicalPart` supports validated position/duration/name/comment changes, with note and curve bounds checked before commit. `VSGetMusicalPartSinger` reports the actual track singer identity; unsupported language-bank enumerations do not masquerade as a VOCALOID bank.

## Controls and pitch

- Implement the eight observed control calls: seek, next, get-at, get-default, insert, update, update-at and remove. Use transaction-local event identities so moving an event does not lose its original identity.
- `PIT` and `PBS` form a virtual MIDI pitch-bend view of native additive `pitd` cents. The normalised bend maps as `cents = PIT / 8192 * PBS * 100`; the positive limit is 8191. Choose a representable sensitivity for existing deviations. Reconstruct the union of bend/sensitivity event positions when either changes.
- This pitch deviation is added after native base pitch, portamento and vibrato. It must not replace the final pitch curve, modify the exact note address, or add those components a second time. If neither pitch control changes, retain the original native curve byte-for-byte. Reject a result outside the supported native expression range instead of clipping it silently.
- Native curves interpolate, whereas these API events use a declared step-held host policy. Preserve native curves on no-op runs; any edited event projection must document and test its sample/rounding boundary, including range endpoints. Do not label edited curves sample-identical to the original editor.
- `DYN` has an approximate gain mapping to native dynamics. Other timbral controls may be mapped only where the active renderer exposes an explicitly identified expression with a documented range. `BRE`, `BRI`, `CLE`, `GEN`, `POR`, `XSY` and `GWL` must not silently create unaudible compatibility-only curves. Unsupported writes abort the transaction and name the control.

## Tempo, metre and V4 expressions

- `VSSeekToBeginTempo`, `VSGetNextTempo`, `VSGetTempoAt`, `VSSeekToBeginTimeSig`, `VSGetNextTimeSig` and `VSGetTimeSigAt` are read-only views of `TimeAxis`. Return BPM multiplied by 100 for tempo values. Tempo/metre events use global song ticks; time signatures additionally expose their measure location. No plugin API changes the project tempo map in this initial implementation.
- The observed V4 extension comprises `VSSeekToBeginNoteExpression`, `VSGetNextNoteExpression`, `VSGetNoteExpressionAt`, `VSInsertNoteExpression` and `VSRemoveNoteExpression`, with normalised note-relative positions and types `VR`/`VD`.
- Native vibrato is parameterised differently, and the precise vendor rate/depth value domains are not established by the available primary evidence. These extension calls are initially explicit unsupported operations. Scripts depending on them must be identified as requiring unsupported APIs, not claimed compatible merely because their manifest declares 4.2.0.1. Implementing and validating a physical rate/depth conversion is a later extension, not an arbitrary numeric pass-through.

## Dialog contract

- `VSDlgSetDialogTitle` and `VSDlgAddField` build detached dialog data. Copy each field when added because scripts reuse their table.
- Field type 0 is integer, 1 is numeric boolean, 2 is floating point, 3 is text and 4 is a choice list. The observed comma-separated `initialVal` supplies choices; after acceptance the getters return the selected text, not its index. Bounds and duplicate field names are validated.
- `VSDlgDoModal` returns 1 for acceptance and 2 for cancellation. The typed getters return `status, value`; boolean values are 0/1. Text is displayed as plain text, not interpreted as markup.
- `VSMessageBox` displays text with a mapped supported button set. Only flag/result combinations with an explicit mapping are accepted; unsupported flags fail rather than guessing a Yes response.

## Commit, undo and errors

- Execute against a detached snapshot. Successful changes apply on the UI thread as one undo group/command containing notes, curves and part metadata. A no-op produces no undo entry. Restore note selection appropriately after replacement.
- Undo/redo restores exact pitch, original curves, timing and metadata. Notify through normal document commands so phonemisation, renderer caches and views invalidate through existing paths.
- Failures show the plugin name, failing API or source location and cause. Missing paths or one malformed plugin do not prevent the remaining catalogue from loading. Rescan updates the menu without restarting the application.

## Acceptance and implementation order

1. Save this specification and its two evidence reports before writing the host.
2. Implement safe multi-folder discovery and the restricted runtime with original fixtures, including top-level side effects, infinite loops and malformed sources.
3. Implement note/part/time/control adapters and atomic undo. Tests cover nonzero origins, exact microtonal no-ops, semitone moves with residuals, cursor mutation, PIT/PBS, unsupported writes, cancellation and rollback.
4. Add preferences, catalogue/menu and UI-thread dialog bridge. Verify multiple paths, duplicate names, refresh, cancelled dialogs and background construction.
5. Run serial solution build/full tests, inspect actual supplied metadata in place without redistributing source, record per-feature limitations, and refresh the self-contained Windows x64 trial. Do not report all 30 scripts as verified without executing and checking each one.

The first implementation may support only the explicitly mapped subset above. Its catalogue and completion record must disclose the remaining API/resource limitations. This specification does not authorise changes to installed plugins, voicebanks or editor files, nor remote publication.
