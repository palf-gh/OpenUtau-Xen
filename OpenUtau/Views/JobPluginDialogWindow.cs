using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Threading;
using OpenUtau.Core.JobPlugins;

namespace OpenUtau.App.Views {
    /// <summary>Modal form for a JOB plugin's VSDlg* fields. Captions are shown as plain text.</summary>
    public sealed class JobPluginDialogWindow : Window {
        readonly JobPluginDialog dialog;
        readonly Func<bool>[] commits;
        readonly TextBlock error = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap, IsVisible = false };
        bool accepted;

        public JobPluginDialogWindow(JobPluginDialog dialog, string pluginName) {
            this.dialog = dialog;
            XenDialogStyles.Apply(this);
            Title = string.IsNullOrEmpty(dialog.Title) ? pluginName : dialog.Title;
            Width = 420;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,12,*"), RowSpacing = 8 };
            commits = new Func<bool>[dialog.Fields.Count];
            for (int i = 0; i < dialog.Fields.Count; i++) {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var field = dialog.Fields[i];
                var caption = new TextBlock { Text = field.Caption, VerticalAlignment = VerticalAlignment.Center, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 200 };
                var (control, commit) = Editor(field);
                Grid.SetRow(caption, i);
                Grid.SetRow(control, i);
                Grid.SetColumn(control, 2);
                grid.Children.Add(caption);
                grid.Children.Add(control);
                commits[i] = commit;
            }
            var ok = new Button { Content = ThemeManager.GetString("button.ok"), IsDefault = true };
            var cancel = new Button { Content = ThemeManager.GetString("button.cancel"), IsCancel = true };
            ok.Click += (_, _) => {
                if (commits.All(c => c())) {
                    accepted = true;
                    Close();
                } else {
                    error.Text = XenStrings.Get("jobplugin.invalid");
                    error.IsVisible = true;
                }
            };
            cancel.Click += (_, _) => Close();
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            Content = new ScrollViewer {
                Content = new StackPanel {
                    Margin = new Thickness(20), Spacing = 14,
                    Children = { grid, error, buttons },
                },
            };
        }

        static (Control, Func<bool>) Editor(JobPluginDialogField field) {
            switch (field.Type) {
                case 1: {
                    var box = new CheckBox { IsChecked = field.Value is true };
                    return (box, () => { field.Value = box.IsChecked == true; return true; });
                }
                case 4: {
                    var combo = new ComboBox { ItemsSource = field.Choices, SelectedItem = field.Value as string, HorizontalAlignment = HorizontalAlignment.Stretch };
                    if (combo.SelectedIndex < 0) combo.SelectedIndex = 0;
                    return (combo, () => { field.Value = combo.SelectedItem as string ?? field.Choices[0]; return true; });
                }
                case 0:
                case 2: {
                    var text = new TextBox { Text = Convert.ToString(field.Value, CultureInfo.InvariantCulture) };
                    return (text, () => {
                        string value = text.Text?.Trim() ?? string.Empty;
                        bool valid;
                        if (field.Type == 0) {
                            valid = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i);
                            if (valid) field.Value = i;
                        } else {
                            valid = double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)
                                && double.IsFinite(d);
                            if (valid) field.Value = d;
                        }
                        text.Classes.Set("error", !valid);
                        return valid;
                    });
                }
                default: {
                    var text = new TextBox { Text = field.Value as string ?? string.Empty };
                    return (text, () => { field.Value = text.Text ?? string.Empty; return true; });
                }
            }
        }

        public async Task<bool> ShowFor(Window owner) {
            await ShowDialog(owner);
            return accepted;
        }
    }

    /// <summary>Marshals a plugin's dialogs and messages from its worker thread to the UI thread.</summary>
    public sealed class JobPluginInteraction : IJobPluginInteraction {
        readonly string pluginName;

        public JobPluginInteraction(string pluginName) {
            this.pluginName = pluginName;
        }

        static Window? Owner() {
            var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            return desktop?.Windows.FirstOrDefault(w => w.IsActive) ?? desktop?.MainWindow;
        }

        public bool ShowDialog(JobPluginDialog dialog) => Dispatcher.UIThread.InvokeAsync(async () => {
            var owner = Owner();
            return owner != null && await new JobPluginDialogWindow(dialog, pluginName).ShowFor(owner);
        }).GetAwaiter().GetResult();

        public int ShowMessage(string message, int flags) => Dispatcher.UIThread.InvokeAsync(async () => {
            var owner = Owner();
            if (owner == null) {
                return flags == JobPluginApi.MessageOk ? JobPluginApi.ResultOk : JobPluginApi.ResultNo;
            }
            var buttons = flags == JobPluginApi.MessageYesNo ? MessageBox.MessageBoxButtons.YesNo : MessageBox.MessageBoxButtons.Ok;
            var result = await MessageBox.Show(owner, message, pluginName, buttons);
            return result switch {
                MessageBox.MessageBoxResult.Yes => JobPluginApi.ResultYes,
                MessageBox.MessageBoxResult.No => JobPluginApi.ResultNo,
                _ => flags == JobPluginApi.MessageYesNo ? JobPluginApi.ResultNo : JobPluginApi.ResultOk,
            };
        }).GetAwaiter().GetResult();
    }
}
