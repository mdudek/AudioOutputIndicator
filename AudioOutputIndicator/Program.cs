using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Media.Devices;

[assembly:AssemblyTitle("Audio Output Indicator")]
namespace AudioOutputIndicator
{
    static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.Run(new IndicatorApplicationContext());
        }


        public class IndicatorApplicationContext : ApplicationContext
        {
            private readonly NotifyIcon _notifyIcon;
            private string _defaultAudioId;

            const string ICON_PROP_NAME = "System.Devices.Icon";

            public IndicatorApplicationContext()
            {
                // Initialize
                _notifyIcon = new NotifyIcon();
                _notifyIcon.Visible = true;
                _notifyIcon.ContextMenuStrip = new ContextMenuStrip();
                _notifyIcon.ContextMenuStrip.Items.Add("Exit", null, Exit);

                MediaDevice.DefaultAudioRenderDeviceChanged += (sender, args) =>
                {
                    ShowDefaultDevice();
                };
                ShowDefaultDevice();
            }

            void Exit(object sender, EventArgs e)
            {
                // Hide tray icon, otherwise it will remain shown until user mouses over it
                _notifyIcon.Visible = false;
                Application.Exit();
            }

            private async void ShowDefaultDevice()
            {

                ////Paired bluetooth devices
                //var PairedBluetoothDevices = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true));
                ////Connected bluetooth devices
                //var ConnectedBluetoothDevices = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected));
                
                // audio devices
                var audioDevices = await DeviceInformation.FindAllAsync(MediaDevice.GetAudioRenderSelector());
                var defaultAudioId = MediaDevice.GetDefaultAudioRenderId(AudioDeviceRole.Default);
                if (_defaultAudioId != defaultAudioId)
                {
                    var defaultAudio = audioDevices.FirstOrDefault(a => a.Id == defaultAudioId);

                    if (defaultAudio != null)
                    {
                        var deviceIconString = defaultAudio.Properties[ICON_PROP_NAME].ToString();
                        var iconParts = deviceIconString.Split(",");
                        var icon = IconExtractor.Extract(Environment.ExpandEnvironmentVariables(iconParts[0]),
                            Int32.Parse(iconParts[1]), false);

                        _notifyIcon.Text = defaultAudio.Name;
                        _notifyIcon.Icon = icon;

                        if (_defaultAudioId != null)
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

        }
    }
}
