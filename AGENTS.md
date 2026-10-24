# Agent instructions for OpenUtau Xen

OpenUtau Xen is a local derivative of upstream OpenUtau for microtonal and
multi-tuning singing synthesis. The plan and acceptance criteria are in
`docs/microtonal/PLAN.md`; progress is in `docs/microtonal/PRODUCTION.md`.
Read both before starting work.

## Documents and writing

- **Always use the yomiyasu skill for documents.** Any Japanese prose you write
  or update (README, `docs/`, records, release notes, PR descriptions, UI copy
  explanations) must be polished with yomiyasu (`/yomiyasu` in Claude Code,
  `$yomiyasu:yomiyasu` in Codex) and pass its `run`/`verify` check before it
  is committed.
- Use British English spelling, except for external identifiers (APIs, file
  formats, upstream names and keys such as `en-US`, `Color`).
- `README.md` is a single bilingual file: English first, then Japanese, with
  `[English](#english) | [日本語](#japanese)` links at the top. Keep both
  sections in step when either changes. Do not add a separate README per language.
- Record each change's purpose, implementation, verification and open issues in
  `docs/microtonal/` in the same commit as the change.

## Build and test

```powershell
dotnet build OpenUtau.sln -m:1
dotnet test OpenUtau.Test -m:1
```

- Build and test serially; parallel runs lock testhost files.
- Real-voice checks are opt-in: `pwsh -NoProfile -File tools/verify-xen-voices.ps1`
  (see `docs/microtonal/voice-smoke.md`). Clear the test-host `Cache` folder
  when recording results, so renders are not served from cache.
- Never edit, copy into the repository or redistribute the user's voicebanks,
  UTAU binaries or external engines.

## Code conventions

- Match the surrounding code's style, naming and comment density.
- Integer `tone` is only an adapter for phonemisers and sample selection; exact
  pitch lives in `PitchAddress` and is resolved by `PitchResolver`.
- Piano-roll geometry goes through `PitchRowMapper`; do not add separate
  pitch-to-row conversions.
- Xen UI strings belong in `OpenUtau/Strings/Xen.<culture>.axaml` for every
  registered language. Do not edit the Crowdin-managed upstream dictionaries.
  Serialised identifiers are never translated.
- Native formats (`.ustxen`, `.tunxen`) have their own identifier and version;
  never reuse upstream `ustx_version`.

## Git

- Work on feature branches; integrate tested commits into `develop`. `main`
  keeps the upstream baseline. Bring upstream updates in as separate changes.
- Keep commits small and coherent, and make each one build.
- Do not push, publish or create remote forks without explicit authorisation.
- Never declare the first release complete while any check in `PLAN.md`
  (including ENUNU, Vogen, VOICEVOX and real MIDI hardware) remains unverified.
