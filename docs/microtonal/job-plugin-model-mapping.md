# JOB plug-in model mapping research

Date: 2026-10-26. Status: design research, before implementation. This document records the existing OpenUtau model and proposed bridge policies. It is not a claim that the APIs are implemented or that VOCALOID synthesis has been reproduced. The companion `job-plugin-api-inventory.md` records the script-side interfaces; the consolidated specification must resolve the open policies below before code is written.

## Evidence boundary

The observations below come from this repository and the XML reference documentation of the already restored NeoLua 1.3.19 package. External Lua scripts remain in their original user-supplied directories. No external script bodies, editor binaries, voices, preset definitions or proprietary synthesis assets are included here.

Public API identifiers and data-field names describe an interoperability contract. A matching identifier does not establish equivalent acoustic behaviour. In particular, the same names for velocity, dynamics, breathiness and vibrato do not make the two synthesis systems interchangeable.

## Existing model facts

- `OpenUtau.Core/Ustx/UProject.cs:45` fixes resolution at 480 ticks per quarter. Its tempo entries are absolute project ticks and BPM (`:9–18`); time signatures are stored by bar number and numerator/denominator (`:21–32`). `GetTuning` selects a track override or the project tuning (`:36–38`).
- `OpenUtau.Core/Ustx/UNote.cs:14–35` stores note position relative to its part, integer adapter tone and an optional exact `PitchAddress`. `RefreshPitch` resolves the address, then derives the adapter tone. `AdjustedTone` additionally includes the note's integer-cent `tuning` (`:50–54`). The bridge must not treat `tone` as an EDO degree.
- `OpenUtau.Core/Ustx/UPart.cs:40–48` stores voice-part notes in `SortedSet<UNote>` and part-wide curves in a separate list. Changing an existing note position requires removal and reinsertion; the sort key includes position (`UNote.cs:96–104`).
- `OpenUtau.Core/Ustx/UCurve.cs:10–17` uses part-local integer ticks and integer values. Its declared editing interval is five ticks. `Sample` linearly interpolates between stored points and returns the expression default outside them (`:53–66`); this is not necessarily the same as a JOB control-event cursor.
- `OpenUtau.Core/Ustx/UNote.cs:269–284` deep-clones pitch addresses, note pitch data, vibrato and expression/override lists. Generated relationships, validation state and render products are not a replacement for a detached working model.
- `OpenUtau.Core/Render/RenderPhrase.cs:267–308` builds physical-cent pitch from resolved notes, vibrato and note portamento. It subsequently adds the part's `pitd` curve in cents (`:432–438`). Copying a final or absolute pitch contour into `pitd` would count the base pitch and existing note modulation twice.
- Default expression definitions are in `OpenUtau.Core/Format/Ustx.cs:42–73`. `dyn` is in tenths of decibels, `pitd` in cents, `vel` is 0–200 with neutral 100, and gender/breathiness curves use different neutral values and ranges from their similarly named VOCALOID controls. Renderer support is checked before a curve is used (`RenderPhrase.cs:443–468`).
- `OpenUtau.Core/Ustx/UPhoneme.cs:208–220` resolves expressions per generated phoneme index, with track defaults as fallback. OpenUtau has no universal scalar note velocity independent of phonemisation. Its velocity changes consonant stretch/preutterance (`:112–115`); it is not note loudness.

## Coordinate and execution contract

Use a detached execution session containing one voice part, its notes, relevant curves, a read-only tempo/time-signature snapshot and the effective tuning. Expose only plain Lua values and tables, never live C# project, singer, note, curve or file-system objects.

All note and part-control positions exposed to a JOB script are part-local 480-PPQ ticks. To ask the global `TimeAxis` for tempo, milliseconds or time signature, add the source part's project position first. Convert back only at the API boundary. Never change the project's fixed resolution, part position or tempo map merely to emulate an API.

`processParam.beginPosTick` and `endPosTick` need an explicitly documented half-open range `[begin, end)`. Proposed application policy is the selected note span when notes are selected, otherwise the active range selection if present, otherwise the whole part. `songPosTick` is the project playhead minus the part origin and may fall outside that range. This is an OpenUtau invocation policy, not evidence of VOCALOID's selection rules. The final specification must state whether the API can enumerate all part notes while the process range merely guides the script; scripts often filter their own ranges.

Assign session-local opaque note identities on export. They remain stable through note updates and iterator resets, including changes of pitch, lyric, position or duration. New notes receive fresh identities. A removed identity cannot update a different note later. Do not expose `GetHashCode` as a persistent note identifier, or identify a note only by position: the model permits separate note objects with equal starts.

Cursor enumeration must have documented ordering and mutation semantics. A robust design snapshots the enumeration identities at each seek, skips subsequently removed items and does not revisit an updated item. This supports scripts that remove events while iterating without relying on `SortedSet` enumerators invalidated by mutation. The note returned to Lua is a copied table, not the stored mutable object.

## Notes: proposed write rules

Map `posTick`, `durTick` and lyric text to note position, duration and lyric. Validate finite integral ticks, non-negative positions, positive durations and supported limits before commit. OpenUtau validation expands duration to at least ten ticks (`UNote.cs:140–146`); the bridge must either reject shorter events or document that minimum rather than claim an exact one-tick insertion.

For an existing note whose exported `noteNum` was not changed, retain its exact `PitchAddress`, residual cent information, spelling and `tuning` byte-for-byte. A lyric-only or timing-only plug-in must never quantise a 53-EDO or just-intonation note to twelve-tone MIDI.

For an existing note whose integer `noteNum` changed by `d`, the proposed microtonal policy is to transpose its resolved physical pitch by `100*d` cents and resolve a new address using `PitchResolver.NearestAddress(..., preserveExactPitch: true)` or its actual repository equivalent. Do not move by `d` tuning degrees. Preserve the pre-existing integer-cent `tuning` separately so it is not added twice. This is a bridge policy: a MIDI-only script cannot express a new exact rational spelling.

For an inserted note, `noteNum` denotes a twelve-tone equal-tempered MIDI pitch at the MIDI frequency reference; resolve it into the effective tuning while retaining residual cents. Construct it with `UProject.CreateNote` (`UProject.cs:127–143`) so it gets fresh OpenUtau pitch defaults. Do not clone unrelated source curves, cached phoneme indexes or override lists.

VOCALOID phoneme strings are not UTAU aliases or guaranteed phonemiser hints. Reading a source phoneme string cannot be implemented by fabricating aliases from lyrics. An unchanged opaque compatibility field can remain in the detached session, but an explicit phoneme rewrite requires a documented phonemiser-specific mapping or an unsupported-operation result. Storing an otherwise unrepresented phoneme string permanently would introduce a persistence design decision; it is not achieved by returning API success.

Velocity requires an approximation policy, not an alias to `vol`. A possible optional conversion anchors VS velocity 64 to native `vel=100` and maps the two halves of the VS range separately to native 0–100 and 100–200. Reverse conversion must use the same rounding rule. This assumes a VS 0–127 range that must be verified from API documentation; local source usage establishes the common neutral 64 only. When a note has heterogeneous phoneme velocities, a scalar export is lossy. The first implementation should either reject modifying that scalar on such a note, or explicitly document that the change applies to every generated phoneme. A no-op script must retain the original per-phoneme values exactly.

## Pitch controllers: PIT and PBS

PIT and PBS form one logical pitch system. With the conventional contract, the physical bend is `100 * PBS * PIT / 8192` cents. Verify the API's endpoint/range rules before adopting this as a normative formula. The positive PIT endpoint is conventionally 8191, not a symmetric +8192. PBS zero is a real special case and must not cause division by zero.

The OpenUtau target is the additive physical-cent `pitd` curve, not `tone`, `pitchAddress`, `tuning`, note portamento or vibrato. Read the existing `pitd` only. Existing portamento and vibrato must not be baked into the controller view and then added again on commit.

OpenUtau has no persistent PBS lane. A proposed session policy is to synthesise PBS from a documented neutral, increasing it when necessary to represent existing cent values without saturation; the detached session then stores both raw PIT and PBS events while the script runs. Recombine their final physical-cent values only when either was actually changed. Writing PBS alone intentionally rescales the raw PIT bend. Do not discard a PBS edit merely because `pitd` was not directly addressed.

Unmodified imported points must be a true no-op: preserve their original integer-cent data rather than round-trip them through quantised PIT. For edited points, use one stated rounding rule, test at the PIT extrema, and reject an out-of-range physical-cent result rather than silently clamp it. The default `pitd` descriptor is ±1200 cents. Widening it to accommodate larger PBS is a project-expression change affecting other tracks and saved files; that should be a separate explicit design, not an incidental plug-in side effect.

The script's event semantics and native curve interpolation must be reconciled. Local callers alone do not establish whether `VSGetControlAt` holds the preceding event or interpolates. Native `UCurve.Sample` is linear and defaults outside the stored extent. Until the public contract is verified, neither behaviour should be claimed equivalent. Any conversion needs guard samples at the edit-range boundaries so untouched pitch outside that range remains unchanged, including the part tail. Sampling every five ticks can be an explicitly bounded approximation, but it cannot be called an exact event round-trip.

## Other controllers and renderer capability

Supported storage is not sufficient evidence of audible support: `RenderPhrase` queries each renderer. Determine capability for the active track before running a plug-in that writes an expression.

- `DYN`: a candidate is a documented gain conversion into native tenths of decibels, with the VS neutral mapped to native zero and VS zero mapped to mute. A logarithmic mapping such as `200*log10(value/neutral)` is an OpenUtau approximation; it must not be described as verified VOCALOID gain law without evidence.
- `GEN`: the native `genc` curve is neutral zero with range −100…100; a VS bipolar controller would require a neutral-preserving scale, plus a renderer support check. Equal numbers are not equal voice timbres.
- `BRE`: native `brec` is a renderer-dependent −100…100 curve; native `bre` is a per-phoneme 0…100 resampler flag. The compatibility specification must choose one target and document the difference. Do not update both.
- `BRI`, `CLE`, `OPE` and `POR`: there is no verified one-to-one host expression. Do not silently redirect brightness to tension, opening to volume, or portamento amount to an arbitrary pitch curve. Return a named unsupported capability until a tested mapping exists.
- Singer-specific or V4 features such as cross-synthesis and growl similarly require explicit capability handling. A native field with a similar name is not proof of equivalent semantics.

For unsupported writes, return the API's failure code and latch an execution diagnostic. If the script ignores the failure, the host must not report the whole edit as fully successful. Proposed strict mode aborts the entire transaction, preserving the original project. Optional approximation modes, if added, should be named in the plug-in's compatibility report before execution rather than discovered by silent data loss.

## Extended note and note-expression APIs

`UVibrato` is one normalised envelope with a millisecond period, cent depth, fade percentages, phase percentage, drift and volume link (`UNote.cs:305–348`). The VS vibrato type is a catalogue identifier and can carry rate/depth envelopes. It cannot be faithfully mapped to a native preset number because OpenUtau has no corresponding catalogue.

Proposed first-tier support is basic note APIs plus unchanged preservation of all source native vibrato data. Extended-note reads may expose explicitly documented neutral values only for absent native concepts; writes of a non-neutral unrepresentable value must fail. Native vibrato length can be mapped to a percentage only when type/envelope semantics have also been resolved. Do not claim support for a type identifier merely by setting `UVibrato.length`.

Variable vibrato-depth/rate note-expression points could in principle be sampled into an additive-cent curve, but the adapter would have to disable or subtract the corresponding native vibrato and respect changing tempo. That is a separate approximation design with precision tests, not the basic note bridge. A shaped note-expression envelope cannot be represented faithfully by a single native scalar.

Preserving unsupported values in session memory is useful for read-modify-write within one execution, but is not persistent compatibility. The final API capability manifest should distinguish native read/write, approximated read/write, session-only opaque values, and unsupported operations. Plug-ins requiring note-expression editing should not be labelled supported merely because they can be listed in the menu.

## Part, singer, tempo and time signature APIs

- Musical-part name and comment can map to `UPart.name` and `comment` (`UPart.cs:17–18`). Part length maps to the voice-part duration. Position changes and trims must also update local notes and curves consistently; `ResizeVoicePartCommand` already demonstrates the necessary coordinate shift (`PartCommands.cs:61–91`). A trim cannot leave curves at their previous project locations.
- Singer reads may expose a synthetic host identity and display name from `UTrack.Singer` (`UTrack.cs:79`; `USinger.cs:218–220`). VOCALOID bank IDs, language numbers and synthesis-preset defaults cannot be inferred from an arbitrary UTAU/DiffSinger/VOICEVOX singer. Scripts branching on those values need a declared compatibility policy; fabricated Yamaha IDs would be misleading.
- Read-only tempo enumeration can use the project tempo snapshot. A script-side time query at local tick `t` must query the project at `part.position+t`. Source plug-in evidence from `AutoVibrato.lua:273–295` uses ordinary BPM, not BPM multiplied by 100. The exact script field contract is recorded in the inventory.
- Time-signature enumeration needs conversion from the project's bar positions into absolute ticks using `TimeAxis.BarBeatToTickPos`, then back into part-local ticks. The signature in effect at the part's start must be available even when its global event precedes the part. `TimeSignatureAtTick` and `TimeSignatureAtBar` already identify the governing signature (`TimeAxis.cs:193–208`). Verify whether the API field encodes a denominator value or an exponent before exposing it.
- Time/tempo writes are not required by the supplied scripts' observed API inventory. Keep them read-only until a specified transaction can include project-wide tempo changes and revalidate every affected part.

## Dialogs and application integration

The existing legacy UTAU plug-in path is separate: `PianoRollViewModel.cs:236–273` enumerates `DocManager.Plugins`, and `Classic/PluginRunner.cs` exchanges UST data. JOB Lua APIs should have their own loader/menu and should not impersonate executable UTAU plug-ins.

Preferences can add an ordered list of JOB script directories. Canonicalise paths, remove duplicate roots, tolerate missing/unreadable directories individually and keep the preference even when a removable path is temporarily absent. The user supplied four initial local folders; this is configuration data, not a list to hard-code for every installation. The existing singer path controls in `PreferencesDialog.axaml:139–163` are a layout reference, but they currently provide a single additional path and do not fulfil a multiple-JOB-folder requirement.

Dialog definitions must be copied when `VSDlgAddField` is called because scripts reuse and mutate a single Lua table between fields. Map integer, Boolean, floating, string and list fields to separate editable controls with declared validation. Return the API's modal return codes exactly; Cancel must discard the whole edit, including mutations made before the dialog. UI creation, modal calls, menu icons and message boxes must use the UI dispatcher—the recent grid crash demonstrates why a worker-created view model is not sufficient thread affinity.

Run script work outside the UI thread. The runner may synchronously await a dialog response through an explicit request channel, but the UI must remain responsive and the script session must still be cancellable. A plugin-generated message is untrusted display content, not an instruction for the host agent or an authorisation to open arbitrary URLs or execute commands.

## Transaction, validation and cache invalidation

All script APIs operate on the detached session. A nonzero main return, exception, Cancel, timeout, unsupported write or failed final validation discards it without touching the project and without creating an undo entry. Before commit, verify that the active project/part and the source revision still match; do not overwrite notes the user edited while a plug-in dialog was open.

On success, compute a minimal change set. Preserve original note references for unchanged notes; invalidate stale source phoneme overrides/indexes when lyric or duration transformations make them inappropriate. New notes use fresh defaults and explicit mapped fields. Reinsert moved notes in sort order. Preserve part data outside the permitted operation and retain the user's original selection where the affected notes still exist.

Use one main-thread undo group, with deferred validation if multiple dependent edits are needed. `DocManager.ExecuteCmd` requires a live undo group and dispatches to its main thread (`DocManager.cs:208–277`). `EndUndoGroup` validates deferred groups and triggers pre-render (`:302–330`); `RollBackUndoGroup` unexecutes commands but does not itself close the group (`:333–349`). Code must close the group after rollback, including exceptional paths.

A new whole-part replacement command must participate in the same notification and expression-refresh mechanisms as ordinary note/curve commands. `ScheduleRealCurveRefresh` recognises `ExpCommand` (`DocManager.cs:284–300`); using an unrelated command base without checking its cache effects may leave generated curves stale. Test actual renderer cache invalidation, not only collection equality. Do not call `Project.Validate` repeatedly while the script is half-way through a multi-step transformation.

## Interpreter and execution boundary

`OpenUtau.Core/OpenUtau.Core.csproj:21` already references NeoLua 1.3.19, but no existing repository runtime use was found. Package presence alone does not establish safe JOB hosting or Lua 5.1 compatibility.

The installed package XML documents `LuaCompileOptions.ClrEnabled`, `Sandbox`, `DynamicSandbox` and `LuaSandboxResult.Restrict` (`Neo.Lua.xml`, net6.0, lines499–546). A candidate implementation can disable CLR syntax and deny access to non-whitelisted members/types. It must prove that ordinary Lua values and delegates cannot recover reflection, runtime types, host objects or unrestricted global libraries. No project or singer object should cross that boundary even if the scripting engine offers automatic CLR interoperability.

Expose an allow-list of Lua language libraries actually needed by the supplied scripts. Reintroduce legacy helpers such as `table.getn` only as original compatibility code. Do not expose unrestricted `io`, `os`, `debug`, `package.loadlib`, process execution, arbitrary `loadfile`/`dofile`, network access or CLR imports. If script-relative module loading is later required, resolve it strictly inside authorised plug-in roots without writing those roots, and record that added capability.

Cancellation via `Task.WhenAny` does not stop an infinite Lua loop or terminate generated CLR code. A hard execution bound needs a tested interpreter instruction budget or a separate killable worker process. Metadata scanning is affected too: arbitrary top-level Lua or `manifest()` can loop indefinitely. Static manifest extraction with a filename fallback avoids executing unknown source during discovery, but dynamic manifests must then be reported as unresolved rather than silently treated as verified. The consolidated specification must choose this execution boundary before implementing runtime evaluation.

## Acceptance cases required by the mapping

- A no-op script leaves a 53-EDO note's exact address, spelling, residual cents, tuning, expressions and curves byte-for-byte equivalent; timing-only updates also preserve them.
- Semitone transposition moves physical frequency by the specified interval in 12-EDO, 53-EDO and a non-octave tuning; newly inserted MIDI notes get the documented residual representation.
- Note iteration remains deterministic while deleting, moving and inserting notes at equal positions. A script retaining an old table cannot accidentally update the next note.
- A part with a nonzero project origin, tempo changes and changing time signatures yields correct local event positions and physical timing.
- PIT/PBS no-op, scaling, zero sensitivity, positive/negative extrema, out-of-range bends, pre-existing vibrato/portamento and edit-boundary guards do not double pitch or damage outside data.
- A renderer that lacks an expression causes a truthful unsupported-operation result; no hidden conversion to a similarly named expression occurs.
- Cancel after an earlier mutation, Lua error, unsupported write, timeout and source revision conflict all leave the project and undo queue untouched.
- One successful operation has one undo entry; undo/redo restores notes, curves, part metadata and generated-render invalidation consistently.
- Metadata enumeration and scripts cannot access CLR reflection, arbitrary files, processes or networking. An infinite manifest and an infinite main can be terminated without freezing the editor.
- Repeated menu discovery and dialogs work when the runner/view model is created on a worker. Multiple configured roots, duplicate paths, missing roots, duplicate plug-in names and one invalid script do not prevent other plug-ins from appearing.

## Decisions still requiring evidence or an explicit scope choice

1. Exact VS control interpolation, iterator mutation rules, value ranges and return codes beyond what scripts exercise.
2. Whether first delivery implements only lossless note/timing APIs, or also explicitly approximated control conversion. Discovery coverage and execution coverage must be reported separately.
3. The fixed/synthetic PBS policy and whether bends outside the current native descriptor are rejected or supported through an explicit expression-range change.
4. Phoneme-string, singer-language, vibrato-type and per-note-expression capabilities. No one-to-one native representation has been established.
5. The interpreter execution boundary, enforceable instruction/time limits and permitted script-relative helper loading.
