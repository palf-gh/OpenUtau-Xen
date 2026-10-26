using System;
using System.IO;
using System.Linq;
using System.Text;
using MoonSharp.Interpreter;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Core.JobPlugins {
    public sealed class JobPluginRunResult {
        public int ReturnCode { get; set; }
        public bool Cancelled { get; set; }
        /// <summary>main returned 0 and no dialog was cancelled.</summary>
        public bool Accepted => ReturnCode == 0 && !Cancelled;
        /// <summary>Null when the run must not change the project.</summary>
        public JobPluginEdit? Edit { get; set; }
    }

    public static class JobPluginRunner {
        /// <summary>
        /// Captures the part on the calling thread, then runs the plugin against the detached copy.
        /// The returned edit has not been applied.
        /// </summary>
        public static JobPluginSession Prepare(UProject project, UVoicePart part, JobPluginProcessParameters process,
            IJobPluginInteraction? interaction) => new JobPluginSession(project, part, process, interaction);

        public static string Fingerprint(UProject project, UVoicePart part) {
            var sb = new StringBuilder();
            sb.Append(project.parts.IndexOf(part)).Append('|').Append(part.position).Append('|')
                .Append(part.Duration).Append('|').Append(part.name).Append('|');
            foreach (var note in part.notes) {
                sb.Append(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(note)).Append(':')
                    .Append(note.position).Append(',').Append(note.duration).Append(',').Append(note.tone).Append(',')
                    .Append(note.pitchAddress?.Degree).Append(',').Append(note.pitchAddress?.ResidualCents).Append(',')
                    .Append(note.lyric).Append(';');
            }
            foreach (var curve in part.curves) {
                sb.Append(curve.abbr).Append('=').Append(string.Join(",", curve.xs)).Append('/').Append(string.Join(",", curve.ys)).Append(';');
            }
            return sb.ToString();
        }

        /// <param name="buildEdit">False leaves <see cref="JobPluginRunResult.Edit"/> null so the caller can
        /// build it on the document thread after checking for conflicts.</param>
        public static JobPluginRunResult Run(JobPluginInfo plugin, JobPluginSession session, JobPluginLimits limits, bool buildEdit = true) {
            string source = JobPluginCatalog.ReadSource(plugin.FilePath, limits.MaxSourceBytes);
            string scriptDir = Path.GetDirectoryName(plugin.FilePath) ?? string.Empty;
            string tempDir = Path.Combine(Path.GetTempPath(), "OpenUtau-Xen-JobPlugin", Guid.NewGuid().ToString("N"));
            var runtime = new JobPluginRuntime(limits);
            session.Runtime = runtime;
            Directory.CreateDirectory(tempDir);
            try {
                var script = runtime.CreateScript(scriptDir, message => session.Log.Add(message));
                session.Register(script);
                int code = runtime.Run(script, source, "@" + Path.GetFileName(plugin.FilePath), s => {
                    var env = new Table(s);
                    env["scriptDir"] = scriptDir + Path.DirectorySeparatorChar;
                    env["scriptName"] = Path.GetFileName(plugin.FilePath);
                    env["tempDir"] = tempDir + Path.DirectorySeparatorChar;
                    return new[] { DynValue.NewTable(session.ProcessParam(s)), DynValue.NewTable(env) };
                });
                var result = new JobPluginRunResult { ReturnCode = code, Cancelled = session.Cancelled };
                if (buildEdit && result.Accepted) {
                    result.Edit = session.BuildEdit();
                }
                return result;
            } finally {
                try {
                    Directory.Delete(tempDir, true);
                } catch (IOException) {
                } catch (UnauthorizedAccessException) {
                }
            }
        }

        /// <summary>Applies an edit as one undo group. Must run on the document's main thread.</summary>
        public static void Apply(DocManager docManager, UProject project, UVoicePart part, JobPluginEdit edit, string undoName) {
            if (edit.IsEmpty) {
                return;
            }
            docManager.StartUndoGroup(undoName);
            try {
                if (edit.Position != null) {
                    docManager.ExecuteCmd(new MovePartCommand(project, part, edit.Position.Value, part.trackNo));
                }
                if (edit.Duration != null) {
                    docManager.ExecuteCmd(new ResizeVoicePartCommand(project, part, edit.Duration.Value - part.Duration, false));
                }
                if (edit.Name != null) {
                    docManager.ExecuteCmd(new RenamePartCommand(project, part, edit.Name));
                }
                if (edit.RemovedNotes.Count > 0) {
                    docManager.ExecuteCmd(new RemoveNoteCommand(part, edit.RemovedNotes));
                }
                if (edit.AddedNotes.Count > 0) {
                    docManager.ExecuteCmd(new AddNoteCommand(part, edit.AddedNotes));
                }
                foreach (var (abbr, curve) in edit.Curves.OrderBy(c => c.Key, StringComparer.Ordinal)) {
                    var old = part.curves.FirstOrDefault(c => c.abbr == abbr);
                    docManager.ExecuteCmd(new MergedSetCurveCommand(project, part, abbr,
                        old?.xs.ToArray() ?? Array.Empty<int>(), old?.ys.ToArray() ?? Array.Empty<int>(), curve.xs, curve.ys));
                }
            } catch {
                docManager.RollBackUndoGroup();
                docManager.EndUndoGroup();
                throw;
            }
            docManager.EndUndoGroup();
        }
    }
}
