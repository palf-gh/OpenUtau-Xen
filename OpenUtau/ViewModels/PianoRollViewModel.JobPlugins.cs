using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using OpenUtau.App.Controls;
using OpenUtau.App.Views;
using OpenUtau.Core;
using OpenUtau.Core.JobPlugins;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace OpenUtau.App.ViewModels {
    public partial class PianoRollViewModel {
        private ReactiveCommand<JobPluginInfo, RxVoid>? jobPluginCommand;
        private bool jobPluginRunning;

        private void InitJobPlugins() {
            jobPluginCommand = ReactiveCommand.CreateFromTask<JobPluginInfo>(RunJobPlugin);
            JobPluginManager.Inst.CatalogChanged += (_, _) => Dispatcher.UIThread.Post(LoadJobPlugins);
            LoadJobPlugins();
        }

        private void LoadJobPlugins() {
            JobPlugins.Clear();
            var plugins = JobPluginManager.Inst.Catalog.Plugins;
            JobPlugins.AddRange(plugins
                .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Select(plugin => new MenuItemViewModel() {
                    Header = plugin.FullySupported
                        ? plugin.DisplayName
                        : XenStrings.Get("jobplugin.limited", plugin.DisplayName),
                    Command = jobPluginCommand,
                    CommandParameter = plugin,
                }));
            if (plugins.Count == 0) {
                JobPlugins.Add(new MenuItemViewModel() {
                    Header = XenStrings.Get("jobplugin.none"),
                    IsEnabled = false,
                });
            }
            JobPlugins.Add(new MenuItemViewModel() { // Separator
                Header = "-",
                Height = 1
            });
            JobPlugins.Add(new MenuItemViewModel() {
                Header = XenStrings.Get("jobplugin.rescan"),
                Command = ReactiveCommand.Create(() => JobPluginManager.Inst.Rescan()),
            });
        }

        private static void Notice(string message) {
            DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, message));
        }

        private async Task RunJobPlugin(JobPluginInfo plugin) {
            var part = NotesViewModel.Part;
            var project = NotesViewModel.Project;
            if (part == null || project == null || jobPluginRunning) {
                return;
            }
            jobPluginRunning = true;
            var process = new JobPluginProcessParameters {
                BeginTick = 0,
                EndTick = part.Duration,
                SongTick = DocManager.Inst.playPosTick - part.position,
            };
            if (!NotesViewModel.Selection.IsEmpty) {
                process.BeginTick = NotesViewModel.Selection.Min(n => n.position);
                process.EndTick = NotesViewModel.Selection.Max(n => n.End);
            }
            DocManager.Inst.ExecuteCmd(new LoadingNotification(typeof(PianoRoll), true, "job plugin"));
            try {
                var session = JobPluginRunner.Prepare(project, part, process, new JobPluginInteraction(plugin.DisplayName));
                string fingerprint = JobPluginRunner.Fingerprint(project, part);
                var result = await Task.Run(() => JobPluginRunner.Run(plugin, session, new JobPluginLimits(), buildEdit: false));
                if (!result.Accepted) {
                    return;
                }
                if (DocManager.Inst.Project != project || fingerprint != JobPluginRunner.Fingerprint(project, part)) {
                    Notice(XenStrings.Get("jobplugin.conflict", plugin.DisplayName));
                    return;
                }
                // Built here, after the conflict check, so live notes are cloned on the document thread.
                var edit = session.BuildEdit();
                if (edit.IsEmpty) {
                    return;
                }
                NotesViewModel.DeselectNotes();
                JobPluginRunner.Apply(DocManager.Inst, project, part, edit, "xen.jobplugin.undo");
            } catch (JobPluginException e) {
                // Expected compatibility stops: the project is unchanged, so a status-bar line is enough.
                Serilog.Log.Warning(e, "JOB plugin {0} stopped", plugin.FilePath);
                Notice(XenStrings.Get("jobplugin.failed", plugin.DisplayName, e.Message));
            } catch (Exception e) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(
                    XenStrings.Get("jobplugin.failed", plugin.DisplayName, e.Message), e));
            } finally {
                DocManager.Inst.ExecuteCmd(new LoadingNotification(typeof(PianoRoll), false, "job plugin"));
                jobPluginRunning = false;
            }
        }
    }
}
