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
    /// </summary>
    public class JobPluginCorpusTest {
        sealed class AutoAccept : IJobPluginInteraction {
            public bool ShowDialog(JobPluginDialog dialog) => true;
            public int ShowMessage(string message, int flags) => flags == 0 ? JobPluginApi.ResultOk : JobPluginApi.ResultYes;
        }

        static (UProject, UVoicePart) Project() {
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
            var catalog = JobPluginCatalog.Scan(dirs.Split(';', StringSplitOptions.RemoveEmptyEntries));
            var report = new StringBuilder();
            report.AppendLine("| Plugin | Static scan | Result |");
            report.AppendLine("| --- | --- | --- |");
            foreach (var plugin in catalog.Plugins) {
                var (project, part) = Project();
                string outcome;
                try {
                    var session = JobPluginRunner.Prepare(project, part,
                        new JobPluginProcessParameters { BeginTick = 0, EndTick = part.Duration, SongTick = 960 }, new AutoAccept());
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
    }
}
