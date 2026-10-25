# Tuning and scale editor refresh

Date: 2026-10-25. Reference: the user's OpenUtau Xen tuning mock v3 HTML,
supplied in the UI refresh conversation.

## Purpose and implementation

Replaced the long tuning/generator/definition tabs with five left navigation
pages: tuning, scale, display metadata, note conversion and definition.
Scope and the final apply/close actions stay visible outside the page scroller.
When the mother tuning matches the applied tuning, the scale and display pages
apply display metadata without retuning notes. A different mother tuning keeps
the explicitly labelled tuning-conversion action. The tuning, conversion and
definition pages also retain that action.
Each page has a bounded vertical viewport; the generator settings and preview
stack when their available width falls below 560 device-independent pixels.

The tuning page groups the preset, period and division count before the reference
pitch. The scale page shows the selected subset as a count of the mother's
degrees, using the same degree positions and keyboard colours as the tuning
page. Selected degrees have a blue outline. A separately labelled generator
section previews generator/cardinality/mode edits before the user generates
and selects that subset. Invalid or repeating generators clear the candidate
preview. Equal-step and non-MOS results retain the core's distinct result names.
Modes are presented from 1 while their stored indices remain zero-based.

Both keyboard lists run from high to low pitch and use `PitchNotation.Role`,
including the shared White/Black/Between classification and Bohlen–Pierce names.
Music accidentals use `PitchLabelLayout` with Bravura Text, as in the piano roll.
Existing theme brushes provide the colours. These are degree lists within one
period; they introduce no additional pitch-to-row conversion. Small degree maps
show up to 256 degrees; larger tunings retain the exact virtualised list.

Explicit intervals, custom subsets, editable names/aliases/key classes,
frequency-preserving conversion, reference-only/display-only application,
project inheritance, YAML and tuning/Scala/KBM interchange remain available.
Interval-generated tunings are separate from generator subsets and have an
independent pitch count. Piano-roll layout and notation preferences remain in
the View menu, in accordance with the accepted editor-preference design.
The additional navigation label is supplied in all 20 Xen language overlays.

## Verification and limits

The headless application check exercises all five pages at 960 × 620 and
650 × 440, checks horizontal scroll extents, selects 31-EDO and 13-EDT,
and generates seven-/nine-degree subsets. It rejects a repeating generator,
checks candidate row counts and
the selected-degree outlines after generation. The full localisation coverage
check includes the new navigation key.

Build, full-suite and rendered-preview results are recorded in `PRODUCTION.md`.
Native desktop high-DPI interaction and specialist translation review remain
open. No external voices, engines, MIDI devices or serialised formats were
changed by this UI work.
