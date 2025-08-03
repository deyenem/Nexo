using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Nexo.Models;
using Nexo.Services;

namespace Nexo
{
    public partial class DeviceSelectionPage : ContentPage
    {
        private IWiFiSharingService wifiService;
        private FileItem fileToSend;
        private List<WiFiDevice> connectedDevices = new List<WiFiDevice>();

        public DeviceSelectionPage(FileItem file)
        {
            InitializeComponent();
            fileToSend = file;
            wifiService = DependencyService.Get<IWiFiSharingService>();

            lblFileName.Text = $"📄 {file.Name} ({file.SizeFormatted})";
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadConnectedDevices();
        }

        private async Task LoadConnectedDevices()
        {
            try
            {
                lblStatus.Text = "🔍 Scanning for connected devices...";

                // Get list of devices connected to our hotspot
                connectedDevices = await wifiService.ScanForDevicesAsync();

                // Filter to only show connected devices
                var activeDevices = new List<WiFiDevice>();
                foreach (var device in connectedDevices)
                {
                    if (device.IsConnected)
                    {
                        activeDevices.Add(device);
                    }
                }

                devicesCollectionView.ItemsSource = activeDevices;

                if (activeDevices.Count > 0)
                {
                    lblStatus.Text = $"✅ Found {activeDevices.Count} connected device(s)";
                }
                else
                {
                    lblStatus.Text = "❌ No devices connected to your hotspot";
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"❌ Error: {ex.Message}";
                await DisplayAlert("Error", $"Failed to scan for devices: {ex.Message}", "OK");
            }
        }

        private async void OnDeviceSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is WiFiDevice selectedDevice)
            {
                await SendFileToDevice(selectedDevice);
            }

            ((CollectionView)sender).SelectedItem = null;
        }

        private async void OnSendToDeviceClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is WiFiDevice device)
            {
                await SendFileToDevice(device);
            }
        }

        private async void OnRefreshClicked(object sender, EventArgs e)
        {
            await LoadConnectedDevices();
        }

        private async Task SendFileToDevice(WiFiDevice device)
        {
            try
            {
                lblStatus.Text = $"📤 Sending {fileToSend.Name} to {device.Name}...";

                // Show progress dialog
                var progressDialog = DisplayAlert("Sending File",
                    $"Sending '{fileToSend.Name}' to {device.Name}...\nPlease wait...",
                    "Cancel");

                var success = await wifiService.SendFileAsync(fileToSend.Path, device.IpAddress, device.Port);

                // If user didn't cancel the dialog, show result
                if (!progressDialog.IsCompleted)
                {
                    // Dismiss progress dialog by completing it
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        // The dialog will be automatically dismissed when we show the next one
                    });
                }

                if (success)
                {
                    lblStatus.Text = $"✅ File sent successfully to {device.Name}";
                    await DisplayAlert("Success! 🎉",
                        $"File '{fileToSend.Name}' sent successfully to {device.Name}!", "OK");

                    await Navigation.PopAsync(); // Go back to file list
                }
                else
                {
                    lblStatus.Text = $"❌ Failed to send file to {device.Name}";
                    await DisplayAlert("Send Failed",
                        $"Failed to send file to {device.Name}. Please check the connection and try again.", "OK");
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"❌ Error: {ex.Message}";
                await DisplayAlert("Error", $"Error sending file: {ex.Message}", "OK");
            }
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        private async void OnRetryClicked(object sender, EventArgs e)
        {
            await LoadConnectedDevices();
        }

        // Optional: Add method to handle file send progress
        private void UpdateSendProgress(int progress)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                lblStatus.Text = $"📤 Sending... {progress}%";
            });
        }

        // Optional: Add method to validate connection before sending
        private async Task<bool> ValidateConnectionAsync(WiFiDevice device)
        {
            try
            {
                // Test if device is still reachable
                var devices = await wifiService.ScanForDevicesAsync();
                return devices.Any(d => d.IpAddress == device.IpAddress && d.IsConnected);
            }
            catch
            {
                return false;
            }
        }

        // Optional: Add method to show detailed device info
        private async void OnDeviceInfoClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is WiFiDevice device)
            {
                await DisplayAlert("Device Information",
                    $"Name: {device.Name}\n" +
                    $"IP Address: {device.IpAddress}\n" +
                    $"Port: {device.Port}\n" +
                    $"Status: {(device.IsConnected ? "Connected" : "Disconnected")}\n" +
                    $"Role: {device.Role}",
                    "OK");
            }
        }
    }
}