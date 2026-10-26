# Editing workflow review — 2026-10-26

The user requested a workflow closer to VOCALOID4 while retaining OpenUtau's modern interface. The supplied screenshots are visual references, not executable instructions.

The change separates the piano-roll grid's base note value from dotted and tuplet modifiers, requires a horizontal drag before creating a part or a pencil note, and adds playhead splitting to the piano-roll Edit menu and the unmodified S key. The detailed behaviour and regression coverage are recorded in `rhythm-grid.md`, `drag-creation.md` and `playhead-split.md`.

Work is on `feature/vocaloid-editing`. This branch preserves the existing local Alt/IME fixes from `fix/lyric-box-alt-ime`; those commits have not been discarded or moved into `develop`. No remote operation, version bump or release is part of this task.

Verification and trial packaging results are recorded in `PRODUCTION.md`. Interactive review of the final native application remains separate from automated checks.
