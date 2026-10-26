using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenUtau.Core.Format;
using OpenUtau.Core.JobPlugins;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Test {
    /// <summary>
    /// Opt-in run of user-installed JOB plugins, read in place. Set OPENUTAU_XEN_JOB_PLUGIN_DIRS to
    /// folders separated by ';' and optionally OPENUTAU_XEN_JOB_REPORT to a Markdown output path.
    /// Dialogs are accepted with their initial values and Yes/No questions are answered Yes.
    /// OPENUTAU_XEN_JOB_FILTER limits the run to plugins whose file name contains one of its
    /// '|'-separated parts. OPENUTAU_XEN_JOB_VIBRATO=1 gives the first note vibrato curves and the
    /// third scalar vibrato, puts the playhead inside the first note and reports how far the
    /// resulting vibrato departs from the original. OPENUTAU_XEN_JOB_SONG_TICK moves the playhead.
    /// OPENUTAU_XEN_JOB_PRESET gives the first note that vibrato type instead of custom curves.
    /// OPENUTAU_XEN_JOB_DIALOG sets text or choice fields by name, as name=value pairs separated by ';'.
    /// </summary>
    public class JobPluginCorpusTest {
        sealed class AutoAccept : IJobPluginInteraction {
            readonly Dictionary<string, string> values = (Environment.GetEnvironmentVariable("OPENUTAU_XEN_JOB_DIALOG") ?? "")
                .Split(';', StringSplitOptions.RemoveEmptyEntries).Select(pair => pair.Split('=', 2))
                .Where(pair => pair.Length == 2).ToDictionary(pair => pair[0], pair => pair[1]);
            public bool ShowDialog(JobPluginDialog dialog) {
                foreach (var field in dialog.Fields) {
                    if ((field.Type == 3 || field.Type == 4) && values.TryGetValue(field.Name, out var value)) {
                        field.Value = value;
                    }
                }
                return true;
            }
            public int ShowMessage(string message, int flags) => flags == 0 ? JobPluginApi.ResultOk : JobPluginApi.ResultYes;
        }

        static (UProject, UVoicePart) Project(bool vibrato) {
            var project = Ustx.Create();
            var part = new UVoicePart { trackNo = 0, position = 1920, Duration = 7680, name = "corpus" };
            project.parts.Add(part);
            string[] lyrics = { "あ", "い", "う", "え", "お", "か", "き", "く" };
            for (int i = 0; i < lyrics.Length; i++) {
                var note = project.CreateNote(60 + i % 5, i * 480, 480);
                note.lyric = lyrics[i];
                part.notes.Add(note);
            }
            part.curves.Add(new UCurve(project.expressions[Ustx.PITD]) { xs = { 0, 240, 480 }, ys = { 0, 50, 0 } });
            if (vibrato) {
                var curved = part.notes.ElementAt(0).vibrato;
                curved.length = 80;
                curved.rateCurve = new List<VibratoPoint> { new VibratoPoint(0.2f, 4), new VibratoPoint(1, 8) };
                curved.depthCurve = new List<VibratoPoint> { new VibratoPoint(0.2f, 20), new VibratoPoint(1, 80) };
                if (int.TryParse(Environment.GetEnvironmentVariable("OPENUTAU_XEN_JOB_PRESET"), out int preset)) {
                    VibratoPresets.Apply(curved, preset);
                }
                var scalar = part.notes.ElementAt(2).vibrato;
                scalar.length = 60;
                scalar.period = 180;
                scalar.depth = 30;
            }
            project.timeAxis.BuildSegments(project);
            foreach (var note in part.notes) note.RefreshPitch(project, project.tracks[0]);
            return (project, part);
        }

        [Fact]
        public void RunInstalledPlugins() {
            string? dirs = Environment.GetEnvironmentVariable("OPENUTAU_XEN_JOB_PLUGIN_DIRS");
            if (string.IsNullOrWhiteSpace(dirs)) {
                return;
            }
            bool vibrato = Environment.GetEnvironmentVariable("OPENUTAU_XEN_JOB_VIBRATO") == "1";
            var filter = Environment.GetEnvironmentVariable("OPENUTAU_XEN_JOB_FILTER")?
                .Split('|', StringSplitOptions.RemoveEmptyEntries);
            int songTick = int.TryParse(Environment.GetEnvironmentVariable("OPENUTAU_XEN_JOB_SONG_TICK"), out int tick)
                ? tick : vibrato ? 240 : 960;
            var catalog = JobPluginCatalog.Scan(dirs.Split(';', StringSplitOptions.RemoveEmptyEntries));
            var report = new StringBuilder();
            report.AppendLine("| Plugin | Static scan | Result |");
            report.AppendLine("| --- | --- | --- |");
            foreach (var plugin in catalog.Plugins) {
                if (filter != null && !filter.Any(f => Path.GetFileName(plugin.FilePath).Contains(f, StringComparison.OrdinalIgnoreCase))) {
                    continue;
                }
                var (project, part) = Project(vibrato);
                string outcome;
                try {
                    var session = JobPluginRunner.Prepare(project, part,
                        new JobPluginProcessParameters { BeginTick = 0, EndTick = part.Duration, SongTick = songTick }, new AutoAccept());
                    var result = JobPluginRunner.Run(plugin, session, new JobPluginLimits());
                    if (result.Edit == null) {
                        outcome = result.Cancelled ? "cancelled" : $"returned {result.ReturnCode}";
                    } else if (result.Edit.IsEmpty) {
                        outcome = "ran, no change";
                    } else {
                        var e = result.Edit;
                        var parts = new List<string>();
                        if (e.RemovedNotes.Count > 0 || e.AddedNotes.Count > 0) parts.Add($"notes −{e.RemovedNotes.Count}/+{e.AddedNotes.Count}");
                        if (e.AddedNotes.Count > 0 && e.AddedNotes.Any(n => !part.notes.Any(o => o.lyric == n.lyric))) {
                            var lyrics = part.notes.Except(e.RemovedNotes).Concat(e.AddedNotes).OrderBy(n => n.position).Select(n => n.lyric);
                            parts.Add("lyrics " + string.Join(" ", lyrics));
                        }
                        if (e.Curves.Count > 0) parts.Add("curves " + string.Join(",", e.Curves.Keys));
                        if (e.Curves.TryGetValue(Ustx.PITD, out var pitd)) {
                            // The synthetic pitd ramp renders 25 cents at tick 120.
                            var after = new UCurve(project.expressions[Ustx.PITD]) { xs = pitd.xs.ToList(), ys = pitd.ys.ToList() };
                            parts.Add($"pitd@120 25→{after.Sample(120)}");
                        }
                        if (e.Position != null || e.Duration != null || e.Name != null) parts.Add("part");
                        if (vibrato) parts.Add(DescribeVibrato(project, part, e));
                        outcome = "ran: " + string.Join("; ", parts);
                    }
                } catch (Exception e) {
                    outcome = "failed: " + e.Message.Split('\n')[0].Replace("|", "/");
                }
                string scan = plugin.FullySupported ? "ok" : plugin.Status;
                report.AppendLine($"| {plugin.DisplayName} | {scan} | {outcome} |");
            }
            foreach (var d in catalog.Diagnostics) {
                report.AppendLine($"| (diagnostic) | {d.Replace("|", "/")} | |");
            }
            string output = Environment.GetEnvironmentVariable("OPENUTAU_XEN_JOB_REPORT")
                ?? Path.Combine(AppContext.BaseDirectory, "job-plugin-corpus.md");
            File.WriteAllText(output, report.ToString(), new UTF8Encoding(false));
            Assert.NotEmpty(catalog.Plugins);
        }

        // Compares vibrato offsets (excluding the base pitch) of the original and edited notes.
        static string DescribeVibrato(UProject project, UVoicePart part, JobPluginEdit edit) {
            var after = part.notes.Except(edit.RemovedNotes).Concat(edit.AddedNotes).OrderBy(n => n.position).ToList();
            double Offset(UNote note, int tick) {
                if (note.vibrato.length <= 0) {
                    return 0;
                }
                // Detached notes have no cached duration in ms, so derive the period from the time axis.
                double startMs = project.timeAxis.TickPosToMsPos(part.position + note.position);
                float nPeriod = (float)project.timeAxis.TicksBetweenMsPos(startMs, startMs + note.vibrato.period) / note.duration;
                var point = note.vibrato.HasCurves
                    ? note.vibrato.EvaluateAtTick(project, part, note, tick)
                    : note.vibrato.Evaluate((float)(tick - note.position) / note.duration, nPeriod, note);
                return (point.Y - note.AdjustedTone) * 100;
            }
            double maxCents = 0, maxRate = 0, maxDepth = 0;
            int compared = 0;
            float NPos(UNote note, int tick) => (float)(tick - note.position) / note.duration;
            for (int tick = 0; tick < 1440; tick += 5) {
                var original = part.notes.FirstOrDefault(n => n.position <= tick && tick < n.End);
                var edited = after.FirstOrDefault(n => n.position <= tick && tick < n.End);
                if (original == null || edited == null) {
                    continue;
                }
                maxCents = Math.Max(maxCents, Math.Abs(Offset(original, tick) - Offset(edited, tick)));
                compared++;
                float before = NPos(original, tick), now = NPos(edited, tick);
                if (edited.vibrato.length > 0 && now >= edited.vibrato.NormalizedStart) {
                    maxRate = Math.Max(maxRate, Math.Abs(original.vibrato.SampleRate(before) - edited.vibrato.SampleRate(now)));
                    maxDepth = Math.Max(maxDepth, Math.Abs(original.vibrato.SampleDepth(before) - edited.vibrato.SampleDepth(now)));
                }
            }
            var notes = after.Where(n => n.position < 1440).Select(n =>
                $"{n.position}+{n.duration} preset {n.vibrato.preset?.ToString() ?? "-"} len {n.vibrato.length:0.#} in {n.vibrato.@in:0.#} out {n.vibrato.@out:0.#} shift {n.vibrato.shift:0.#} rate {Points(n.vibrato.rateCurve)} depth {Points(n.vibrato.depthCurve)}");
            string Points(List<VibratoPoint>? points) => points == null ? "-" : string.Join(" ", points.Select(p => $"{p.x:0.###}:{p.y:0.##}"));
            return $"vibrato max Δ {maxCents:0.###} cents over {compared} ticks, active rate Δ {maxRate:0.###} Hz, depth Δ {maxDepth:0.###} cents [{string.Join(", ", notes)}]";
        }
    }
}
