# Shared pitch and renderer integration review

Review date: 24 October 2026. This record distinguishes source review and synthetic integration tests from actual singing-engine acceptance.

## Shared phrase construction

`RenderPhrase` refreshes each note against its effective project/track tuning, constructs `RenderNote.adjustedTone`, and builds its absolute pitch curve in MIDI cents from `UNote.AdjustedTone`. The latter combines the resolved degree/reference/residual and existing per-note cent adjustment. Pitch points retain their existing 10-cent units and vibrato remains expressed in cents. PITD is added after `pitchesBeforeDeviation` is captured.

The pre-effect hash now includes the continuous adjusted pitch and note timing. This matters when adjacent 53-EDO degrees share the same rounded integer MIDI tone: phone-only hashes can remain equal, but inference and synthesis must not reuse the old phrase. The final hash additionally includes final pitch samples and applicable curves.

`XenRenderTest` constructs real `RenderPhrase` objects with a synthetic loaded singer and validated synthetic phonemes. The renderer fixture deliberately cannot synthesise audio. Tests cover:

- A4 = 432 Hz, degree 40 of 53-EDO, residual cents, and existing note tuning applied once.
- Adjacent 53-EDO degrees with identical legacy tone/phone hash but different inference/synthesis hashes.
- Reference-frequency changes within the same rounded semitone.
- Track reference overriding project reference without applying either reference twice.
- Contiguous-note microtonal transitions.
- PITD changing final pitch while preserving the base inference key.
- Legacy 12-EDO notes and fine tuning.
- Vibrato and pitch-point offsets preserving a single application of note tuning.

The coordinating agent runs builds and tests serially; the synthetic tests do not prove any external model's pitch accuracy.

## Renderer boundaries inspected

- **Classic:** `ResamplerItem` samples `phrase.pitches`, subtracts the integer phone tone, then rounds to integer cents for the resampler pitchbend. This introduces up to approximately half a cent of quantisation. The integer pitch token remains necessary for resampler and OTO compatibility.
- **Worldline:** `WorldlineRenderer` converts the absolute phrase curve to F0 with `MusicMath.ToneToFreq(cents * 0.01)` and uses that curve for synthesis. Both registered Worldline variants share this route.
- **ENUNU:** score labels remain integer phone tones, while the editing F0 supplied to the synthesis route comes from the absolute phrase curve. The grouped cache key includes `phrase.preEffectHash`, so a change to the continuous note pitch cannot silently reuse an earlier acoustic prediction.
- **Vogen:** `notePitches` uses `RenderNote.adjustedTone` in the float ONNX tensor. The acoustic F0 comes from the absolute phrase curve, while the F0 model also supplies voiced/unvoiced information. The phrase hash identifies its cached pitch result.
- **DiffSinger:** note, padding, and inserted-rest pitches use continuous `adjustedTone` in the float `note_midi` tensor. Final acoustic and variance paths sample the absolute phrase curve. Actual model behaviour, auto-pitch, and retake require an available DiffSinger voice model.
- **VOICEVOX:** the engine score still receives integer note tones; its synthesis parameter F0 is replaced from the absolute phrase curve. Group hashing includes the pre-effect hash. Input score shifts and singer-specific model ranges still require real-engine verification.

## Range handling and acceptance limits

Native project storage and MIDI capture retain out-of-range addresses. Numerical integer overflow is rejected during resolution. An explicit engine-boundary range check is also required before invoking integer-note score/resampler interfaces; silently clamping would change the intended pitch. Tone shifts and continuous pitch curves must be checked against applicable engine limits as well as the base note.

The general MIDI-coordinate range does not establish each voicebank's supported acoustic range. ENUNU/VOICEVOX score constraints, specific DiffSinger/Vogen models, resampler pitchbend limits, voiced/unvoiced masking, and the accuracy of measured output remain external acceptance checks. No actual singing-engine result is claimed in this source review.
