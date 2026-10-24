# Agent instructions for OpenUtau Xen

OpenUtau Xen is a fork of upstream OpenUtau for microtonal and multi-tuning
singing synthesis, published at https://github.com/palf-gh/OpenUtau-Xen and
developed with collaborators. The plan and acceptance criteria are in
`docs/microtonal/PLAN.md`; progress is in `docs/microtonal/PRODUCTION.md`.
Read both before starting work.

## Documents and writing

- **Always use the yomiyasu skill for documents.** Any Japanese prose you write
  or update (README, `docs/`, records, release notes, PR descriptions, UI copy
  explanations) must be polished with yomiyasu (`/yomiyasu` in Claude Code,
  `$yomiyasu:yomiyasu` in Codex) and pass its `run`/`verify` check before it
  is committed. A contributor without yomiyasu marks the Japanese text in the pull
  request; the maintainer runs yomiyasu on it before merging into `develop`.
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

- Follow `DESIGN.md` for colours and appearance: keep upstream OpenUtau's layout;
  brand lime replaces upstream pink as the main colour and a UI-adjusted deep blue
  replaces its light blue. Never put white text on brand lime. A new theme colour
  goes into the light, dark and custom themes and the theme editor.

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

## Versioning

- OpenUtau Xen has its own version, `MAJOR.MINOR.PATCH`, independent of upstream
  OpenUtau. It starts at `0.1.0`.
- Stay on `0.x` until every first-release check in `docs/microtonal/PLAN.md` passes;
  the accepted first release is `1.0.0`.
- Bump MINOR for new features or user-visible behaviour changes (on `0.x`, also for
  breaking changes). Bump PATCH for fixes only. After `1.0.0`, bump MAJOR when
  existing `.ustxen` or `.tunxen` files can no longer be opened as before.
- Bump during development whenever `develop` reaches a state worth sharing with
  testers: MINOR when it adds features, PATCH when it only fixes. Do not bump on
  every commit. Merging upstream changes does not bump the version by itself.
- To bump: change `Version`, `CFBundleVersion` and `CFBundleShortVersionString`
  together in `OpenUtau/OpenUtau.csproj`, record what the version contains in
  `docs/microtonal/PRODUCTION.md`, merge to `develop`, then fast-forward `main` to
  that commit and add an annotated tag `vX.Y.Z`.
- The `.ustxen`/`.tunxen` format versions are separate: change them only when the
  file format changes, with a migration and a round-trip test.

## Git

- `develop` is the default branch and integrates all work. Branch from it as
  `feature/<topic>` or `fix/<topic>` and open a pull request back into `develop`;
  the pull-request tests must pass before merging.
- `main` points to the latest versioned commit and only moves by fast-forward when
  a version is tagged. Never commit to `main` directly.
- The upstream fork point is tagged `upstream-base`. Bring upstream updates in as
  separate changes from the `upstream` remote.
- Keep commits small and coherent, and make each one build.
- Agents do not push, tag, change repository settings or publish without the
  maintainer's explicit authorisation. Push branches and tags by name; never push
  upstream's tags or local backup branches.
- Never declare the first release complete while any check in `PLAN.md`
  remains unverified. ENUNU, Vogen and real MIDI hardware are waived by the
  user and recorded as unverified; VOICEVOX is still required.
