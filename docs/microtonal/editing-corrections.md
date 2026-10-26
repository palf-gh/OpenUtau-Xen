# Editing corrections — 2026-10-26

## Report

The user found that the tuplet menu did not match the requested VOCALOID4 interaction, and that splitting with S left a copied pitch curve before the new note. The earlier shortcut implementation was separate from the knife tool and cloned the complete source curve. Translating its points did not remove the portion before the split.

## Intended correction

The snap menu exposes triplets, quintuplets and septuplets directly, separated from the note values. A visible dot marks the active modifier. Selecting it again disables tuplets; selecting another switches to that modifier. Straight and dotted note values remain explicit choices.

The shortcut and knife use one shared split implementation. The right fragment uses the knife's fresh pitch data rather than cloning the source curve. Exact microtonal pitch and the appropriate expression data are retained. Undo restores the original note and removes the new fragment.

Build, regression and trial-package verification are recorded in `PRODUCTION.md`. Native interactive review remains distinct from headless verification.
