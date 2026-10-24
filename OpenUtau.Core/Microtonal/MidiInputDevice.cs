using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;

namespace OpenUtau.Core.Microtonal {
    public sealed class MidiInputDeviceInfo {
        public int Index { get; }
        public string Name { get; }
        public MidiInputDeviceInfo(int index, string name) { Index = index; Name = name; }
        public override string ToString() => Name;
    }

    /// <summary>Windows MIDI input with monotonic timestamps and guaranteed note flushing on shutdown.</summary>
    public sealed class MidiInputDevice : IDisposable {
        readonly object sync = new object();
        readonly Stopwatch clock = Stopwatch.StartNew();
        InputDevice? device;
        DevicesWatcher? watcher;
        bool disposed;
        public MidiInputState State { get; }
        public double Milliseconds => clock.Elapsed.TotalMilliseconds;
        public event Action<string>? Disconnected;

        public static IReadOnlyList<MidiInputDeviceInfo> GetDevices() {
            if (!OperatingSystem.IsWindows()) { return Array.Empty<MidiInputDeviceInfo>(); }
            var devices = InputDevice.GetAll().ToArray();
            try {
                return devices.Select((item, index) => new MidiInputDeviceInfo(index, item.Name)).ToArray();
            } finally {
                foreach (var item in devices) { item.Dispose(); }
            }
        }

        public MidiInputDevice(TuningDefinition tuning, int? channel = null) {
            State = new MidiInputState(tuning, channel);
        }

        public void Connect(int index) {
            if (!OperatingSystem.IsWindows()) {
                throw new PlatformNotSupportedException("MIDI input is available on Windows.");
            }
            InputDevice input;
            lock (sync) {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (device != null) { throw new InvalidOperationException("Disconnect the current MIDI device first."); }
                input = InputDevice.GetByIndex(index);
                device = input;
            }
            try {
                input.EventReceived += OnEvent;
                input.ErrorOccurred += OnError;
                watcher = DevicesWatcher.Instance;
                watcher.DeviceRemoved += OnRemoved;
                input.StartEventsListening();
            } catch {
                CloseDevice();
                State.AllNotesOff(Milliseconds);
                throw;
            }
        }

        void OnEvent(object? sender, MidiEventReceivedEventArgs args) {
            lock (sync) {
                if (disposed || device == null) { return; }
                var time = Milliseconds;
                switch (args.Event) {
                    case NoteOnEvent note:
                        State.NoteOn((int)note.Channel, (int)note.NoteNumber, (int)note.Velocity, time);
                        break;
                    case NoteOffEvent note:
                        State.NoteOff((int)note.Channel, (int)note.NoteNumber, time);
                        break;
                    case ControlChangeEvent control:
                        State.ControlChange((int)control.Channel, (int)control.ControlNumber,
                            (int)control.ControlValue, time);
                        break;
                }
            }
        }

        // Native stop/dispose must not run inside the driver's event callback.
        void OnError(object? sender, ErrorOccurredEventArgs args) =>
            Task.Run(() => Disconnect(args.Exception.Message));

        void OnRemoved(object? sender, DeviceAddedRemovedEventArgs args) {
            lock (sync) {
                if (device != null && args.Device is InputDevice removed && removed.Equals(device)) {
                    Task.Run(() => Disconnect("MIDI device disconnected."));
                }
            }
        }

        public void Disconnect(string reason = "Disconnected") {
            if (!CloseDevice()) { return; }
            State.AllNotesOff(Milliseconds);
            Disconnected?.Invoke(reason);
        }

        bool CloseDevice() {
            InputDevice? input;
            DevicesWatcher? oldWatcher;
            lock (sync) {
                input = device;
                device = null;
                oldWatcher = watcher;
                watcher = null;
            }
            if (oldWatcher != null) { oldWatcher.DeviceRemoved -= OnRemoved; }
            if (input == null) { return false; }
            input.EventReceived -= OnEvent;
            input.ErrorOccurred -= OnError;
            try { input.StopEventsListening(); } catch (Exception) { /* Hot-unplug can invalidate the handle. */ }
            try { input.Dispose(); } catch (Exception) { /* Notes must still be flushed after hot-unplug. */ }
            return true;
        }

        public void Dispose() {
            lock (sync) {
                if (disposed) { return; }
                disposed = true;
            }
            Disconnect();
        }
    }
}
