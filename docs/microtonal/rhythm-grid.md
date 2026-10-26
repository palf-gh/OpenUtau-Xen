# Piano-roll rhythm grid

Purpose: retain OpenUtau's modern workspace while expressing quantisation as a base note value, explicit dotted note values and direct triplet, quintuplet and septuplet toggles.

Implementation: the piano-roll snap menu offers straight powers of two from whole notes to 1/128. Modifiers combine. Grid positions are rounded individually from each bar origin, so septuplets do not accumulate integer-tick error; a final short interval ends at the next bar. Time-signature changes start a fresh grid. Direct snapping reads the time axis, independently of viewport size or rendered grid-line density. Part-local positions are translated through the project tick origin. Quantise uses the same grid and keeps notes at least one interval long when both ends would otherwise collapse. The track overview retains its existing automatic grid.

Verification: regression cases cover dotted-quarter remainder reset, septuplet rounding, independent modifiers and time-signature changes. The realised snap-menu check covers native separators, visible filled-circle icons and tuplet toggle/exclusion behaviour. Root integration runs the serial solution build and complete test suite. Japanese menu labels completed yomiyasu run and verify (run 20261025T233111Z-78ac28c7); the sole meaning-risk candidate is the intended numeric placeholder added to the tuplet label, checked against the requested 3-, 5- and 7-tuplet entries. The submitted labels match the checked hash. Interactive native verification remains pending.

The user declined an additional mode that subdivides a selected dotted interval. The menu offers the explicit note-base ratios 3:2, 5:4 and 7:4. No native-format or pitch representation changes are required.

## Background startup crash correction

A user report identified a cross-thread failure when opening the snap menu. Startup creates the piano-roll view model on a worker thread, and the menu builder had created Avalonia TextBlock icons and Separator controls on that worker. Assigning those controls to menu items later failed in MenuItem.IconChanged because their logical parent was changed from the UI thread.

UpdateSnapMenu now checks UI-thread access before creating any Avalonia control. Worker calls post the builder itself to the UI dispatcher; the callback reads the current rhythm selection rather than retaining an obsolete menu or controls. UI-thread calls still update immediately. The view model has no disposal lifecycle to synchronise here.

The regression constructs NotesViewModel with Task.Run, drains the dispatcher, opens the realised menu and checks its visible markers and native separators. It then repeats rhythm changes, zoom changes and menu reopening three times. The check isolates preferences and restores the original preferences in a finally block. Serial build, full tests and refreshed trial packaging are performed by the integrating agent; native interactive verification remains separate.
