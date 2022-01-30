using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Media.Devices;
using Microsoft.Win32;
using RegistryUtils;

[assembly: AssemblyTitle("Audio Output Indicator")]
namespace AudioOutputIndicator
{
    static class Program
    {
        private static IconSets _selectedIconSet;
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            //var culture = new CultureInfo("cs");
            //Thread.CurrentThread.CurrentCulture = culture;
            //Thread.CurrentThread.CurrentUICulture = culture;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.Run(new IndicatorApplicationContext());

        }


        public class IndicatorApplicationContext : ApplicationContext
        {
            const string OsThemeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            private RegistryMonitor _monitor;
            private readonly NotifyIcon _notifyIcon;
            private string _defaultAudioId;
            private Dictionary<IconSets, ToolStripMenuItem> _iconSetsMenu = new Dictionary<IconSets, ToolStripMenuItem>();

            const string ICON_PROP_NAME = "System.Devices.Icon";

            public IndicatorApplicationContext()
            {
                _monitor = new RegistryMonitor(RegistryHive.CurrentUser, OsThemeRegistryPath);
                _monitor.RegChanged += OnRegChanged;
                _monitor.Start();


                SystemEvents.PowerModeChanged += OnPowerChange;


                // Initialize
                _notifyIcon = new NotifyIcon();
                _notifyIcon.Visible = true;
                _notifyIcon.ContextMenuStrip = new ContextMenuStrip();
                var iconSetItem = _notifyIcon.ContextMenuStrip.Items.Add(Resources.Texts.IconSet, Resources.Icons.headphones.ToBitmap()) as ToolStripMenuItem;
                _iconSetsMenu.Add(IconSets.Auto, iconSetItem.DropDownItems.Add(Resources.Texts.Auto, Resources.Icons.auto.ToBitmap(), (sender, args) => SetIconSet(IconSets.Auto)) as ToolStripMenuItem);
                _iconSetsMenu.Add(IconSets.Dark, iconSetItem.DropDownItems.Add(Resources.Texts.Dark, Resources.Icons.headphones.ToBitmap(), (sender, args) => SetIconSet(IconSets.Dark)) as ToolStripMenuItem);
                _iconSetsMenu.Add(IconSets.Light, iconSetItem.DropDownItems.Add(Resources.Texts.Light, Resources.Icons.headphones_white.ToBitmap(), (sender, args) => SetIconSet(IconSets.Light)) as ToolStripMenuItem);
                _iconSetsMenu.Add(IconSets.Original, iconSetItem.DropDownItems.Add(Resources.Texts.Original, Resources.Icons.headphones_orig.ToBitmap(), (sender, args) => SetIconSet(IconSets.Original)) as ToolStripMenuItem);
                _notifyIcon.ContextMenuStrip.Items.Add(Resources.Texts.About, Resources.Icons.info.ToBitmap(), ShowAbout);
                _notifyIcon.ContextMenuStrip.Items.Add(Resources.Texts.Exit, Resources.Icons.exit.ToBitmap(), Exit);

                var initialIconSet = IconSets.Auto;
                // load user settings
                var savedIconSet = Properties.Settings.Default["IconSet"].ToString();
                if (!String.IsNullOrEmpty(savedIconSet))
                {
                    Enum.TryParse(savedIconSet, out initialIconSet);
                }
                SetIconSet(initialIconSet);

                MediaDevice.DefaultAudioRenderDeviceChanged += (sender, args) =>
                {
                    ShowDefaultDevice();
                };
                ShowDefaultDevice();
            }

            private void OnRegChanged(object? sender, EventArgs e)
            {
                if (_selectedIconSet == IconSets.Auto)
                {
                    ShowDefaultDevice(true);
                }
            }

            private async void OnPowerChange(object s, PowerModeChangedEventArgs e)
            {
                switch (e.Mode)
                {
                    case PowerModes.Resume:
                        await Task.Delay(3000);
                        ShowDefaultDevice(true);
                        break;
                }
            }

            private void SetIconSet(IconSets iconSet)
            {
                _selectedIconSet = iconSet;
                
                // save user settings
                Properties.Settings.Default["IconSet"] = iconSet.ToString();
                Properties.Settings.Default.Save();

                foreach (var icSet in _iconSetsMenu.Keys)
                {
                    _iconSetsMenu[icSet].Checked = icSet == iconSet;
                }

                ShowDefaultDevice(true);
            }

            void Exit(object sender, EventArgs e)
            {
                _monitor.Stop();
                // Hide tray icon, otherwise it will remain shown until user mouses over it
                _notifyIcon.Visible = false;
                Application.Exit();
            }

            void ShowAbout(object sender, EventArgs e)
            {
                MessageBox.Show($"Audio Output Indicator - {GetAppVersion()}", Resources.Texts.About, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            private async void ShowDefaultDevice(bool updateIconOnly = false)
            {

                ////Paired bluetooth devices
                //var PairedBluetoothDevices = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true));
                ////Connected bluetooth devices
                //var ConnectedBluetoothDevices = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected));

                // audio devices
                var audioDevices = await DeviceInformation.FindAllAsync(MediaDevice.GetAudioRenderSelector());
                var defaultAudioId = MediaDevice.GetDefaultAudioRenderId(AudioDeviceRole.Default);
                Debug.Print("defaultAudioId:" + defaultAudioId + '\n');
                if (_defaultAudioId != defaultAudioId || updateIconOnly)
                {
                    var defaultAudio = audioDevices.FirstOrDefault(a => a.Id == defaultAudioId);

                    if (defaultAudio != null)
                    {
                        var deviceIconString = defaultAudio.Properties[ICON_PROP_NAME].ToString();

                        _notifyIcon.Text = defaultAudio.Name;
                        _notifyIcon.Icon = getDeviceIcon(deviceIconString);

                        if (_defaultAudioId != null && !updateIconOnly)
                        {
                            _notifyIcon.BalloonTipTitle = @"Default audio device changed:";
                            _notifyIcon.BalloonTipText = defaultAudio.Name;
                            _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
                            _notifyIcon.ShowBalloonTip(2000);
                        }
                    }
                    else
                    {
                        _notifyIcon.Text = null;
                    }
                }
                _defaultAudioId = defaultAudioId;
            }

            private Icon getDeviceIcon(string deviceIconString)
            {

                Debug.WriteLine($"deviceIconString: {deviceIconString}");
                // %windir%\system32\mmres.dll,-3010
                // "C:\\Windows\\System32\\DDORes.dll,-2033"

                if (_selectedIconSet != IconSets.Original)
                {
                    var iconSetToUse = _selectedIconSet;
                    if (iconSetToUse == IconSets.Auto)
                    {
                        // set default value
                        iconSetToUse = IconSets.Original;
                        // use icons set by OS theme
                        using (var key = Registry.CurrentUser.OpenSubKey($"{OsThemeRegistryPath}"))
                        {
                            if (key != null) {
                                var value = key.GetValue("AppsUseLightTheme") as int?;
                                iconSetToUse = value == 1 ? IconSets.Dark : IconSets.Light;
                            }
                        }
                    }
                    if (deviceIconString.ToLower().IndexOf("mmres.dll", StringComparison.Ordinal) != -1)
                    {
                        var iconNo = deviceIconString.Substring(deviceIconString.IndexOf(",", StringComparison.Ordinal) + 1);
                        switch (iconNo)
                        {
                            case "-3010":
                                return iconSetToUse == IconSets.Light ? Resources.Icons.speakers_white : Resources.Icons.speakers;
                        }
                    }
                    if (deviceIconString.ToLower().IndexOf("ddores.dll", StringComparison.Ordinal) != -1)
                    {
                        var iconNo = deviceIconString.Substring(deviceIconString.IndexOf(",", StringComparison.Ordinal) + 1);
                        switch (iconNo)
                        {
                            case "-2033":
                                return iconSetToUse == IconSets.Light ? Resources.Icons.headphones_white : Resources.Icons.headphones;
                        }
                    }
                }

                // show original device icon
                var iconParts = deviceIconString.Split(",");
                var icon = IconExtractor.Extract(Environment.ExpandEnvironmentVariables(iconParts[0]),
                    int.Parse(iconParts[1]), false);
                return icon;

            }

            private string GetAppVersion()
            {
                return GetType().Assembly.GetName().Version.ToString();
            }

        }
    }
}
