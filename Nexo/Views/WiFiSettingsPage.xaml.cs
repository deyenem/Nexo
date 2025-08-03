using System;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Nexo.Models;
using Nexo.Services;
using Xamarin.Essentials;

namespace Nexo
{
    public partial class WiFiSettingsPage : ContentPage
    {
        private IWiFiSharingService wifiService;

        public WiFiSettingsPage()
        {
            InitializeComponent();
            wifiService = DependencyService.Get<IWiFiSharingService>();
            LoadSettings();
        }

        private void LoadSettings()
        {
            var settings = wifiService.GetConnectionSettings();
            entryHotspotName.Text = settings.HotspotName;
            entryPassword.Text = settings.Password;
            entryPort.Text = settings.ServerPort.ToString();
            entryDeviceName.Text = settings.DeviceName ?? DeviceInfo.Name;

            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (wifiService.IsAccessPointActive)
            {
                lblCurrentMode.Text = "📡 Access Point Active";
                lblCurrentMode.TextColor = Color.Green;
            }
            else if (wifiService.IsServerRunning)
            {
                lblCurrentMode.Text = "🔗 Connected to Device";
                lblCurrentMode.TextColor = Color.Blue;
            }
            else
            {
                lblCurrentMode.Text = "❌ Disconnected";
                lblCurrentMode.TextColor = Color.Gray;
            }

            lblDeviceName.Text = entryDeviceName.Text ?? "Unknown";
        }

        private async void OnCreateHotspotClicked(object sender, EventArgs e)
        {
            try
            {
                lblStatus.Text = "📡 Creating hotspot...";
                lblStatus.TextColor = Color.Blue;

                var settings = new ConnectionSettings
                {
                    HotspotName = entryHotspotName.Text?.Trim(),
                    Password = entryPassword.Text?.Trim(),
                    ServerPort = int.TryParse(entryPort.Text, out var port) ? port : 8080,
                    DeviceName = entryDeviceName.Text?.Trim()
                };

                wifiService.UpdateConnectionSettings(settings);

                var success = await wifiService.StartAccessPointAsync(settings.HotspotName, settings.Password);

                if (success)
                {
                    await wifiService.StartFileServerAsync(settings.ServerPort);
                    lblStatus.Text = "✅ Hotspot created successfully!";
                    lblStatus.TextColor = Color.Green;
                    UpdateStatus();
                }
                else
                {
                    lblStatus.Text = "❌ Failed to create hotspot";
                    lblStatus.TextColor = Color.Red;
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"❌ Error: {ex.Message}";
                lblStatus.TextColor = Color.Red;
                await DisplayAlert("Error", $"Failed to create hotspot: {ex.Message}", "OK");
            }
        }

        private async void OnScanDevicesClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new DeviceConnectionPage());
        }

        private async void OnStopSharingClicked(object sender, EventArgs e)
        {
            try
            {
                lblStatus.Text = "🛑 Stopping services...";
                lblStatus.TextColor = Color.Orange;

                await wifiService.StopFileServerAsync();
                await wifiService.StopAccessPointAsync();

                lblStatus.Text = "✅ All services stopped";
                lblStatus.TextColor = Color.Green;
                UpdateStatus();
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"❌ Error: {ex.Message}";
                lblStatus.TextColor = Color.Red;
                await DisplayAlert("Error", $"Failed to stop services: {ex.Message}", "OK");
            }
        }

        private async void OnSaveSettingsClicked(object sender, EventArgs e)
        {
            try
            {
                await SecureStorage.SetAsync("wifi_hotspot_name", entryHotspotName.Text?.Trim() ?? "");
                await SecureStorage.SetAsync("wifi_password", entryPassword.Text?.Trim() ?? "");
                await SecureStorage.SetAsync("wifi_port", entryPort.Text?.Trim() ?? "8080");
                await SecureStorage.SetAsync("device_name", entryDeviceName.Text?.Trim() ?? "");

                lblStatus.Text = "✅ Settings saved successfully!";
                lblStatus.TextColor = Color.Green;

                var settings = new ConnectionSettings
                {
                    HotspotName = entryHotspotName.Text?.Trim(),
                    Password = entryPassword.Text?.Trim(),
                    ServerPort = int.TryParse(entryPort.Text, out var port) ? port : 8080,
                    DeviceName = entryDeviceName.Text?.Trim()
                };

                wifiService.UpdateConnectionSettings(settings);
                UpdateStatus();
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"❌ Error saving: {ex.Message}";
                lblStatus.TextColor = Color.Red;
                await DisplayAlert("Error", $"Failed to save settings: {ex.Message}", "OK");
            }
        }
    }
}