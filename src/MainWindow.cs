// SPDX-License-Identifier: MIT
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

[assembly: AssemblyTitle("GameSir G7 Pro Pairing")]
[assembly: AssemblyDescription("Independent open-source USB pairing utility for GameSir G7 Pro")]
[assembly: AssemblyVersion("0.2.0.0")]
[assembly: AssemblyFileVersion("0.2.0.0")]

namespace GameSirPairing
{
    internal sealed class MainWindow
    {
        private readonly Window window;
        private readonly ComboBox receivers, controllers;
        private readonly Button scan, read, pair;
        private readonly TextBlock receiverInfo, controllerInfo, statusTitle, statusText;
        private readonly TextBox activity;
        private readonly Border statusPanel;
        private readonly ProgressBar progress;
        private readonly object logLock = new object();
        private readonly string dataDirectory;
        private bool busy;
        private bool scanning;

        internal MainWindow()
        {
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("MainWindow.xaml"))
                window = (Window)XamlReader.Load(resource);
            receivers = Find<ComboBox>("Receivers");
            controllers = Find<ComboBox>("Controllers");
            scan = Find<Button>("Scan");
            read = Find<Button>("Read");
            pair = Find<Button>("Pair");
            receiverInfo = Find<TextBlock>("ReceiverInfo");
            controllerInfo = Find<TextBlock>("ControllerInfo");
            statusTitle = Find<TextBlock>("StatusTitle");
            statusText = Find<TextBlock>("StatusText");
            statusPanel = Find<Border>("StatusPanel");
            activity = Find<TextBox>("Activity");
            progress = Find<ProgressBar>("Progress");
            dataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");

            scan.Click += async (sender, args) => await ScanAsync();
            read.Click += async (sender, args) => await CommunicateAsync(false);
            pair.Click += async (sender, args) => await CommunicateAsync(true);
            receivers.SelectionChanged += (sender, args) => SelectionChanged();
            controllers.SelectionChanged += (sender, args) => SelectionChanged();
            Find<Button>("Copy").Click += (sender, args) =>
            {
                try { Clipboard.SetText(activity.Text); }
                catch (Exception error) { SetStatus("Could not copy activity", error.Message, true); }
            };
            Find<Button>("OpenLogs").Click += (sender, args) =>
            {
                try
                {
                    Directory.CreateDirectory(dataDirectory);
                    Process.Start(new ProcessStartInfo(dataDirectory) { UseShellExecute = true });
                }
                catch (Exception error) { SetStatus("Could not open the data folder", error.Message, true); }
            };
            window.Closing += (sender, args) =>
            {
                if (!busy) return;
                args.Cancel = true;
                SetStatus("Please wait for the operation to finish", "Closing is paused while USB communication is in progress.", false);
            };
            window.Loaded += async (sender, args) => await ScanAsync();
        }

        private T Find<T>(string name) where T : class { return (T)window.FindName(name); }

        internal void Show() { window.Show(); }

        private void SetStatus(string title, string message, bool error)
        {
            statusTitle.Text = title;
            statusText.Text = message;
            statusPanel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(error ? "#462630" : "#202B40"));
            statusTitle.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(error ? "#FFB7BC" : "#EDF2FC"));
        }

        private void Log(string message)
        {
            string line = DateTimeOffset.Now.ToString("O") + " " + message + Environment.NewLine;
            lock (logLock)
            {
                // Failure to write an activity log must not interrupt a USB transaction.
                // Backups use a separate fail-closed path below.
                try
                {
                    Directory.CreateDirectory(dataDirectory);
                    File.AppendAllText(Path.Combine(dataDirectory, "activity.log"), line, Encoding.UTF8);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                activity.AppendText(line);
                activity.ScrollToEnd();
            }));
        }

        private void SetBusy(bool value)
        {
            busy = value;
            scan.IsEnabled = !value;
            receivers.IsEnabled = controllers.IsEnabled = !value;
            progress.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            bool selected = receivers.SelectedItem != null && controllers.SelectedItem != null;
            read.IsEnabled = pair.IsEnabled = !busy && selected;
        }

        private void SelectionChanged()
        {
            receiverInfo.Text = receivers.SelectedItem == null ? "Not detected" : "Detected · ready to read";
            controllerInfo.Text = controllers.SelectedItem == null ? "Not detected" : "USB pairing mode detected";
            UpdateButtons();
            if (!busy && !scanning && receivers.SelectedItem != null && controllers.SelectedItem != null)
                SetStatus("Both devices detected", "Read their addresses or select Pair devices to back up, pair, and verify.", false);
        }

        private async Task ScanAsync()
        {
            if (busy) return;
            SetBusy(true);
            scanning = true;
            SetStatus("Looking for devices...", "Checking supported USB HID pairing interfaces.", false);
            try
            {
                var devices = await Task.Run(() => NativeHid.Enumerate());
                var previousReceiver = receivers.SelectedItem as HidDeviceInfo;
                var previousController = controllers.SelectedItem as HidDeviceInfo;
                receivers.Items.Clear();
                controllers.Items.Clear();
                foreach (var device in devices)
                {
                    Log(device.ToString() + Environment.NewLine + device.Path);
                    if (!device.IsPairingInterface) continue;
                    if (device.IsReceiver) receivers.Items.Add(device);
                    else controllers.Items.Add(device);
                }
                SelectDevice(receivers, previousReceiver);
                SelectDevice(controllers, previousController);
                SelectionChanged();
                if (receivers.SelectedItem != null && controllers.SelectedItem != null)
                    SetStatus("Both devices detected", "Read their addresses or select Pair devices to back up, pair, and verify.", false);
                else if (controllers.Items.Count == 0 && receivers.Items.Count == 0)
                    SetStatus("Waiting for your devices", "Connect the receiver and put the controller into USB pairing mode, then scan again.", false);
                else if (controllers.Items.Count == 0)
                    SetStatus("Waiting for the controller", "Follow the USB pairing-mode steps above, then scan again. Normal USB gaming mode will not appear here.", false);
                else if (receivers.Items.Count == 0)
                    SetStatus("Waiting for the receiver", "Plug the receiver directly into your PC, then scan again.", false);
                else
                    SetStatus("Choose the intended devices", "More than one device was found. Select the receiver and controller you want to pair.", false);
                Log("Scan complete. " + receivers.Items.Count + " receiver(s), " + controllers.Items.Count + " controller(s).");
            }
            catch (Exception error)
            {
                Log("Scan failed: " + error.Message);
                SetStatus("Could not scan devices", error.Message, true);
            }
            finally { scanning = false; SetBusy(false); }
        }

        private static void SelectDevice(ComboBox box, HidDeviceInfo previous)
        {
            if (previous != null)
                box.SelectedItem = box.Items.Cast<HidDeviceInfo>().FirstOrDefault(item => item.Path == previous.Path);
            if (box.SelectedItem == null && box.Items.Count == 1) box.SelectedIndex = 0;
        }

        private void ShowAddresses(PairingAddresses receiver, PairingAddresses controller)
        {
            receiverInfo.Text = "Address " + PairingAddresses.Display(receiver.Receiver) +
                "\nPartner " + PairingAddresses.Display(receiver.Controller);
            controllerInfo.Text = "Address " + PairingAddresses.Display(controller.Controller) +
                "\nPartner " + PairingAddresses.Display(controller.Receiver);
        }

        private string SaveBackup(HidDeviceInfo receiverDevice, HidDeviceInfo controllerDevice,
            PairingAddresses receiver, PairingAddresses controller)
        {
            Directory.CreateDirectory(dataDirectory);
            string path = Path.Combine(dataDirectory, "backup-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ") + ".txt");
            string text = "GameSir G7 Pro Pairing 0.2.0 — original addresses\r\n" +
                "Receiver path=" + receiverDevice.Path + "\r\nReceiver original=" + receiver +
                "\r\nReceiver wire=" + BitConverter.ToString(receiver.Controller.Concat(receiver.Receiver).ToArray()) +
                "\r\nController path=" + controllerDevice.Path + "\r\nController original=" + controller +
                "\r\nController wire=" + BitConverter.ToString(controller.Controller.Concat(controller.Receiver).ToArray()) + "\r\n";
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                file.Write(bytes, 0, bytes.Length);
                file.Flush(true); // The backup must reach disk before either device changes.
            }
            return path;
        }

        private async Task CommunicateAsync(bool write)
        {
            if (busy) return;
            var receiverDevice = receivers.SelectedItem as HidDeviceInfo;
            var controllerDevice = controllers.SelectedItem as HidDeviceInfo;
            if (receiverDevice == null || controllerDevice == null) return;
            SetBusy(true);
            SetStatus(write ? "Pairing your devices..." : "Reading device addresses...",
                "Keep both devices connected until this operation finishes.", false);
            try
            {
                if (!write)
                {
                    PairingAddresses receiver = null, controller = null;
                    await Task.Run(() =>
                    {
                        using (var receiverLink = new HidPairingDevice(receiverDevice, Log))
                        using (var controllerLink = new HidPairingDevice(controllerDevice, Log))
                        {
                            receiver = receiverLink.Read();
                            controller = controllerLink.Read();
                        }
                    });
                    ShowAddresses(receiver, controller);
                    bool paired = receiver.Matches(controller) && PairingAddresses.IsValid(receiver.Controller) && PairingAddresses.IsValid(receiver.Receiver);
                    SetStatus(paired ? "These devices share a pairing" : "Addresses read successfully",
                        paired ? "Both devices report the same controller and receiver addresses. Wireless operation requires a cable-free test."
                               : "Select Pair devices to save the current addresses and pair this controller with this receiver.", false);
                }
                else
                {
                    var result = await Task.Run(() =>
                    {
                        using (var receiverLink = new HidPairingDevice(receiverDevice, Log))
                        using (var controllerLink = new HidPairingDevice(controllerDevice, Log))
                            return PairingService.Pair(receiverLink, controllerLink,
                                (receiver, controller) => SaveBackup(receiverDevice, controllerDevice, receiver, controller), Log);
                    });
                    ShowAddresses(result.Addresses, result.Addresses);
                    Log(result.AlreadyPaired ? "Already paired. No address writes were needed." : "Both pairing writes and fresh readbacks verified.");
                    SetStatus(result.AlreadyPaired ? "Already paired" : "Pairing verified",
                        "Disconnect the controller's USB cable and switch to 2.4 GHz. Test wireless input, then turn it off and on to check reconnection.", false);
                    statusPanel.Background = new SolidColorBrush(Color.FromRgb(27, 59, 49));
                    statusTitle.Foreground = new SolidColorBrush(Color.FromRgb(117, 217, 188));
                }
            }
            catch (Exception error)
            {
                Log("ERROR: " + error.Message);
                SetStatus("Operation could not be completed", error.Message, true);
            }
            finally { SetBusy(false); }
        }

        [STAThread]
        private static void Main()
        {
            var application = new Application();
            try
            {
                var main = new MainWindow();
                application.MainWindow = main.window;
                main.Show();
                application.Run();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message, "GameSir G7 Pro Pairing", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

