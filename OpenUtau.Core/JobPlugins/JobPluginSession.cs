using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MoonSharp.Interpreter;
using OpenUtau.Core.Microtonal;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;

namespace OpenUtau.Core.JobPlugins {
    /// <summary>Changes a successful run applies to the part as one undo group.</summary>
    public sealed class JobPluginEdit {
        public List<UNote> RemovedNotes { get; } = new List<UNote>();
        public List<UNote> AddedNotes { get; } = new List<UNote>();
        public Dictionary<string, (int[] xs, int[] ys)> Curves { get; } = new Dictionary<string, (int[] xs, int[] ys)>();
        public int? Position { get; set; }
        public int? Duration { get; set; }
        public string? Name { get; set; }
        public bool IsEmpty => RemovedNotes.Count == 0 && AddedNotes.Count == 0 && Curves.Count == 0
            && Position == null && Duration == null && Name == null;
    }

    /// <summary>
    /// Detached working copy of one voice part, exposed to Lua through the VS* API. Nothing in the
    /// project changes until <see cref="BuildEdit"/> is applied by the caller.
    /// </summary>
    public sealed class JobPluginSession {
        // Adapter defaults for VOCALOID note fields without an OpenUtau equivalent.
        const int DefaultVelocity = 64;
        const int DefaultBendDepth = 8;
        const int DefaultAccent = 50;
        const int DefaultDecay = 50;
        const int DefaultOpening = 127;
        const int MinDuration = 10;
        // VOCALOID's melisma lyric and OpenUtau's extender both continue the previous vowel.
        const string Melisma = "-";
        const string Extender = "+";

        sealed class NoteFields {
            public int Pos, Dur, NoteNum, Velocity;
            public string Lyric = string.Empty;
            public string Phonemes = string.Empty;
            public int PhLock, BendDepth = DefaultBendDepth, BendLength, RisePort, FallPort;
            public int Decay = DefaultDecay, Accent = DefaultAccent, Opening = DefaultOpening;
            public int VibratoLength, VibratoType;
            public NoteFields Clone() => (NoteFields)MemberwiseClone();
            public bool SameAs(NoteFields o) => Pos == o.Pos && Dur == o.Dur && NoteNum == o.NoteNum
                && Velocity == o.Velocity && Lyric == o.Lyric && Phonemes == o.Phonemes && PhLock == o.PhLock
                && BendDepth == o.BendDepth && BendLength == o.BendLength && RisePort == o.RisePort
                && FallPort == o.FallPort && Decay == o.Decay && Accent == o.Accent && Opening == o.Opening
                && VibratoLength == o.VibratoLength && VibratoType == o.VibratoType;
        }

        sealed class NoteState {
            public long Id;
            public UNote? Source;
            public NoteFields? Original;
            public UNote? Template;
            public NoteFields? TemplateFields;
            public NoteFields Current = new NoteFields();
            public bool Removed;
            /// <summary>Phonemes carry different native velocities; a scalar cannot represent them.</summary>
            public bool VelocityLocked;
            public UVibrato Vibrato = new UVibrato();
            /// <summary>A vibrato type change replaced the preset and curves of <see cref="Vibrato"/>.</summary>
            public bool VibratoReset;
            public Dictionary<string, NoteExpressionLane> Expressions = new Dictionary<string, NoteExpressionLane>();
        }

        sealed class NoteExpressionEvent {
            public long Id;
            public double Pos, Value;
        }

        sealed class NoteExpressionLane {
            public List<NoteExpressionEvent> Events = new List<NoteExpressionEvent>();
            public List<NoteExpressionEvent> Original = new List<NoteExpressionEvent>();
            public List<long> Cursor = new List<long>();
            public int CursorIndex;
        }

        sealed class ControlEvent {
            public long Id;
            public int Pos;
            public int Value;
        }

        sealed class ControlLane {
            public string Type = string.Empty;
            public int Default, Min, Max;
            public bool Native;
            public bool Changed;
            public List<ControlEvent> Events = new List<ControlEvent>();
            public List<long> Cursor = new List<long>();
            public int CursorIndex;
        }

        static readonly (string type, int def, int min, int max)[] controlTypes = {
            ("PIT", 0, -8192, 8191), ("PBS", 2, 0, 24), ("DYN", 64, 0, 127), ("BRE", 0, 0, 127),
            ("BRI", 64, 0, 127), ("CLE", 0, 0, 127), ("GEN", 64, 0, 127), ("POR", 64, 0, 127),
            ("OPE", 127, 0, 127), ("XSY", 0, 0, 127), ("GWL", 0, 0, 127),
        };

        readonly UProject project;
        readonly UTrack track;
        readonly UVoicePart part;
        readonly TuningDefinition tuning;
        readonly JobPluginProcessParameters process;
        readonly IJobPluginInteraction? interaction;
        readonly List<NoteState> notes = new List<NoteState>();
        readonly Dictionary<string, ControlLane> lanes = new Dictionary<string, ControlLane>();
        readonly Dictionary<string, (int[] xs, int[] ys)> originalCurves = new Dictionary<string, (int[] xs, int[] ys)>();
        readonly UTempo[] tempos;
        readonly (int tick, int bar, int numerator, int denominator)[] timeSignatures;
        readonly bool dynSupported;
        readonly bool velSupported;
        readonly JobPluginDialog dialog = new JobPluginDialog();
        long nextId = 1;
        List<long> noteCursor = new List<long>();
        int noteCursorIndex;
        int tempoCursor = -1;
        int timeSigCursor = -1;
        int partPosition, partDuration;
        string partName;
        readonly string partComment;
        bool partDurationSet;

        public JobPluginRuntime? Runtime { get; set; }
        public bool Cancelled { get; private set; }
        public List<string> Log { get; } = new List<string>();

        public JobPluginSession(UProject project, UVoicePart part, JobPluginProcessParameters process, IJobPluginInteraction? interaction) {
            this.project = project;
            this.part = part;
            this.process = process;
            this.interaction = interaction;
            track = project.tracks[part.trackNo];
            tuning = project.GetTuning(track);
            partPosition = part.position;
            partDuration = part.Duration;
            partName = part.name;
            partComment = part.comment;
            var renderer = track.RendererSettings?.Renderer;
            dynSupported = renderer == null || !project.expressions.TryGetValue(Format.Ustx.DYN, out var dynDesc)
                || renderer.SupportsExpression(dynDesc);
            velSupported = project.expressions.TryGetValue(Format.Ustx.VEL, out var velDesc)
                && (renderer == null || renderer.SupportsExpression(velDesc));
            foreach (var note in part.notes) {
                var fields = Export(note, out bool locked);
                notes.Add(new NoteState {
                    Id = nextId++, Source = note, Original = fields, Current = fields.Clone(), VelocityLocked = locked,
                    Vibrato = note.vibrato.Clone(),
                });
            }
            foreach (var (type, def, min, max) in controlTypes) {
                lanes[type] = new ControlLane { Type = type, Default = def, Min = min, Max = max };
            }
            lanes["PIT"].Native = lanes["PBS"].Native = lanes["DYN"].Native = true;
            ExportPitch();
            ExportDynamics();
            tempos = project.tempos.OrderBy(t => t.position).Select(t => new UTempo(t.position, t.bpm)).ToArray();
            timeSignatures = project.timeSignatures.OrderBy(t => t.barPosition)
                .Select(t => (project.timeAxis.BarBeatToTickPos(t.barPosition, 0), t.barPosition, t.beatPerBar, t.beatUnit))
                .ToArray();
        }

        NoteFields Export(UNote note, out bool velocityLocked) {
            var vels = note.phonemeExpressions.Where(e => e.abbr == Format.Ustx.VEL).ToList();
            int phonemeCount = note.phonemeIndexes.Length > 0 ? note.phonemeIndexes.Last() + 1 : 1;
            float native = 100;
            velocityLocked = false;
            if (vels.Count > 0) {
                native = (vels.FirstOrDefault(e => e.index == 0) ?? vels[0]).value;
                velocityLocked = vels.Select(e => e.value).Distinct().Count() > 1 || vels.Count < phonemeCount;
            }
            bool vibrato = note.vibrato.length > 0;
            return new NoteFields {
                Pos = note.position,
                Dur = note.duration,
                NoteNum = note.tone,
                Velocity = NativeToVelocity(native),
                Lyric = note.lyric == Extender ? Melisma : note.lyric,
                VibratoType = vibrato ? note.vibrato.preset ?? 1 : 0,
                VibratoLength = vibrato ? (int)Math.Round(note.vibrato.length) : 0,
            };
        }

        /// <summary>VS velocity 64 is native 100; each half of the VS range scales linearly.</summary>
        public static int NativeToVelocity(double native) => native <= 100
            ? (int)Math.Round(Math.Max(0, native) * DefaultVelocity / 100.0)
            : Math.Min(127, DefaultVelocity + (int)Math.Round((native - 100) * 63 / 100.0));
        public static int VelocityToNative(int velocity) => velocity <= DefaultVelocity
            ? (int)Math.Round(velocity * 100.0 / DefaultVelocity)
            : 100 + (int)Math.Round((velocity - DefaultVelocity) * 100.0 / 63);

        /// <summary>DYN 64 is 0 dB; the gain law is an OpenUtau approximation, not a vendor curve.</summary>
        public static int DynToNative(int value) => value <= 0
            ? -240 : Math.Clamp((int)Math.Round(200 * Math.Log10(value / 64.0)), -240, 120);
        public static int NativeToDyn(int native) => native <= -240
            ? 0 : Math.Clamp((int)Math.Round(64 * Math.Pow(10, native / 200.0)), 0, 127);

        void ExportPitch() {
            var curve = part.curves.FirstOrDefault(c => c.abbr == Format.Ustx.PITD);
            originalCurves[Format.Ustx.PITD] = (curve?.xs.ToArray() ?? Array.Empty<int>(), curve?.ys.ToArray() ?? Array.Empty<int>());
            if (curve == null || curve.xs.Count == 0) {
                return;
            }
            int maxCents = curve.ys.Select(Math.Abs).DefaultIfEmpty(0).Max();
            int pbs = Math.Clamp((int)Math.Ceiling(maxCents * 8192.0 / 8191 / 100), 2, 24);
            if (pbs != lanes["PBS"].Default) {
                lanes["PBS"].Events.Add(new ControlEvent { Id = nextId++, Pos = Math.Min(0, curve.xs[0]), Value = pbs });
            }
            foreach (var (x, y) in SampleNative(curve)) {
                int pit = (int)Math.Round(y * 8192.0 / (100.0 * pbs));
                lanes["PIT"].Events.Add(new ControlEvent { Id = nextId++, Pos = x, Value = Math.Clamp(pit, -8192, 8191) });
            }
        }

        /// <summary>
        /// Native curves interpolate linearly and fall back to the default outside their points,
        /// whereas JOB events hold. Sampling every curve interval keeps reads within that step.
        /// </summary>
        static IEnumerable<(int x, int y)> SampleNative(UCurve curve) {
            int first = curve.xs[0];
            int last = curve.xs[curve.xs.Count - 1];
            var positions = new SortedSet<int>(curve.xs);
            for (int x = (int)Math.Ceiling(first / (double)UCurve.interval) * UCurve.interval; x < last; x += UCurve.interval) {
                positions.Add(x);
            }
            foreach (int x in positions) {
                yield return (x, curve.Sample(x));
            }
            int defaultValue = (int)curve.descriptor.defaultValue;
            if (curve.ys[curve.ys.Count - 1] != defaultValue) {
                yield return (last + 1, defaultValue);
            }
        }

        void ExportDynamics() {
            var curve = part.curves.FirstOrDefault(c => c.abbr == Format.Ustx.DYN);
            originalCurves[Format.Ustx.DYN] = (curve?.xs.ToArray() ?? Array.Empty<int>(), curve?.ys.ToArray() ?? Array.Empty<int>());
            if (curve == null || curve.xs.Count == 0) {
                return;
            }
            foreach (var (x, y) in SampleNative(curve)) {
                lanes["DYN"].Events.Add(new ControlEvent { Id = nextId++, Pos = x, Value = NativeToDyn(y) });
            }
        }

        public void Register(Script script) {
            void Add(string name, Func<CallbackArguments, DynValue> fn) {
                script.Globals[name] = DynValue.NewCallback((ctx, args) => {
                    try {
                        return fn(args);
                    } catch (JobPluginException) {
                        throw;
                    } catch (ScriptRuntimeException e) {
                        throw new JobPluginException($"{name}: {e.Message}", e);
                    }
                }, name);
            }
            Add("VSSeekToBeginNote", _ => SeekNotes());
            Add("VSGetNextNote", _ => NextNote(script));
            Add("VSGetNextNoteEx", _ => NextNote(script));
            Add("VSInsertNote", a => InsertNote("VSInsertNote", Arg(a, 0, "VSInsertNote")));
            Add("VSInsertNoteEx", a => InsertNote("VSInsertNoteEx", Arg(a, 0, "VSInsertNoteEx")));
            Add("VSUpdateNote", a => UpdateNote("VSUpdateNote", Arg(a, 0, "VSUpdateNote")));
            Add("VSUpdateNoteEx", a => UpdateNote("VSUpdateNoteEx", Arg(a, 0, "VSUpdateNoteEx")));
            Add("VSRemoveNote", a => RemoveNote(Arg(a, 0, "VSRemoveNote")));
            Add("VSGetMusicalPart", _ => GetPart(script));
            Add("VSUpdateMusicalPart", a => UpdatePart(Arg(a, 0, "VSUpdateMusicalPart")));
            Add("VSGetMusicalPartSinger", _ => GetSinger(script));
            Add("VSSeekToBeginControl", a => SeekControl(a));
            Add("VSGetNextControl", a => NextControl(script, a));
            Add("VSGetControlAt", a => ControlAt(a));
            Add("VSGetDefaultControlValue", a => DefaultControl(a));
            Add("VSInsertControl", a => InsertControl("VSInsertControl", Arg(a, 0, "VSInsertControl")));
            Add("VSUpdateControl", a => UpdateControl(Arg(a, 0, "VSUpdateControl")));
            Add("VSUpdateControlAt", a => UpdateControlAt(a));
            Add("VSRemoveControl", a => RemoveControl(Arg(a, 0, "VSRemoveControl")));
            Add("VSSeekToBeginTempo", _ => { tempoCursor = 0; return Ok; });
            Add("VSGetNextTempo", _ => NextTempo(script));
            Add("VSGetTempoAt", a => TempoAt(a));
            Add("VSSeekToBeginTimeSig", _ => { timeSigCursor = 0; return Ok; });
            Add("VSGetNextTimeSig", _ => NextTimeSig(script));
            Add("VSGetTimeSigAt", a => TimeSigAt(a));
            Add("VSDlgSetDialogTitle", a => { dialog.Title = a[0].CastToString() ?? string.Empty; return Ok; });
            Add("VSDlgAddField", a => AddField(Arg(a, 0, "VSDlgAddField")));
            Add("VSDlgDoModal", _ => DoModal());
            Add("VSDlgGetIntValue", a => FieldValue(a, 0));
            Add("VSDlgGetBoolValue", a => FieldValue(a, 1));
            Add("VSDlgGetFloatValue", a => FieldValue(a, 2));
            Add("VSDlgGetStringValue", a => FieldValue(a, 3));
            Add("VSMessageBox", a => MessageBox(a));
            Add("VSSeekToBeginNoteExpression", a => SeekNoteExpression(a));
            Add("VSGetNextNoteExpression", a => NextNoteExpression(script, a));
            Add("VSGetNoteExpressionAt", a => NoteExpressionAt(a));
            Add("VSInsertNoteExpression", a => InsertNoteExpression(a));
            Add("VSRemoveNoteExpression", a => RemoveNoteExpression(a));
        }

        static readonly DynValue Ok = DynValue.NewNumber(1);
        static readonly DynValue Fail = DynValue.NewNumber(0);
        static DynValue Pair(DynValue status, DynValue value) => DynValue.NewTuple(status, value);
        // Exhausted iterators still return a table: shipped scripts assign fields to it after the loop.
        static DynValue Exhausted(Script script) => DynValue.NewTuple(Fail, DynValue.NewTable(script));

        static Table Arg(CallbackArguments args, int index, string api) {
            var value = args[index];
            if (value.Type != DataType.Table) {
                throw new JobPluginException($"{api}: expected a table argument");
            }
            return value.Table;
        }

        static int? IntField(Table t, string key, string api) {
            var v = t.Get(key);
            if (v.IsNil()) {
                return null;
            }
            return ToInt(v.CastToNumber()) ?? throw new JobPluginException($"{api}: field '{key}' must be a number, got {v.ToPrintString()}");
        }

        /// <summary>
        /// Non-integral numbers round to nearest, ties to even, like Lua 5.1's lua_tointeger in 32-bit
        /// Windows builds. This is a declared host policy; scripts commonly pass computed ticks and values.
        /// </summary>
        static int? ToInt(double? n) {
            if (n == null || !double.IsFinite(n.Value)) {
                return null;
            }
            double rounded = Math.Round(n.Value, MidpointRounding.ToEven);
            return rounded < int.MinValue || rounded > int.MaxValue ? null : (int)rounded;
        }

        static string? StringField(Table t, string key, string api) {
            var v = t.Get(key);
            if (v.IsNil()) {
                return null;
            }
            if (v.Type != DataType.String && v.Type != DataType.Number) {
                throw new JobPluginException($"{api}: field '{key}' must be a string");
            }
            return v.CastToString();
        }

        static int IntArg(CallbackArguments args, int index, string api) {
            return ToInt(args[index].CastToNumber()) ?? throw new JobPluginException($"{api}: argument {index + 1} must be a number");
        }

        // Notes

        IEnumerable<NoteState> LiveNotes => notes.Where(n => !n.Removed);

        DynValue SeekNotes() {
            noteCursor = LiveNotes.OrderBy(n => n.Current.Pos).ThenBy(n => n.Id).Select(n => n.Id).ToList();
            noteCursorIndex = 0;
            return Ok;
        }

        DynValue NextNote(Script script) {
            while (noteCursorIndex < noteCursor.Count) {
                long id = noteCursor[noteCursorIndex++];
                var state = notes.FirstOrDefault(n => n.Id == id);
                if (state != null && !state.Removed) {
                    return Pair(Ok, DynValue.NewTable(NoteTable(script, state)));
                }
            }
            return Exhausted(script);
        }

        static Table NoteTable(Script script, NoteState state) {
            var f = state.Current;
            var t = new Table(script);
            t["objID"] = (double)state.Id;
            t["posTick"] = f.Pos;
            t["durTick"] = f.Dur;
            t["noteNum"] = f.NoteNum;
            t["velocity"] = f.Velocity;
            t["lyric"] = f.Lyric;
            t["phonemes"] = f.Phonemes;
            t["phLock"] = f.PhLock;
            t["bendDepth"] = f.BendDepth;
            t["bendLength"] = f.BendLength;
            t["risePort"] = f.RisePort;
            t["fallPort"] = f.FallPort;
            t["decay"] = f.Decay;
            t["accent"] = f.Accent;
            t["opening"] = f.Opening;
            t["vibratoLength"] = f.VibratoLength;
            t["vibratoType"] = f.VibratoType;
            return t;
        }

        NoteFields ReadFields(Table t, NoteFields baseline, string api) {
            var f = baseline.Clone();
            f.Pos = IntField(t, "posTick", api) ?? f.Pos;
            f.Dur = IntField(t, "durTick", api) ?? f.Dur;
            f.NoteNum = IntField(t, "noteNum", api) ?? f.NoteNum;
            f.Velocity = IntField(t, "velocity", api) ?? f.Velocity;
            f.Lyric = StringField(t, "lyric", api) ?? f.Lyric;
            f.Phonemes = StringField(t, "phonemes", api) ?? f.Phonemes;
            f.PhLock = IntField(t, "phLock", api) ?? f.PhLock;
            f.BendDepth = IntField(t, "bendDepth", api) ?? f.BendDepth;
            f.BendLength = IntField(t, "bendLength", api) ?? f.BendLength;
            f.RisePort = IntField(t, "risePort", api) ?? f.RisePort;
            f.FallPort = IntField(t, "fallPort", api) ?? f.FallPort;
            f.Decay = IntField(t, "decay", api) ?? f.Decay;
            f.Accent = IntField(t, "accent", api) ?? f.Accent;
            f.Opening = IntField(t, "opening", api) ?? f.Opening;
            f.VibratoLength = IntField(t, "vibratoLength", api) ?? f.VibratoLength;
            f.VibratoType = IntField(t, "vibratoType", api) ?? f.VibratoType;
            if (f.Pos < 0) {
                throw new JobPluginException($"{api}: posTick {f.Pos} is negative");
            }
            if (f.Dur < MinDuration) {
                throw new JobPluginException($"{api}: durTick {f.Dur} is shorter than OpenUtau's {MinDuration}-tick minimum");
            }
            if (f.NoteNum < 0 || f.NoteNum > 127) {
                throw new JobPluginException($"{api}: noteNum {f.NoteNum} is outside 0-127");
            }
            if (f.Velocity < 0 || f.Velocity > 127) {
                throw new JobPluginException($"{api}: velocity {f.Velocity} is outside 0-127");
            }
            if (f.VibratoLength < 0 || f.VibratoLength > 100) {
                throw new JobPluginException($"{api}: vibratoLength {f.VibratoLength} is outside 0-100");
            }
            return f;
        }

        /// <param name="lyricDriven">The lyric differs from the exported one (or the note is new), so
        /// phonemes and phLock describe that lyric's reading. OpenUtau's phonemiser derives the reading
        /// from the lyric, so they are accepted and not applied.</param>
        void CheckUnsupported(NoteFields before, NoteFields after, string api, bool velocityLocked, bool lyricDriven) {
            void Check(bool same, string field, string reason) {
                if (!same) {
                    throw new JobPluginException($"{api}: changing note field '{field}' is not supported ({reason})");
                }
            }
            const string noEquivalent = "no OpenUtau equivalent";
            const string phonemeOnly = "a phoneme change without a lyric change cannot be expressed in OpenUtau";
            Check(lyricDriven || before.Phonemes == after.Phonemes, "phonemes", phonemeOnly);
            Check(lyricDriven || before.PhLock == after.PhLock, "phLock", phonemeOnly);
            Check(before.BendDepth == after.BendDepth || after.BendDepth == 0, "bendDepth", noEquivalent);
            Check(before.BendLength == after.BendLength, "bendLength", noEquivalent);
            Check(before.RisePort == after.RisePort, "risePort", noEquivalent);
            Check(before.FallPort == after.FallPort, "fallPort", noEquivalent);
            Check(before.Decay == after.Decay, "decay", noEquivalent);
            Check(before.Accent == after.Accent, "accent", noEquivalent);
            Check(before.Opening == after.Opening, "opening", noEquivalent);
            Check(after.VibratoType >= 0 && after.VibratoType <= VibratoPresets.All.Count,
                "vibratoType", "only 0 (off) and the vibrato types 1-16 exist");
            if (before.Velocity != after.Velocity) {
                Check(!velocityLocked, "velocity", "the note's phonemes have different velocities");
                Check(velSupported, "velocity", "the track's renderer has no velocity expression");
            }
        }

        DynValue InsertNote(string api, Table t) {
            if (IntField(t, "posTick", api) == null || IntField(t, "durTick", api) == null || IntField(t, "noteNum", api) == null) {
                throw new JobPluginException($"{api}: posTick, durTick and noteNum are required");
            }
            var defaults = new NoteFields { Velocity = DefaultVelocity, Lyric = NotePresets.Default.DefaultLyric };
            var fields = ReadFields(t, defaults, api);
            CheckUnsupported(defaults, fields, api, false, StringField(t, "lyric", api) != null);
            var copiedId = t.Get("objID").CastToNumber();
            var copied = copiedId.HasValue ? notes.FirstOrDefault(n => n.Id == copiedId.Value) : null;
            // A copied API table carries its original objID. Keep the native pitch address and
            // residual cents rather than reconstructing a clone from its integer MIDI adapter.
            var template = copied == null ? null : ApplyNoteState(copied);
            var vibrato = copied?.Vibrato.Clone() ?? new UVibrato();
            var state = new NoteState { Id = nextId++, Current = fields, Vibrato = vibrato,
                Template = template, TemplateFields = copied?.Current.Clone() };
            // A copy that keeps the source's vibrato type keeps its vibrato. Curve positions span the
            // whole note, so they only stay valid for a copy at the same time, such as a note removed
            // and reinserted to change its lyric; a moved preset note gets the preset laid out again.
            bool sameType = copied != null && fields.VibratoType == copied.Current.VibratoType;
            if (!sameType) {
                vibrato.rateCurve = null;
                vibrato.depthCurve = null;
                vibrato.preset = null;
                if (fields.VibratoType > 0) {
                    vibrato.length = fields.VibratoLength;
                    VibratoPresets.Apply(vibrato, fields.VibratoType, VibratoSeconds(fields));
                }
            } else if (fields.Pos == copied!.Current.Pos && fields.Dur == copied.Current.Dur) {
                foreach (var pair in copied.Expressions) {
                    state.Expressions[pair.Key] = new NoteExpressionLane {
                        Events = pair.Value.Events.Select(e => new NoteExpressionEvent {
                            Id = nextId++, Pos = e.Pos, Value = e.Value }).ToList(),
                        Original = pair.Value.Original.Select(e => new NoteExpressionEvent {
                            Id = e.Id, Pos = e.Pos, Value = e.Value }).ToList(),
                    };
                }
            } else if (vibrato.preset != null) {
                vibrato.length = fields.VibratoLength;
                VibratoPresets.Apply(vibrato, vibrato.preset.Value, VibratoSeconds(fields));
            } else {
                vibrato.rateCurve = null;
                vibrato.depthCurve = null;
            }
            notes.Add(state);
            return Ok;
        }

        double VibratoSeconds(NoteFields fields) {
            double end = partPosition + fields.Pos + fields.Dur;
            return project.timeAxis.MsBetweenTickPos(end - fields.Dur * fields.VibratoLength / 100.0, end) / 1000;
        }

        NoteState? FindNote(Table t) {
            var id = t.Get("objID").CastToNumber();
            return id == null ? null : notes.FirstOrDefault(n => n.Id == (long)id.Value && !n.Removed);
        }

        DynValue UpdateNote(string api, Table t) {
            var state = FindNote(t);
            if (state == null) {
                return Fail;
            }
            var fields = ReadFields(t, state.Current, api);
            CheckUnsupported(state.Current, fields, api, state.VelocityLocked, LyricDriven(state, fields));
            if (fields.VibratoType != state.Current.VibratoType && fields.VibratoType > 0) {
                // Choosing a type replaces the note's curves with the preset's, as the type list does.
                state.Vibrato.length = fields.VibratoLength;
                VibratoPresets.Apply(state.Vibrato, fields.VibratoType, VibratoSeconds(fields));
                state.Expressions.Clear();
                state.VibratoReset = true;
            }
            state.Current = fields;
            return Ok;
        }

        /// <summary>
        /// VOCALOID reads katakana and hiragana lyrics alike, but OpenUtau's Japanese phonemisers look
        /// lyrics up as hiragana. A lyric written entirely in katakana is converted; the long-vowel mark
        /// and ヴ, which voicebanks usually label in katakana, are kept. Mixed text is left unchanged.
        /// </summary>
        public static string KatakanaToHiragana(string lyric) {
            if (lyric.Length == 0 || !lyric.All(c => c >= '\u30A1' && c <= '\u30F6' || c == '\u30FC')) {
                return lyric;
            }
            return string.Concat(lyric.Select(c => c >= '\u30A1' && c <= '\u30F6' && c != '\u30F4' ? (char)(c - 0x60) : c));
        }

        static bool LyricDriven(NoteState state, NoteFields fields) =>
            state.Original == null || fields.Lyric != state.Original.Lyric;

        DynValue RemoveNote(Table t) {
            var state = FindNote(t);
            if (state == null) {
                return Fail;
            }
            state.Removed = true;
            return Ok;
        }

        // JOB value 64 is the base rate and depth of the note's vibrato type (5 Hz and 50 cents
        // without one). VR doubles every 64 units; VD is linear.
        public static double NoteExpressionToNative(string type, double value, int? preset = null) => type == "VR"
            ? VibratoPreset.RateHz(VibratoPresets.BaseRate(preset), value)
            : VibratoPreset.DepthCents(VibratoPresets.BaseDepth(preset), value);
        public static double NativeToNoteExpression(string type, double value, int? preset = null) => type == "VR"
            ? VibratoPreset.RateValue(VibratoPresets.BaseRate(preset), value)
            : VibratoPreset.DepthValue(VibratoPresets.BaseDepth(preset), value);

        static double Number(DynValue value, string api) {
            double? number = value.CastToNumber();
            if (!number.HasValue || !double.IsFinite(number.Value)) {
                throw new JobPluginException($"{api}: expected a finite number");
            }
            return number.Value;
        }

        static string ExpressionType(DynValue value, string api) {
            string? type = value.CastToString();
            if (type != "VR" && type != "VD") {
                throw new JobPluginException($"{api}: note expression '{type}' is not supported");
            }
            return type;
        }

        static double ExpressionPosition(DynValue value, string api) {
            double pos = Number(value, api);
            if (pos < 0 || pos > 1) {
                throw new JobPluginException($"{api}: posNorm must be within 0-1 of the whole note");
            }
            return pos;
        }

        NoteExpressionLane NoteLane(NoteState state, string type) {
            if (!state.Expressions.TryGetValue(type, out var lane)) {
                lane = new NoteExpressionLane();
                var curve = type == "VR" ? state.Vibrato.rateCurve : state.Vibrato.depthCurve;
                if (curve != null && curve.Count > 0) {
                    foreach (var point in curve) {
                        lane.Events.Add(new NoteExpressionEvent { Id = nextId++, Pos = point.x,
                            Value = NativeToNoteExpression(type, point.y, state.Vibrato.preset) });
                    }
                } else {
                    double value = type == "VR" ? 1000.0 / state.Vibrato.period : state.Vibrato.depth;
                    lane.Events.Add(new NoteExpressionEvent { Id = nextId++, Pos = 0,
                        Value = NativeToNoteExpression(type, value, state.Vibrato.preset) });
                }
                lane.Original = lane.Events.Select(e => new NoteExpressionEvent {
                    Id = e.Id, Pos = e.Pos, Value = e.Value }).ToList();
                state.Expressions[type] = lane;
            }
            return lane;
        }

        NoteState ExpressionNote(CallbackArguments args, string api) {
            return FindNote(Arg(args, 0, api))
                ?? throw new JobPluginException($"{api}: note does not exist");
        }

        DynValue SeekNoteExpression(CallbackArguments args) {
            const string api = "VSSeekToBeginNoteExpression";
            var state = ExpressionNote(args, api);
            var lane = NoteLane(state, ExpressionType(args[1], api));
            lane.Cursor = lane.Events.OrderBy(e => e.Pos).ThenBy(e => e.Id).Select(e => e.Id).ToList();
            lane.CursorIndex = 0;
            return Ok;
        }

        DynValue NextNoteExpression(Script script, CallbackArguments args) {
            const string api = "VSGetNextNoteExpression";
            var state = ExpressionNote(args, api);
            string type = ExpressionType(args[1], api);
            var lane = NoteLane(state, type);
            while (lane.CursorIndex < lane.Cursor.Count) {
                long id = lane.Cursor[lane.CursorIndex++];
                var point = lane.Events.FirstOrDefault(e => e.Id == id);
                if (point == null) continue;
                var t = new Table(script);
                t["objID"] = point.Id;
                t["type"] = type;
                t["posNorm"] = point.Pos;
                t["value"] = point.Value;
                return Pair(Ok, DynValue.NewTable(t));
            }
            return Exhausted(script);
        }

        DynValue NoteExpressionAt(CallbackArguments args) {
            const string api = "VSGetNoteExpressionAt";
            var state = ExpressionNote(args, api);
            string type = ExpressionType(args[1], api);
            double pos = ExpressionPosition(args[2], api);
            var events = NoteLane(state, type).Events.OrderBy(e => e.Pos).ToList();
            if (events.Count == 0) return Pair(Ok, DynValue.NewNumber(64));
            var left = events.LastOrDefault(e => e.Pos <= pos);
            if (left == null) return Pair(Ok, DynValue.NewNumber(events[0].Value));
            var right = events.FirstOrDefault(e => e.Pos > pos);
            if (right == null) return Pair(Ok, DynValue.NewNumber(left.Value));
            // Interpolate physical Hz/cents, matching the native curve evaluator.
            double a = NoteExpressionToNative(type, left.Value, state.Vibrato.preset);
            double b = NoteExpressionToNative(type, right.Value, state.Vibrato.preset);
            double value = a + (b - a) * (pos - left.Pos) / (right.Pos - left.Pos);
            return Pair(Ok, DynValue.NewNumber(NativeToNoteExpression(type, value, state.Vibrato.preset)));
        }

        DynValue InsertNoteExpression(CallbackArguments args) {
            const string api = "VSInsertNoteExpression";
            var state = ExpressionNote(args, api);
            var t = Arg(args, 1, api);
            string type = ExpressionType(t.Get("type"), api);
            double pos = ExpressionPosition(t.Get("posNorm"), api);
            double value = Number(t.Get("value"), api);
            var lane = NoteLane(state, type);
            if ((value < 0 || value > 127) && !lane.Original.Any(e =>
                Math.Abs(e.Pos - pos) < 1e-9 && Math.Abs(e.Value - value) < 1e-7)) {
                throw new JobPluginException($"{api}: value must be within the OpenUtau host range 0-127");
            }
            var existing = lane.Events.FirstOrDefault(e => Math.Abs(e.Pos - pos) < 1e-9);
            if (existing != null) existing.Value = value;
            else lane.Events.Add(new NoteExpressionEvent { Id = nextId++, Pos = pos, Value = value });
            return Ok;
        }

        DynValue RemoveNoteExpression(CallbackArguments args) {
            const string api = "VSRemoveNoteExpression";
            var state = ExpressionNote(args, api);
            var t = Arg(args, 1, api);
            string type = ExpressionType(t.Get("type"), api);
            var lane = NoteLane(state, type);
            double? id = t.Get("objID").CastToNumber();
            var point = id.HasValue ? lane.Events.FirstOrDefault(e => e.Id == id.Value)
                : lane.Events.FirstOrDefault(e => Math.Abs(e.Pos - ExpressionPosition(t.Get("posNorm"), api)) < 1e-9);
            if (point == null) return Fail;
            lane.Events.Remove(point);
            return Ok;
        }

        static bool ExpressionChanged(NoteExpressionLane lane) {
            var current = lane.Events.OrderBy(e => e.Pos).ToList();
            var original = lane.Original.OrderBy(e => e.Pos).ToList();
            return current.Count != original.Count || current.Where((e, i) =>
                Math.Abs(e.Pos - original[i].Pos) > 1e-9 || Math.Abs(e.Value - original[i].Value) > 1e-7).Any();
        }

        // Part and singer

        DynValue GetPart(Script script) {
            var t = new Table(script);
            t["posTick"] = partPosition;
            t["playTime"] = partDuration;
            t["durTick"] = partDuration;
            t["name"] = partName;
            t["comment"] = partComment;
            return Pair(Ok, DynValue.NewTable(t));
        }

        DynValue UpdatePart(Table t) {
            const string api = "VSUpdateMusicalPart";
            int pos = IntField(t, "posTick", api) ?? partPosition;
            int? playTime = IntField(t, "playTime", api);
            int? durTick = IntField(t, "durTick", api);
            int duration = partDuration;
            if (playTime != null && playTime != partDuration) {
                duration = playTime.Value;
            }
            if (durTick != null && durTick != partDuration) {
                if (duration != partDuration && duration != durTick) {
                    throw new JobPluginException($"{api}: playTime and durTick disagree");
                }
                duration = durTick.Value;
            }
            string name = StringField(t, "name", api) ?? partName;
            string comment = StringField(t, "comment", api) ?? partComment;
            if (pos < 0) {
                throw new JobPluginException($"{api}: posTick {pos} is negative");
            }
            if (duration <= 0) {
                throw new JobPluginException($"{api}: duration {duration} is not positive");
            }
            if (comment != partComment) {
                throw new JobPluginException($"{api}: changing the part comment is not supported");
            }
            if (duration != partDuration) {
                partDurationSet = true;
            }
            partPosition = pos;
            partDuration = duration;
            partName = name;
            return Ok;
        }

        DynValue GetSinger(Script script) {
            var t = new Table(script);
            t["name"] = track.Singer?.Name ?? string.Empty;
            t["id"] = track.Singer?.Id ?? string.Empty;
            // vBS and other VOCALOID bank fields are deliberately absent: an OpenUtau singer has no bank.
            return Pair(Ok, DynValue.NewTable(t));
        }

        // Controls

        ControlLane? Lane(DynValue type) => type.Type == DataType.String && lanes.TryGetValue(type.String, out var lane) ? lane : null;

        ControlLane LaneOrThrow(DynValue type, string api) => Lane(type)
            ?? throw new JobPluginException($"{api}: unknown control type {type.ToPrintString()}");

        DynValue SeekControl(CallbackArguments args) {
            var lane = Lane(args[0]);
            if (lane == null) {
                return Fail;
            }
            lane.Cursor = lane.Events.Select(e => e.Id).ToList();
            lane.CursorIndex = 0;
            return Ok;
        }

        DynValue NextControl(Script script, CallbackArguments args) {
            var lane = Lane(args[0]);
            if (lane == null) {
                return Exhausted(script);
            }
            while (lane.CursorIndex < lane.Cursor.Count) {
                long id = lane.Cursor[lane.CursorIndex++];
                var e = lane.Events.FirstOrDefault(ev => ev.Id == id);
                if (e != null) {
                    return Pair(Ok, DynValue.NewTable(ControlTable(script, lane, e)));
                }
            }
            return Exhausted(script);
        }

        static Table ControlTable(Script script, ControlLane lane, ControlEvent e) {
            var t = new Table(script);
            t["objID"] = (double)e.Id;
            t["type"] = lane.Type;
            t["posTick"] = e.Pos;
            t["value"] = e.Value;
            return t;
        }

        static int FindIndex(ControlLane lane, int pos) {
            int low = 0, high = lane.Events.Count;
            while (low < high) {
                int mid = (low + high) / 2;
                if (lane.Events[mid].Pos < pos) low = mid + 1;
                else high = mid;
            }
            return low;
        }

        /// <summary>JOB control events hold their value until the next event.</summary>
        static int HeldValue(ControlLane lane, int pos) {
            int index = FindIndex(lane, pos);
            if (index < lane.Events.Count && lane.Events[index].Pos == pos) {
                return lane.Events[index].Value;
            }
            return index > 0 ? lane.Events[index - 1].Value : lane.Default;
        }

        DynValue ControlAt(CallbackArguments args) {
            var lane = Lane(args[0]);
            if (lane == null) {
                return Pair(Fail, DynValue.Nil);
            }
            return Pair(Ok, DynValue.NewNumber(HeldValue(lane, IntArg(args, 1, "VSGetControlAt"))));
        }

        DynValue DefaultControl(CallbackArguments args) {
            var lane = Lane(args[0]);
            return lane == null ? Pair(Fail, DynValue.Nil) : Pair(Ok, DynValue.NewNumber(lane.Default));
        }

        void CheckWritable(ControlLane lane, int value, string api) {
            if (value < lane.Min || value > lane.Max) {
                throw new JobPluginException($"{api}: {lane.Type} value {value} is outside {lane.Min}-{lane.Max}");
            }
            if (value == lane.Default) {
                return;
            }
            if (!lane.Native) {
                throw new JobPluginException($"{api}: control {lane.Type} has no OpenUtau equivalent");
            }
            if (lane.Type == "DYN" && !dynSupported) {
                throw new JobPluginException($"{api}: the track's renderer does not support dynamics");
            }
        }

        /// <summary>Write-backs of unchanged values keep the native curve untouched.</summary>
        static void SetValue(ControlLane lane, ControlEvent e, int value) {
            if (e.Value != value) {
                e.Value = value;
                lane.Changed = true;
            }
        }

        void Put(ControlLane lane, ControlEvent e) {
            int index = FindIndex(lane, e.Pos);
            if (index < lane.Events.Count && lane.Events[index].Pos == e.Pos) {
                lane.Events[index] = e;
            } else {
                lane.Events.Insert(index, e);
            }
            lane.Changed = true;
        }

        DynValue InsertControl(string api, Table t) {
            var lane = LaneOrThrow(t.Get("type"), api);
            int pos = IntField(t, "posTick", api) ?? throw new JobPluginException($"{api}: posTick is required");
            int value = IntField(t, "value", api) ?? throw new JobPluginException($"{api}: value is required");
            CheckWritable(lane, value, api);
            int index = FindIndex(lane, pos);
            if (index < lane.Events.Count && lane.Events[index].Pos == pos) {
                SetValue(lane, lane.Events[index], value);
            } else {
                Put(lane, new ControlEvent { Id = nextId++, Pos = pos, Value = value });
            }
            return Ok;
        }

        DynValue UpdateControl(Table t) {
            const string api = "VSUpdateControl";
            var lane = LaneOrThrow(t.Get("type"), api);
            int pos = IntField(t, "posTick", api) ?? throw new JobPluginException($"{api}: posTick is required");
            int value = IntField(t, "value", api) ?? throw new JobPluginException($"{api}: value is required");
            var id = t.Get("objID").CastToNumber();
            var e = id != null ? lane.Events.FirstOrDefault(ev => ev.Id == (long)id.Value) : null;
            if (e == null) {
                int index = FindIndex(lane, pos);
                if (index >= lane.Events.Count || lane.Events[index].Pos != pos) {
                    return Fail;
                }
                e = lane.Events[index];
            }
            CheckWritable(lane, value, api);
            if (e.Pos == pos) {
                SetValue(lane, e, value);
                return Ok;
            }
            lane.Events.Remove(e);
            e.Pos = pos;
            e.Value = value;
            Put(lane, e);
            return Ok;
        }

        DynValue UpdateControlAt(CallbackArguments args) {
            const string api = "VSUpdateControlAt";
            var lane = LaneOrThrow(args[0], api);
            int pos = IntArg(args, 1, api);
            int value = IntArg(args, 2, api);
            CheckWritable(lane, value, api);
            int index = FindIndex(lane, pos);
            if (index < lane.Events.Count && lane.Events[index].Pos == pos) {
                SetValue(lane, lane.Events[index], value);
            } else {
                Put(lane, new ControlEvent { Id = nextId++, Pos = pos, Value = value });
            }
            return Ok;
        }

        DynValue RemoveControl(Table t) {
            const string api = "VSRemoveControl";
            var lane = LaneOrThrow(t.Get("type"), api);
            var id = t.Get("objID").CastToNumber();
            var e = id != null ? lane.Events.FirstOrDefault(ev => ev.Id == (long)id.Value) : null;
            if (e == null && IntField(t, "posTick", api) is int pos) {
                int index = FindIndex(lane, pos);
                e = index < lane.Events.Count && lane.Events[index].Pos == pos ? lane.Events[index] : null;
            }
            if (e == null) {
                return Fail;
            }
            lane.Events.Remove(e);
            lane.Changed = true;
            return Ok;
        }

        // Tempo and metre: read-only, song-global ticks, tempo in BPM.

        DynValue NextTempo(Script script) {
            if (tempoCursor < 0 || tempoCursor >= tempos.Length) {
                return Exhausted(script);
            }
            var tempo = tempos[tempoCursor++];
            var t = new Table(script);
            t["posTick"] = tempo.position;
            t["tempo"] = tempo.bpm;
            return Pair(Ok, DynValue.NewTable(t));
        }

        DynValue TempoAt(CallbackArguments args) {
            int tick = IntArg(args, 0, "VSGetTempoAt");
            var tempo = tempos.LastOrDefault(t => t.position <= tick) ?? tempos.FirstOrDefault();
            return tempo == null ? Pair(Fail, DynValue.Nil) : Pair(Ok, DynValue.NewNumber(tempo.bpm));
        }

        DynValue NextTimeSig(Script script) {
            if (timeSigCursor < 0 || timeSigCursor >= timeSignatures.Length) {
                return Exhausted(script);
            }
            var sig = timeSignatures[timeSigCursor++];
            var t = new Table(script);
            t["posTick"] = sig.tick;
            t["measure"] = sig.bar;
            t["numerator"] = sig.numerator;
            t["denominator"] = sig.denominator;
            return Pair(Ok, DynValue.NewTable(t));
        }

        DynValue TimeSigAt(CallbackArguments args) {
            int tick = IntArg(args, 0, "VSGetTimeSigAt");
            if (timeSignatures.Length == 0) {
                return DynValue.NewTuple(Fail, DynValue.Nil, DynValue.Nil);
            }
            var sig = timeSignatures.LastOrDefault(s => s.tick <= tick);
            if (sig == default) {
                sig = timeSignatures[0];
            }
            return DynValue.NewTuple(Ok, DynValue.NewNumber(sig.numerator), DynValue.NewNumber(sig.denominator));
        }

        // Dialogs and messages

        DynValue AddField(Table t) {
            const string api = "VSDlgAddField";
            string name = StringField(t, "name", api) ?? string.Empty;
            if (name.Length == 0 || dialog.Fields.Any(f => f.Name == name)) {
                return Fail;
            }
            int type = IntField(t, "type", api) ?? 3;
            var field = new JobPluginDialogField {
                Name = name,
                Caption = StringField(t, "caption", api) ?? name,
                Type = type,
            };
            var initial = t.Get("initialVal");
            string text = initial.IsNil() ? string.Empty : initial.CastToString() ?? string.Empty;
            switch (type) {
                case 0:
                    field.Value = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : 0;
                    break;
                case 1:
                    field.Value = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double b) && b != 0;
                    break;
                case 2:
                    field.Value = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : 0.0;
                    break;
                case 3:
                    field.Value = text;
                    break;
                case 4:
                    field.Choices.AddRange(text.Split(','));
                    field.Value = field.Choices[0];
                    break;
                default:
                    throw new JobPluginException($"{api}: field type {type} is not supported");
            }
            dialog.Fields.Add(field);
            return Ok;
        }

        DynValue DoModal() {
            if (interaction == null) {
                throw new JobPluginException("VSDlgDoModal: no user interface is available");
            }
            bool accepted;
            using (Runtime?.PauseClock()) {
                accepted = interaction.ShowDialog(dialog);
            }
            if (!accepted) {
                Cancelled = true;
                return DynValue.NewNumber(2);
            }
            return Ok;
        }

        DynValue FieldValue(CallbackArguments args, int kind) {
            string name = args[0].CastToString() ?? string.Empty;
            var field = dialog.Fields.FirstOrDefault(f => f.Name == name);
            if (field == null) {
                return Pair(Fail, DynValue.Nil);
            }
            object? value = field.Value;
            DynValue result = kind switch {
                0 => value is int i ? DynValue.NewNumber(i) : value is double d ? DynValue.NewNumber(Math.Truncate(d)) : DynValue.Nil,
                1 => value is bool b ? DynValue.NewNumber(b ? 1 : 0) : value is int bi ? DynValue.NewNumber(bi != 0 ? 1 : 0) : DynValue.Nil,
                2 => value is double f ? DynValue.NewNumber(f) : value is int fi ? DynValue.NewNumber(fi) : DynValue.Nil,
                _ => DynValue.NewString(value switch {
                    bool sb => sb ? "1" : "0",
                    double sd => sd.ToString(CultureInfo.InvariantCulture),
                    null => string.Empty,
                    var other => Convert.ToString(other, CultureInfo.InvariantCulture) ?? string.Empty,
                }),
            };
            return result.IsNil() ? Pair(Fail, DynValue.Nil) : Pair(Ok, result);
        }

        DynValue MessageBox(CallbackArguments args) {
            string text = args[0].CastToString() ?? args[0].ToPrintString();
            int flags = args.Count > 1 && !args[1].IsNil() ? IntArg(args, 1, "VSMessageBox") : 0;
            if (flags != JobPluginApi.MessageOk && flags != JobPluginApi.MessageYesNo) {
                throw new JobPluginException($"VSMessageBox: button flags {flags} are not supported");
            }
            Log.Add(text);
            if (interaction == null) {
                if (flags == JobPluginApi.MessageOk) {
                    return DynValue.NewNumber(JobPluginApi.ResultOk);
                }
                throw new JobPluginException("VSMessageBox: no user interface is available");
            }
            int result;
            using (Runtime?.PauseClock()) {
                result = interaction.ShowMessage(text, flags);
            }
            return DynValue.NewNumber(result);
        }

        // Lua parameter tables

        public Table ProcessParam(Script script) {
            var t = new Table(script);
            t["beginPosTick"] = process.BeginTick;
            t["endPosTick"] = process.EndTick;
            t["songPosTick"] = process.SongTick;
            return t;
        }

        // Commit

        /// <summary>Converts the session into project changes, or throws if a change cannot be represented.</summary>
        public JobPluginEdit BuildEdit() {
            var edit = new JobPluginEdit();
            foreach (var state in notes) {
                if (state.Source != null) {
                    if (state.Removed) {
                        edit.RemovedNotes.Add(state.Source);
                    } else if (!state.Current.SameAs(state.Original!) || state.Expressions.Values.Any(ExpressionChanged)) {
                        if (!LyricDriven(state, state.Current) && (state.Current.Phonemes != state.Original!.Phonemes
                            || state.Current.PhLock != state.Original.PhLock)) {
                            throw new JobPluginException($"note at tick {state.Current.Pos}: a phoneme change without a lyric change cannot be expressed in OpenUtau");
                        }
                        edit.RemovedNotes.Add(state.Source);
                        edit.AddedNotes.Add(ApplyNoteState(state));
                    }
                } else if (!state.Removed) {
                    edit.AddedNotes.Add(ApplyNoteState(state));
                }
            }
            edit.AddedNotes.Sort((a, b) => a.position.CompareTo(b.position));
            if (lanes["PIT"].Changed || lanes["PBS"].Changed) {
                AddCurve(edit, Format.Ustx.PITD, BuildPitch());
            }
            if (lanes["DYN"].Changed) {
                var lane = lanes["DYN"];
                AddCurve(edit, Format.Ustx.DYN, StepCurve(lane.Events.Select(e => (e.Pos, DynToNative(e.Value))).ToList(), 0));
            }
            if (partPosition != part.position) {
                edit.Position = partPosition;
            }
            if (partDuration != part.Duration) {
                edit.Duration = partDuration;
            }
            if (partName != part.name) {
                edit.Name = partName;
            }
            if (partDurationSet) {
                int end = LiveNotes.Select(n => n.Current.Pos + n.Current.Dur).DefaultIfEmpty(0).Max();
                if (end > partDuration) {
                    throw new JobPluginException($"VSUpdateMusicalPart: notes end at {end}, after the part's new length {partDuration}");
                }
            }
            return edit;
        }

        void AddCurve(JobPluginEdit edit, string abbr, (int[] xs, int[] ys) curve) {
            var original = originalCurves[abbr];
            if (!original.xs.SequenceEqual(curve.xs) || !original.ys.SequenceEqual(curve.ys)) {
                edit.Curves[abbr] = curve;
            }
        }

        (int[] xs, int[] ys) BuildPitch() {
            var pit = lanes["PIT"];
            var pbs = lanes["PBS"];
            var positions = pit.Events.Select(e => e.Pos).Concat(pbs.Events.Select(e => e.Pos)).Distinct().OrderBy(p => p);
            var points = new List<(int x, int y)>();
            foreach (int pos in positions) {
                double cents = HeldValue(pit, pos) * HeldValue(pbs, pos) * 100.0 / 8192;
                int y = (int)Math.Round(cents);
                if (Math.Abs(y) > 1200) {
                    throw new JobPluginException($"PIT/PBS: a bend of {y} cents at tick {pos} exceeds OpenUtau's ±1200-cent pitch deviation");
                }
                points.Add((pos, y));
            }
            return StepCurve(points, 0);
        }

        /// <summary>
        /// Represents held event values on OpenUtau's linearly interpolated curve: each value holds
        /// until one tick before the next event, and the last one until the end of the part.
        /// </summary>
        (int[] xs, int[] ys) StepCurve(List<(int x, int y)> events, int defaultValue) {
            var points = new List<(int x, int y)>();
            for (int i = 0; i < events.Count; i++) {
                var (x, y) = events[i];
                points.Add((x, y));
                if (i + 1 < events.Count) {
                    int next = events[i + 1].x;
                    if (next - 1 > x && events[i + 1].y != y) {
                        points.Add((next - 1, y));
                    }
                } else if (y != defaultValue) {
                    points.Add((Math.Max(partDuration, x + 1), y));
                }
            }
            var simplified = new List<(int x, int y)>();
            for (int i = 0; i < points.Count; i++) {
                bool interior = i > 0 && i + 1 < points.Count
                    && points[i - 1].y == points[i].y && points[i + 1].y == points[i].y;
                if (!interior) {
                    simplified.Add(points[i]);
                }
            }
            if (simplified.All(p => p.y == defaultValue)) {
                simplified.Clear();
            }
            return (simplified.Select(p => p.x).ToArray(), simplified.Select(p => p.y).ToArray());
        }

        UNote ApplyNoteState(NoteState state) {
            var source = state.Source ?? state.Template;
            var original = state.Original ?? state.TemplateFields;
            var note = source?.Clone() ?? project.CreateNote();
            if (state.Source == null) {
                note.vibrato = state.Vibrato.Clone();
            } else if (state.VibratoReset) {
                note.vibrato.preset = state.Vibrato.preset;
                note.vibrato.shift = state.Vibrato.shift;
                note.vibrato.@in = state.Vibrato.@in;
                note.vibrato.@out = state.Vibrato.@out;
                note.vibrato.rateCurve = state.Vibrato.rateCurve?.Select(point => point.Clone()).ToList();
                note.vibrato.depthCurve = state.Vibrato.depthCurve?.Select(point => point.Clone()).ToList();
            }
            note = Apply(note, source, original, state.Current);
            // An inserted copy has no previous native length, even when its other fields have
            // a template for exact pitch and expression preservation.
            if (state.Source == null) note.vibrato.length = state.Current.VibratoType == 0 ? 0 : state.Current.VibratoLength;
            foreach (var pair in state.Expressions.Where(pair => ExpressionChanged(pair.Value))) {
                var points = pair.Value.Events.OrderBy(e => e.Pos).Select(e => new VibratoPoint {
                    x = (float)e.Pos, y = (float)NoteExpressionToNative(pair.Key, e.Value, state.Vibrato.preset) }).ToList();
                if (points.Count == 0) points.Add(new VibratoPoint {
                    x = 0, y = (float)NoteExpressionToNative(pair.Key, 64, state.Vibrato.preset) });
                if (pair.Key == "VR") note.vibrato.rateCurve = points;
                else note.vibrato.depthCurve = points;
            }
            note.vibrato.ValidateCurves();
            return note;
        }

        UNote Apply(UNote note, UNote? source, NoteFields? original, NoteFields current) {
            note.position = current.Pos;
            note.duration = current.Dur;
            if (original == null || current.Lyric != original.Lyric) {
                note.lyric = current.Lyric == Melisma ? Extender : KatakanaToHiragana(current.Lyric);
            }
            if (source == null || original == null) {
                note.tone = current.NoteNum;
                note.pitchAddress = PitchResolver.NearestAddress(tuning, current.NoteNum, true);
            } else if (current.NoteNum != original.NoteNum) {
                int delta = current.NoteNum - original.NoteNum;
                // A semitone delta moves the physical pitch, keeping any microtonal residual.
                if (source.pitchAddress != null) {
                    note.pitchAddress = PitchResolver.NearestAddress(tuning, source.ResolvedTone + delta, true);
                } else {
                    note.tone = source.tone + delta;
                }
            }
            note.RefreshPitch(project, track);
            if (original == null ? current.Velocity != DefaultVelocity : current.Velocity != original.Velocity) {
                int native = VelocityToNative(current.Velocity);
                note.SetExpression(project, track, Format.Ustx.VEL, new float?[] { native == 100 ? null : native });
            }
            if (original == null || current.VibratoType != original.VibratoType || current.VibratoLength != original.VibratoLength) {
                if (current.VibratoType == 0) {
                    if (original != null || note.vibrato.length > 0) {
                        note.vibrato.length = 0;
                    }
                } else {
                    note.vibrato.length = current.VibratoLength;
                }
            }
            return note;
        }
    }
}
