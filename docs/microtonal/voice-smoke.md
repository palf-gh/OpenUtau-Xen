# Real voice synthesis checks

## Local assets

The person running the check supplies every asset; the repository names none.

- Classic: a UTAU voicebank with a sustained Japanese /a/ oto, and a UTAU folder
  containing `resampler.exe` and its DLL.
- DiffSinger: a voicebank configuration folder with its acoustic model, and the
  vocoder model that voicebank expects.

The recorded results used one licensed single-note Japanese UTAU voicebank and one
licensed DiffSinger voicebank with a HiFi-GAN vocoder. These assets remain external
and are not committed or redistributed.

The test copies the selected Classic recording, oto parameters and any existing
FRQ into an isolated working bank. Generated metadata is written only to that
bank or the development cache, never back to the original voice directory.
DiffSinger models are read from their original locations; a working vocoder
configuration supplies the model path without editing the source bank.

## Results (2026-10-24)

All three real-engine tests passed, including a rerun with an empty render cache. Each synthesises 440 Hz, the adjacent
53-EDO pitch (445.7922229309625 Hz), and the just 5/4 pitch (550 Hz).

- Classic: errors +0.149, +0.261 and -0.418 cent.
- Worldline-R: errors +0.300, +0.213 and -0.515 cent.
- DiffSinger: errors +0.338, +0.267 and -0.196 cent (first run); rerun with an
  empty test-host render cache: +0.600, +0.746 and -0.297 cent.

Measured using normalised autocorrelation with interpolated lag in a stable
central voiced segment. The acceptance window is +/-5 cents for this smoke
check. This does not replace listening, broad voice/model coverage, transitions,
inferred-pitch/retake checks or the Classic adapter's separate integer-cent test.

Tests are opt-in through OPENUTAU_XEN_CLASSIC_BANK, OPENUTAU_XEN_DIFFSINGER_BANK,
OPENUTAU_XEN_DIFFSINGER_VOCODER, OPENUTAU_XEN_UTAU_ENGINE and
OPENUTAU_XEN_SMOKE_OUTPUT. The script sets OPENUTAU_XEN_SMOKE_ISOLATED and uses
a dedicated filtered test process so DocManager initialisation cannot affect
other tests. Preferences and the UI callback are restored in finally. Tools are
staged in a unique test-only resampler subdirectory and removed afterwards;
existing installed resamplers are never overwritten. Any unset asset setting
means explicitly skipped external tests. See `tools/verify-xen-voices.ps1`.

ENUNU, Vogen and VOICEVOX assets were not located in the supplied directories.
Their shared F0 paths were inspected, but actual synthesis is unverified. A first
release satisfying the full plan cannot be declared complete until those engine
families and real MIDI hardware are verified too.
