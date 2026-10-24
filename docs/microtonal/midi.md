# MIDI input implementation record

OpenUtau Xen uses the existing DryWetMIDI dependency for Windows MIDI input. The device-independent `MidiInputState` uses the same `PitchResolver.MapMidi` function as MIDI-file import. Pitch addresses and tuning are captured at key-down, including Scala unmapped-key behaviour. Preview uses the resolved frequency rather than the compatibility MIDI integer.

## Implemented behaviour

- Device selection and refresh; all-channel or single-channel input.
- Note On/Off, velocity-zero Note On, per-channel sustain (CC64), All Sound Off (CC120), Reset Controllers (CC121), and All Notes Off (CC123).
- Repeated key-down closes the previous voice before retriggering. Device stop, channel changes, disposal, and hot-unplug flush active notes.
- Monotonic timestamps from one stopwatch. Recording begins at the project play cursor, at or after the current part start, and converts elapsed milliseconds through the project's tempo map.
- Recorded notes preserve microtonal addresses and are committed as one undo group when recording stops. Sustained durations include pedal time. A project change discards a pending take rather than inserting into another project.
- Preview reference-counts duplicate frequencies across channels so releasing one note cannot prematurely stop another.
- Mapped pitches outside the engine's MIDI-coordinate range retain their original address and unbounded compatibility integer in the recording. Preview skips them with a frequency/range message; no actual pitch is clamped.
- Device errors/removal schedule native shutdown outside the driver's callback. Native stop/dispose runs without holding the event-processing lock. Session identifiers reject delayed callbacks after reconnect.

## Verification and limits

`MidiInputTest` exercises the note lifecycle independently of native devices. A physical MIDI device and Windows audio output are required for real-device acceptance; software tests do not establish hardware timing or audio correctness.

The automated scenarios cover per-channel sustain, panic, retrigger, zero-velocity Note On, channel changes, idempotent shutdown, unmapped KBM keys, tuning snapshots, tempo changes, part-relative positions, and retention of out-of-range mapped notes. Final suite execution is serialised by the coordinating agent to avoid Windows testhost locking shared output files.

The initial recorder captures a take into the open voice part without a count-in, quantisation, or automatic backing-track transport. Polyphonic input is retained; normal OpenUtau overlap validation still applies. Pitch bend and MPE interpretation are outside this initial MIDI controller implementation. Preview follows the existing Play Tone preference. Changing the tuning while a device is connected requires reconnecting, so held notes cannot change pitch unexpectedly.

The device wrapper subscribes to DryWetMIDI removal and error events, stops listening, disposes the handle, and flushes state. The exact device and driver used for hot-unplug testing must be added to the release verification record.

Resource inspection on 24 October 2026 found an empty `Documents/OpenUtau/Singers` directory and several classic resampler executables. No voicebank was located in that default singer directory, so this inspection does not establish actual singer-engine acceptance.
