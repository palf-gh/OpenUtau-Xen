# Tuning core implementation record

## Model and precision

The tuning domain is implemented under `OpenUtau.Core.Microtonal`. Degree zero is the period origin (conventionally C4). A reference degree and a positive frequency anchor the tuning independently of its notation. The 53-EDO preset anchors degree 40 at 440 Hz. Reference frequency can be changed to any positive finite value.

Intervals retain either an exact normalised rational string or an explicit finite cents value. Explicit tunings include unison and exclude the final period. Continuous MIDI pitch remains a 440 Hz coordinate internally; the chosen reference frequency is applied once by `PitchResolver`.

Nearest-degree conversion compares physical cents distances, including across negative-period boundaries. Ties go to the lower degree. Preserving frequency records the difference in `ResidualCents`; retuning clears that residual. Existing note-specific tuning and curves are the responsibility of the project integration layer.

## Formats

`.tunxen` is a versioned YAML document with `format: OpenUtauXenTuning`, `version: 3` and a complete `tuning` definition, including the keyboard layout, keyboard MOS (with its repeat count for a multi-period MOS) and scale tonic. Version 2 is read with one repeat and version 1 is upgraded on reading (see `keyboard-mos.md`); other versions are rejected. Exact ratios, keyboard mapping holes and aliases round-trip.

Scala `.scl` import/export retains rational intervals and the final period. `.kbm` supports unmapped and repeated degrees, negative mapping cycles, formal periods distinct from the degree count and independent reference frequency. A formal octave of zero resolves to the imported tuning's degree count when applied. Native input mapping is exported as a finite 128-key Scala map so non-octave tunings retain their exact MIDI input mapping.

## Presets and notation

Equal divisions 12, 17, 19, 22, 24, 31, 41, 53 and 72, explicit 5/7-limit examples, fifth-chain Pythagorean and meantone definitions, Werckmeister III, Kirnberger III, Vallotti, Young II, 13-division Bohlen–Pierce and a clearly labelled 24-EDO Rast approximation are generated analytically. No external scale archive is copied. Presets are deep-cloned to prevent library mutations leaking into other projects.

The 53-EDO seven-note MOS uses the generator chain F–C–G–D–A–E–B, yielding degrees 0, 9, 18, 22, 31, 40 and 49. Mode rotation, degenerate equal-step subsets and early generator repetition are handled separately. A multi-period MOS repeats a shorter chain in each equal part of the tuning period (12-EDO 4L4s, 22-EDO 2L8s). Rational generators can create exact explicit tunings rather than silently quantising into an unrelated tuning.

Ups and Downs candidates are derived from the period's nearest fifth and preserve different sharp/flat degrees in 53-EDO. Verified Stein–Zimmermann mappings are provided for 24-EDO and 53-EDO, with semantic accidental IDs translated to glyphs only when formatting. The 53-EDO mapping uses tuning-specific offsets from the [53edo SZG table](https://en.xen.wiki/w/53edo#Stein%E2%80%93Zimmermann%E2%80%93Gould_notation); SMuFL names and codepoints are checked against [W3C metadata](https://github.com/w3c/smufl/blob/gh-pages/metadata/glyphnames.json). In 53-EDO, a glyph whose SMuFL name mentions a quarter tone does not imply an invariant 50-cent alteration. Other SZG mappings fall back to degree labels unless explicitly defined.

## Verification

Historical constructions are recorded in each preset's `Source` and `Description`. Werckmeister III narrows C–G, G–D, D–A and B–F-sharp by one quarter Pythagorean comma. Kirnberger III shares a syntonic comma among C–G–D–A–E and places the remaining schisma on F-sharp–C-sharp. Vallotti and Young II differ by the starting position of six fifths narrowed by one sixth Pythagorean comma; this distinction was cross-checked against [Ross W. Duffin's account](https://casfaculty.case.edu/ross-duffin/why-i-hate-vallotti-or-is-it-young-2-1/) and [Carey Beebe's tuning instructions](https://www.hpschd.nu/tech/tmp/young.html). These are explicit selected variants rather than claims that all historical practice used one universal tuning.

`MicrotonalCoreTest` checks arbitrary references, exact ratios, negative degrees, ties, residuals, all preset validation and cloning, YAML and Scala round-trips, KBM holes/duplicates/negative cycles, non-octave MIDI-map export and MOS generation. These tests validate mathematical and data boundaries; they do not substitute for the separate real-engine and voicebank audio verification required by the release plan.
