using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Threading;
using OpenUtau.Core;
using OpenUtau.Core.Microtonal;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;

namespace OpenUtau.App.ViewModels {
    public sealed class MidiInputViewModel : INotifyPropertyChanged, IDisposable {
        readonly NotesViewModel notesView;
        readonly object recordingLock = new object();
        readonly List<MidiInputNote> recorded = new();
        readonly Dictionary<long, double> previewNotes = new();
        readonly Dictionary<double, int> previewCounts = new();
        MidiInputDevice? input;
        UProject? recordProject;
        UVoicePart? recordPart;
        double recordStartMilliseconds;
        double recordStartProjectMs;
        bool recording;
        bool disposed;
        int generation;

        public event PropertyChangedEventHandler? PropertyChanged;
        public IReadOnlyList<MidiInputDeviceInfo> Devices { get; private set; } = Array.Empty<MidiInputDeviceInfo>();
        public bool IsConnected => input != null;
        public bool IsRecording { get { lock (recordingLock) { return recording; } } }
        public bool Preview { get; set; } = true;
        public string Status { get; private set; } = XenStrings.Get("midi.disconnected");

        public MidiInputViewModel(NotesViewModel notesView) {
            this.notesView = notesView;
            RefreshDevices();
        }

        void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

        public void RefreshDevices() {
            try {
                Devices = MidiInputDevice.GetDevices();
                if (!OperatingSystem.IsWindows()) { Status = XenStrings.Get("midi.windowsOnly"); }
                else if (Devices.Count == 0) { Status = XenStrings.Get("midi.noDevices"); }
            } catch (Exception e) {
                Devices = Array.Empty<MidiInputDeviceInfo>();
                Status = e.Message;
            }
            Notify();
        }

        public void Connect(MidiInputDeviceInfo device, int? channel) {
            Disconnect();
            var project = DocManager.Inst.Project;
            var part = notesView.Part ?? throw new InvalidOperationException(XenStrings.Get("midi.openPart"));
            var tuning = project.tracks[part.trackNo].pitchSystem ?? project.pitchSystem;
            var candidate = new MidiInputDevice(tuning, channel);
            int session = ++generation;
            candidate.State.NoteStarted += note => OnNoteStarted(note, session);
            candidate.State.NoteEnded += note => OnNoteEnded(note, session);
            candidate.Disconnected += reason => OnDisconnected(reason, session);
            try {
                candidate.Connect(device.Index);
                input = candidate;
                Status = device.Name;
            } catch {
                candidate.Dispose();
                throw;
            }
            Notify();
        }

        public void SetChannel(int? channel) {
            if (IsRecording) { StopRecording(); }
            input?.State.SetChannel(channel, input.Milliseconds);
        }

        public void StartRecording() {
            if (input == null) { throw new InvalidOperationException(XenStrings.Get("midi.connectFirst")); }
            var project = DocManager.Inst.Project;
            var part = notesView.Part ?? throw new InvalidOperationException(XenStrings.Get("midi.openPart"));
            // Do not pull an already-held note into a new take.
            input.State.AllNotesOff(input.Milliseconds);
            lock (recordingLock) {
                recordProject = project;
                recordPart = part;
                recordStartMilliseconds = input.Milliseconds;
                recordStartProjectMs = project.timeAxis.TickPosToMsPos(Math.Max(part.position, DocManager.Inst.playPosTick));
                recorded.Clear();
                recording = true;
            }
            Status = XenStrings.Get("midi.recording");
            Notify();
        }

        public void StopRecording() {
            if (!IsRecording) { return; }
            input?.State.AllNotesOff(input.Milliseconds);
            MidiInputNote[] take;
            UProject? project;
            UVoicePart? part;
            lock (recordingLock) {
                recording = false;
                take = recorded.ToArray();
                recorded.Clear();
                project = recordProject;
                part = recordPart;
            }
            if (project != null && part != null && ReferenceEquals(project, DocManager.Inst.Project)
                && project.parts.Contains(part)) {
                var notes = MidiInputRecording.BuildNotes(project, part, take,
                    recordStartMilliseconds, recordStartProjectMs);
                if (notes.Count > 0) {
                    DocManager.Inst.StartUndoGroup("command.note.add");
                    DocManager.Inst.ExecuteCmd(new AddNoteCommand(part, notes));
                    DocManager.Inst.EndUndoGroup();
                }
                Status = XenStrings.Get("midi.recorded", notes.Count);
            } else {
                Status = XenStrings.Get("midi.discarded");
            }
            Notify();
        }

        void OnNoteStarted(MidiInputNote note, int session) => OnUi(() => {
            if (!Preview || disposed || input == null || generation != session) { return; }
            double frequency = MusicMath.ToneToFreq(note.MidiTone);
            if (note.MidiTone < 0 || note.MidiTone > 127) {
                Status = XenStrings.Get("midi.range", note.Key, frequency);
                Notify();
                return;
            }
            previewNotes[note.Id] = frequency;
            previewCounts.TryGetValue(frequency, out int count);
            previewCounts[frequency] = count + 1;
            if (count == 0) { PlaybackManager.Inst.PlayTone(frequency); }
        });

        void OnNoteEnded(MidiInputNote note, int session) {
            lock (recordingLock) {
                if (generation == session && recording && note.EndMilliseconds >= recordStartMilliseconds) {
                    recorded.Add(note);
                }
            }
            OnUi(() => {
                if (generation != session) { return; }
                if (!previewNotes.Remove(note.Id, out double frequency)) { return; }
                int count = previewCounts[frequency] - 1;
                if (count == 0) {
                    previewCounts.Remove(frequency);
                    PlaybackManager.Inst.EndTone(frequency);
                } else { previewCounts[frequency] = count; }
            });
        }

        void OnDisconnected(string reason, int session) => OnUi(() => {
            if (generation != session) { return; }
            StopRecording();
            var previous = input;
            input = null;
            previous?.Dispose();
            StopPreview();
            ++generation;
            Status = XenStrings.Get("midi.disconnected");
            Notify();
        });

        static void OnUi(Action action) {
            if (Dispatcher.UIThread.CheckAccess()) { action(); }
            else { Dispatcher.UIThread.Post(action); }
        }

        public void StopPreview() {
            foreach (var frequency in previewCounts.Keys.ToArray()) { PlaybackManager.Inst.EndTone(frequency); }
            previewNotes.Clear();
            previewCounts.Clear();
        }

        public void Disconnect() {
            StopRecording();
            var previous = input;
            input = null;
            if (previous != null) {
                previous.Dispose();
            }
            ++generation;
            StopPreview();
            Status = XenStrings.Get("midi.disconnected");
            Notify();
        }

        public void Dispose() {
            if (disposed) { return; }
            Disconnect();
            disposed = true;
        }
    }
}
