using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using OpenUtau.App.ViewModels;
using OpenUtau.Core.Microtonal;

namespace OpenUtau.App.Views {
    public sealed class MidiInputDialog : Window {
        readonly MidiInputViewModel viewModel;
        readonly ComboBox devices = new ComboBox { MinWidth = 280 };
        readonly ComboBox channels = new ComboBox { MinWidth = 120 };
        readonly Button connect = new Button { Content = XenStrings.Get("midi.connect") };
        readonly Button record = new Button { Content = XenStrings.Get("midi.record") };
        readonly TextBlock status = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };

        public MidiInputDialog(NotesViewModel notesView) {
            XenDialogStyles.Apply(this);
            Title = XenStrings.Get("midi.title");
            Width = 470;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            viewModel = new MidiInputViewModel(notesView);
            viewModel.PropertyChanged += (_, _) => Update();

            channels.ItemsSource = new[] { XenStrings.Get("midi.allChannels") }.Concat(Enumerable.Range(1, 16).Select(n => XenStrings.Get("midi.channel", n)));
            channels.SelectedIndex = 0;
            channels.SelectionChanged += (_, _) => {
                try { viewModel.SetChannel(channels.SelectedIndex <= 0 ? null : channels.SelectedIndex - 1); }
                catch (Exception e) { status.Text = e.Message; }
            };

            var refresh = new Button { Content = XenStrings.Get("midi.refresh") };
            refresh.Click += (_, _) => { viewModel.RefreshDevices(); SelectDevices(); };
            connect.Click += (_, _) => {
                try {
                    if (viewModel.IsConnected) { viewModel.Disconnect(); }
                    else if (devices.SelectedItem is MidiInputDeviceInfo device) {
                        viewModel.Connect(device, channels.SelectedIndex <= 0 ? null : channels.SelectedIndex - 1);
                    }
                } catch (Exception e) { status.Text = e.Message; }
            };
            record.Click += (_, _) => {
                try {
                    if (viewModel.IsRecording) { viewModel.StopRecording(); }
                    else { viewModel.StartRecording(); }
                } catch (Exception e) { status.Text = e.Message; }
            };
            var preview = new CheckBox { Content = XenStrings.Get("midi.preview"), IsChecked = true };
            preview.IsCheckedChanged += (_, _) => {
                viewModel.Preview = preview.IsChecked == true;
                if (!viewModel.Preview) { viewModel.StopPreview(); }
            };
            var close = new Button { Content = XenStrings.Get("action.close") };
            close.Click += (_, _) => Close();
            var deviceRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            deviceRow.Children.Add(devices);
            deviceRow.Children.Add(refresh);
            var inputRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            inputRow.Children.Add(channels);
            inputRow.Children.Add(preview);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            buttons.Children.Add(connect);
            buttons.Children.Add(record);
            buttons.Children.Add(close);
            Content = new StackPanel {
                Margin = new Thickness(20), Spacing = 14,
                Children = {
                    new TextBlock { Text = XenStrings.Get("midi.device") }, deviceRow, inputRow, buttons, status,
                },
            };
            Closed += (_, _) => viewModel.Dispose();
            SelectDevices();
            Update();
        }

        void SelectDevices() {
            devices.ItemsSource = viewModel.Devices;
            devices.SelectedIndex = viewModel.Devices.Count > 0 ? 0 : -1;
            Update();
        }

        void Update() {
            connect.Content = viewModel.IsConnected ? XenStrings.Get("midi.disconnect") : XenStrings.Get("midi.connect");
            connect.IsEnabled = viewModel.IsConnected || devices.SelectedItem != null;
            devices.IsEnabled = !viewModel.IsConnected;
            record.Content = viewModel.IsRecording ? XenStrings.Get("midi.stopRecord") : XenStrings.Get("midi.record");
            record.IsEnabled = viewModel.IsConnected;
            status.Text = viewModel.Status;
        }
    }
}
