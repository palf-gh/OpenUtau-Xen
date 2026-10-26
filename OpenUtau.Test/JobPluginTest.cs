using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.JobPlugins;
using OpenUtau.Core.Microtonal;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Test {
    public class JobPluginTest : IDisposable {
        readonly string root = Path.Combine(Path.GetTempPath(), "OpenUtau-Xen-JobTest", Guid.NewGuid().ToString("N"));

        public JobPluginTest() {
            Directory.CreateDirectory(root);
        }

        public void Dispose() {
            try { Directory.Delete(root, true); } catch (IOException) { }
        }

        sealed class Interaction : IJobPluginInteraction {
            public bool Accept = true;
            public Action<JobPluginDialog>? Edit;
            public int MessageResult = JobPluginApi.ResultYes;
            public List<(string text, int flags)> Messages = new List<(string, int)>();
            public bool ShowDialog(JobPluginDialog dialog) {
                Edit?.Invoke(dialog);
                return Accept;
            }
            public int ShowMessage(string message, int flags) {
                Messages.Add((message, flags));
                return flags == 0 ? JobPluginApi.ResultOk : MessageResult;
            }
        }

        string Write(string name, string body, string? dir = null) {
            string folder = dir ?? root;
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, body, new UTF8Encoding(false));
            return path;
        }

        static string Plugin(string main, string name = "Fixture") => $@"
function manifest()
  return {{ name = ""{name}"", comment = ""synthetic"", author = ""test"", pluginID = ""{{0}}"",
           pluginVersion = ""1.0.0.0"", apiVersion = ""3.0.0.1"" }}
end
function main(processParam, envParam)
{main}
end
";

        static (UProject project, UVoicePart part) Project(bool native, int partPosition = 0) {
            var project = Ustx.Create();
            if (native) project.pitchSystem = TuningPresets.EqualDivision(53);
            var part = new UVoicePart { trackNo = 0, position = partPosition, Duration = 3840, name = "part" };
            project.parts.Add(part);
            var first = project.CreateNote(60, 0, 480);
            var second = project.CreateNote(62, 480, 480);
            if (native) {
                first.pitchAddress = new PitchAddress { Degree = 4, ResidualCents = 3.5 };
                second.pitchAddress = new PitchAddress { Degree = 9, ResidualCents = -1.25 };
            }
            first.lyric = "la";
            second.lyric = "li";
            part.notes.Add(first);
            part.notes.Add(second);
            project.timeAxis.BuildSegments(project);
            foreach (var note in part.notes) note.RefreshPitch(project, project.tracks[0]);
            return (project, part);
        }

        JobPluginRunResult Run(UProject project, UVoicePart part, string body, Interaction? ui = null,
            JobPluginLimits? limits = null, JobPluginProcessParameters? process = null) {
            string path = Write("Fixture.lua", Plugin(body));
            var info = JobPluginCatalog.Inspect(path);
            var session = JobPluginRunner.Prepare(project, part,
                process ?? new JobPluginProcessParameters { BeginTick = 0, EndTick = part.Duration }, ui ?? new Interaction());
            return JobPluginRunner.Run(info, session, limits ?? new JobPluginLimits());
        }

        [Fact]
        public void CatalogScansSeveralRootsWithoutRunningScripts() {
            string a = Path.Combine(root, "A");
            string b = Path.Combine(root, "B");
            Write("One.lua", "error('top level must not run')\n" + Plugin("return 0", "Same"), a);
            Write("Two.lua", Plugin("return 0", "Same"), Path.Combine(b, "Sub"));
            Write("Computed.lua", "function manifest() return { name = make() } end\nfunction main() return 0 end", b);
            Write("NoMain.lua", "function manifest() return {} end", b);
            File.WriteAllBytes(Path.Combine(b, "Bad.lua"), new byte[] { 0x66, 0xff, 0xfe });
            Write("Uses.lua", Plugin("-- VSInsertNoteExpression(n, e)\nos.execute('x') VSGetNextNoteExpression(n, 'VR')"), b);
            var result = JobPluginCatalog.Scan(new[] { a, b, a + Path.DirectorySeparatorChar, Path.Combine(root, "missing") });
            Assert.Equal(4, result.Plugins.Count);
            Assert.Equal(2, result.Plugins.Count(p => p.Name == "Same"));
            Assert.Equal(2, result.Plugins.Where(p => p.Name == "Same").Select(p => p.DisplayName).Distinct().Count());
            Assert.Equal("Computed", result.Plugins.Single(p => p.FilePath.EndsWith("Computed.lua")).Name);
            var uses = result.Plugins.Single(p => p.FilePath.EndsWith("Uses.lua"));
            Assert.Equal(new[] { "VSGetNextNoteExpression" }, uses.UnsupportedApis);
            Assert.Contains("os.execute", uses.RestrictedFeatures);
            Assert.Contains(result.Diagnostics, d => d.Contains("NoMain.lua"));
            Assert.Contains(result.Diagnostics, d => d.Contains("Bad.lua") && d.Contains("UTF-8"));
            Assert.Contains(result.Diagnostics, d => d.Contains("missing"));
        }

        [Fact]
        public void SandboxWithholdsSystemFacilities() {
            var (project, part) = Project(false);
            Write("setting.txt", "12\nsecond\n");
            string outside = Path.Combine(Path.GetDirectoryName(root)!, "outside.txt");
            var ok = Run(project, part, $@"
  local f = io.open(envParam.scriptDir .. 'setting.txt', 'r')
  local lines = {{}}
  for l in f:lines() do table.insert(lines, l) end
  f:close()
  assert(lines[1] == '12' and lines[2] == 'second')
  assert(io.open([[{outside}]], 'r') == nil)
  assert(io.open(envParam.scriptDir .. 'none.txt') == nil)
  assert(table.getn({{1, 2}}) == 2 and math.pow(2, 3) == 8 and math.log10(1000) == 3)
  local t = {{3, 1, 2}} table.sort(t) assert(t[1] == 1 and t[3] == 3)
  return 0");
            Assert.Equal(0, ok.ReturnCode);
            foreach (var body in new[] { "os.execute('dir') return 0", "io.open(envParam.scriptDir .. 'x.txt', 'w') return 0",
                "require('x') return 0", "return luanet ~= nil and 0 or (os.getenv('PATH') and 0)" }) {
                Assert.Throws<JobPluginException>(() => Run(project, part, body));
            }
        }

        [Theory]
        [InlineData("while true do end")]
        [InlineData("pcall(function() while true do end end)")]
        [InlineData("local t = {} for i = 1, 50 do t[i] = i end table.sort(t, function(a, b) while true do end end)")]
        public void RunawayScriptsStop(string body) {
            var (project, part) = Project(false);
            var limits = new JobPluginLimits { InstructionBudget = 5_000_000 };
            var e = Assert.Throws<JobPluginException>(() => Run(project, part, body + "\nreturn 0", limits: limits));
            Assert.Contains("budget", e.Message);
        }

        [Fact]
        public void CancellationStopsScript() {
            var (project, part) = Project(false);
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.Throws<OperationCanceledException>(() => Run(project, part, "while true do end", limits: new JobPluginLimits { Cancellation = cts.Token }));
        }

        [Fact]
        public void LyricOnlyEditKeepsExactMicrotonalPitch() {
            var (project, part) = Project(true);
            var original = part.notes.ToArray();
            var result = Run(project, part, @"
  VSSeekToBeginNote()
  local r, n = VSGetNextNote()
  while r == 1 do
    n.lyric = n.lyric .. 'x'
    assert(VSUpdateNote(n) == 1)
    r, n = VSGetNextNote()
  end
  return 0");
            var edit = result.Edit!;
            Assert.Equal(original, edit.RemovedNotes);
            Assert.Equal(new[] { "lax", "lix" }, edit.AddedNotes.Select(n => n.lyric));
            for (int i = 0; i < 2; i++) {
                Assert.Equal(original[i].pitchAddress!.Degree, edit.AddedNotes[i].pitchAddress!.Degree);
                Assert.Equal(original[i].pitchAddress!.ResidualCents, edit.AddedNotes[i].pitchAddress!.ResidualCents);
                Assert.Equal(original[i].ResolvedTone, edit.AddedNotes[i].ResolvedTone);
            }
        }

        [Fact]
        public void WriteBackOfUnchangedTablesIsNoOp() {
            var (project, part) = Project(true);
            var result = Run(project, part, @"
  VSSeekToBeginNote()
  local r, n = VSGetNextNoteEx()
  while r == 1 do
    VSUpdateNoteEx(n)
    r, n = VSGetNextNoteEx()
  end
  VSSeekToBeginControl('DYN')
  local s, c = VSGetNextControl('DYN')
  assert(s == 0 and next(c) == nil)
  return 0");
            Assert.True(result.Edit!.IsEmpty);
        }

        [Fact]
        public void SemitoneMoveKeepsResidual() {
            var (project, part) = Project(true);
            var first = part.notes.First();
            var result = Run(project, part, @"
  VSSeekToBeginNote()
  local r, n = VSGetNextNote()
  n.noteNum = n.noteNum + 2
  VSUpdateNote(n)
  return 0");
            var moved = result.Edit!.AddedNotes.Single();
            Assert.Equal(first.ResolvedTone + 2, moved.ResolvedTone, 9);
            Assert.Same(first, result.Edit.RemovedNotes.Single());
        }

        [Fact]
        public void IterationSurvivesRemovalAndInsertion() {
            var (project, part) = Project(false);
            var result = Run(project, part, @"
  VSSeekToBeginNote()
  local r, n = VSGetNextNote()
  local first = n
  assert(VSRemoveNote(n) == 1)
  assert(VSInsertNote({ posTick = 960, durTick = 240, noteNum = 64, lyric = 'new', velocity = 64 }) == 1)
  r, n = VSGetNextNote()
  assert(r == 1 and n.lyric == 'li')
  r, n = VSGetNextNote()
  assert(r == 0 and n.objID == nil)
  assert(VSUpdateNote(first) == 0)
  VSSeekToBeginNote()
  local count = 0
  r, n = VSGetNextNote()
  while r == 1 do count = count + 1 r, n = VSGetNextNote() end
  assert(count == 2)
  return 0");
            Assert.Equal("la", result.Edit!.RemovedNotes.Single().lyric);
            var added = result.Edit.AddedNotes.Single();
            Assert.Equal((960, 240, 64, "new"), (added.position, added.duration, added.tone, added.lyric));
        }

        [Fact]
        public void PartTempoAndMetreUseSongTicks() {
            var (project, part) = Project(false, partPosition: 1920);
            project.tempos.Add(new UTempo(2400, 150));
            project.timeSignatures.Add(new UTimeSignature(2, 3, 4));
            project.timeAxis.BuildSegments(project);
            var result = Run(project, part, @"
  local r, p = VSGetMusicalPart()
  assert(p.posTick == 1920 and p.playTime == 3840 and p.durTick == 3840)
  local s, bpm = VSGetTempoAt(p.posTick + 480)
  assert(bpm == 150, bpm)
  VSSeekToBeginTempo()
  local count = 0
  local r2, t = VSGetNextTempo()
  while r2 == 1 do count = count + 1 r2, t = VSGetNextTempo() end
  assert(count == 2)
  local s2, num, den = VSGetTimeSigAt(3840)
  assert(num == 3 and den == 4, num)
  p.name = 'renamed'
  p.posTick = 2400
  p.playTime = 3360
  assert(VSUpdateMusicalPart(p) == 1)
  return 0");
            Assert.Equal((2400, 3360, "renamed"), (result.Edit!.Position, result.Edit.Duration, result.Edit.Name));
        }

        [Fact]
        public void PitchBendMapsToPitchDeviationCents() {
            var (project, part) = Project(false);
            var result = Run(project, part, @"
  assert(VSInsertControl({ type = 'PBS', posTick = 0, value = 4 }) == 1)
  assert(VSInsertControl({ type = 'PIT', posTick = 100, value = 4096 }) == 1)
  assert(VSUpdateControlAt('PIT', 200, -8192) == 1)
  local s, v = VSGetControlAt('PIT', 150)
  assert(v == 4096)
  return 0");
            var (xs, ys) = result.Edit!.Curves[Ustx.PITD];
            Assert.Equal(new[] { 0, 99, 100, 199, 200, 3840 }, xs);
            Assert.Equal(new[] { 0, 0, 200, 200, -400, -400 }, ys);
        }

        [Fact]
        public void ComputedNumbersRoundToNearestEven() {
            var (project, part) = Project(false);
            var result = Run(project, part, @"
  VSInsertControl({ type = 'PIT', posTick = 22.5, value = 4095.5 })
  VSUpdateControlAt('PIT', 101.5, 409.6)
  return 0");
            Assert.Equal(new[] { 22, 101, 102, 3840 }, result.Edit!.Curves[Ustx.PITD].xs);
        }

        [Fact]
        public void ExistingPitchDeviationRoundTrips() {
            var (project, part) = Project(false);
            var curve = new UCurve(project.expressions[Ustx.PITD]) { xs = { 0, 100, 200 }, ys = { 0, 300, 0 } };
            part.curves.Add(curve);
            var result = Run(project, part, @"
  local s, pbs = VSGetControlAt('PBS', 100)
  local s2, pit = VSGetControlAt('PIT', 100)
  assert(pbs == 4, pbs)
  assert(math.abs(pit * pbs * 100 / 8192 - 300) < 0.1, pit)
  VSSeekToBeginControl('PIT')
  local r, c = VSGetNextControl('PIT')
  while r == 1 do VSUpdateControl(c) r, c = VSGetNextControl('PIT') end
  return 0");
            Assert.True(result.Edit!.IsEmpty);
            Assert.Throws<JobPluginException>(() => Run(project, part, @"
  VSUpdateControlAt('PBS', 0, 24)
  VSUpdateControlAt('PIT', 0, 8191)
  return 0"));
        }

        [Theory]
        [InlineData("VSUpdateControlAt('BRE', 0, 10)")]
        [InlineData("VSSeekToBeginNote() local r, n = VSGetNextNote() n.phonemes = 'a' VSUpdateNote(n)")]
        [InlineData("VSInsertNote({ posTick = 960, durTick = 240, noteNum = 60, phonemes = 'a' })")]
        [InlineData("VSSeekToBeginNote() local r, n = VSGetNextNoteEx() n.vibratoType = 3 VSUpdateNoteEx(n)")]
        [InlineData("VSInsertNote({ posTick = 0, durTick = 5, noteNum = 60 })")]
        [InlineData("VSMessageBox('x', 1)")]
        [InlineData("VSSeekToBeginNoteExpression({}, 'VR')")]
        public void UnsupportedOperationsAbort(string body) {
            var (project, part) = Project(false);
            var e = Assert.Throws<JobPluginException>(() => Run(project, part, body + "\nreturn 0"));
            Assert.Matches("VS|phonemes|vibratoType", e.Message);
        }

        [Fact]
        public void PhonemesFollowingALyricAreLeftToThePhonemiser() {
            var (project, part) = Project(false);
            var result = Run(project, part, @"
  VSSeekToBeginNote()
  local r, n = VSGetNextNote()
  n.lyric = 'ハ' n.phonemes = 'h a' n.phLock = 1
  assert(VSUpdateNote(n) == 1)
  r, n = VSGetNextNote()
  n.lyric = '-' n.phonemes = '-'
  assert(VSUpdateNote(n) == 1)
  assert(VSInsertNote({ posTick = 960, durTick = 240, noteNum = 64, lyric = 'ピ', phonemes = ""p' i"" }) == 1)
  return 0");
            Assert.Equal(new[] { "ハ", "+", "ピ" }, result.Edit!.AddedNotes.Select(n => n.lyric));
            // Changing a lyric and restoring it does not let a phoneme-only edit through.
            Assert.Throws<JobPluginException>(() => Run(project, part, @"
  VSSeekToBeginNote()
  local r, n = VSGetNextNote()
  n.lyric = 'x' n.phonemes = 'x' VSUpdateNote(n)
  n.lyric = 'la' VSUpdateNote(n)
  return 0"));
        }

        [Fact]
        public void ExtenderLyricRoundTripsAsMelisma() {
            var (project, part) = Project(false);
            part.notes.Last().lyric = "+";
            var result = Run(project, part, @"
  VSSeekToBeginNote()
  local r, n = VSGetNextNote()
  r, n = VSGetNextNote()
  assert(n.lyric == '-', n.lyric)
  VSUpdateNote(n)
  return 0");
            Assert.True(result.Edit!.IsEmpty);
        }

        [Fact]
        public void StrictAbortSurvivesPcall() {
            var (project, part) = Project(false);
            Assert.Throws<JobPluginException>(() => Run(project, part, "pcall(VSUpdateControlAt, 'BRE', 0, 10) return 0"));
        }

        [Fact]
        public void SparseCurvesReadAndEditAsRendered() {
            var (project, part) = Project(false);
            var curve = new UCurve(project.expressions[Ustx.PITD]) { xs = { 0, 480 }, ys = { 0, 400 } };
            part.curves.Add(curve);
            var original = curve.Clone();
            var result = Run(project, part, @"
  local s, v = VSGetControlAt('PIT', 120)
  local s2, pbs = VSGetControlAt('PBS', 120)
  assert(math.abs(v * pbs * 100 / 8192 - 100) < 1, v)
  local s3, after = VSGetControlAt('PIT', 600)
  assert(after == 0, after)
  local shift = 10 * 8192 / (100 * pbs)
  VSSeekToBeginControl('PIT')
  local r, c = VSGetNextControl('PIT')
  local events = {}
  while r == 1 do table.insert(events, c) r, c = VSGetNextControl('PIT') end
  for _, e in ipairs(events) do
    if e.posTick <= 480 then e.value = e.value + shift VSUpdateControl(e) end
  end
  return 0");
            var (xs, ys) = result.Edit!.Curves[Ustx.PITD];
            var edited = new UCurve(project.expressions[Ustx.PITD]) { xs = xs.ToList(), ys = ys.ToList() };
            for (int t = 0; t <= 480; t++) {
                // Held 5-tick steps lag a 400-cent ramp by under 5 cents.
                Assert.InRange(edited.Sample(t) - (original.Sample(t) + 10), -5, 1);
            }
            Assert.Equal(0, edited.Sample(700));
        }

        [Fact]
        public void DefaultValuedWritesToUnmappedControlsAreAllowed() {
            var (project, part) = Project(false);
            var result = Run(project, part, @"
  local s, d = VSGetDefaultControlValue('GEN')
  assert(d == 64)
  VSInsertControl({ type = 'GEN', posTick = 0, value = d })
  return 0");
            Assert.True(result.Edit!.IsEmpty);
        }

        [Fact]
        public void VelocityAndDynamicsUseDocumentedApproximations() {
            Assert.Equal(100, JobPluginSession.VelocityToNative(64));
            Assert.Equal(200, JobPluginSession.VelocityToNative(127));
            Assert.Equal(0, JobPluginSession.VelocityToNative(0));
            for (int v = 0; v <= 127; v++) {
                Assert.Equal(v, JobPluginSession.NativeToVelocity(JobPluginSession.VelocityToNative(v)));
            }
            Assert.Equal(0, JobPluginSession.DynToNative(64));
            Assert.Equal(-240, JobPluginSession.DynToNative(0));
            Assert.Equal(64, JobPluginSession.NativeToDyn(0));
        }

        [Fact]
        public void DialogValuesAndCancellation() {
            var (project, part) = Project(false);
            var ui = new Interaction {
                Edit = d => {
                    Assert.Equal("Title", d.Title);
                    Assert.Equal(new[] { "1/4", "1/8" }, d.Fields.Single(f => f.Type == 4).Choices);
                    d.Fields.Single(f => f.Name == "n").Value = 7;
                    d.Fields.Single(f => f.Name == "c").Value = "1/8";
                },
            };
            const string body = @"
  VSDlgSetDialogTitle('Title')
  local field = {}
  field.name = 'n' field.caption = 'Number' field.initialVal = 3 field.type = 0
  VSDlgAddField(field)
  field.name = 'c' field.caption = 'Choice' field.initialVal = '1/4,1/8' field.type = 4
  VSDlgAddField(field)
  field.name = 'b' field.caption = 'Flag' field.initialVal = 1 field.type = 1
  VSDlgAddField(field)
  VSSeekToBeginNote()
  local r, n = VSGetNextNote()
  n.lyric = 'changed'
  VSUpdateNote(n)
  if VSDlgDoModal() ~= 1 then return 0 end
  local s1, v = VSDlgGetIntValue('n')
  local s2, c = VSDlgGetStringValue('c')
  local s3, b = VSDlgGetBoolValue('b')
  assert(v == 7 and c == '1/8' and b == 1)
  return 0";
            var accepted = Run(project, part, body, ui);
            Assert.Single(accepted.Edit!.AddedNotes);
            ui.Accept = false;
            var cancelled = Run(project, part, body, ui);
            Assert.True(cancelled.Cancelled);
            Assert.Null(cancelled.Edit);
        }

        [Fact]
        public void MessageBoxYesNoReturnsWin32Codes() {
            var (project, part) = Project(false);
            var ui = new Interaction { MessageResult = JobPluginApi.ResultNo };
            var result = Run(project, part, "assert(VSMessageBox('ok?', 4) == 7) assert(VSMessageBox('info', 0) == 1) return 1", ui);
            Assert.Equal(1, result.ReturnCode);
            Assert.Null(result.Edit);
            Assert.Equal(new[] { 4, 0 }, ui.Messages.Select(m => m.flags));
        }

        [Fact]
        public void ApplyIsOneUndoGroup() {
            var (project, part) = Project(true);
            var previousPost = DocManager.Inst.PostOnUIThread ?? (_ => { });
            DocManager.Inst.Initialize(Thread.CurrentThread, System.Threading.Tasks.TaskScheduler.Default);
            DocManager.Inst.PostOnUIThread = _ => { };
            try {
                DocManager.Inst.ExecuteCmd(new LoadProjectNotification(project));
                var before = part.notes.ToArray();
                var result = Run(project, part, @"
  VSSeekToBeginNote()
  local r, n = VSGetNextNote()
  n.posTick = 1440
  VSUpdateNote(n)
  VSUpdateControlAt('PIT', 0, 1000)
  local r2, p = VSGetMusicalPart()
  p.name = 'job'
  VSUpdateMusicalPart(p)
  return 0");
                JobPluginRunner.Apply(DocManager.Inst, project, part, result.Edit!, "command.batch.plugin");
                Assert.Equal("job", part.name);
                Assert.Equal(new[] { 480, 1440 }, part.notes.Select(n => n.position));
                Assert.NotEmpty(part.curves.Single(c => c.abbr == Ustx.PITD).xs);
                DocManager.Inst.Undo();
                Assert.Equal("part", part.name);
                Assert.Equal(before, part.notes.ToArray());
                Assert.Empty(part.curves.Single(c => c.abbr == Ustx.PITD).xs);
            } finally {
                DocManager.Inst.PostOnUIThread = previousPost;
            }
        }
    }
}
