using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Nexo.Models;
using Nexo.Services;
using Xamarin.Forms.PlatformConfiguration.WindowsSpecific;

namespace Nexo
{
    public partial class DeviceConnectionPage : ContentPage
    {
        private IWiFiSharingService wifiService;
        private List<WiFiDevice> availableDevices = new List<WiFiDevice>();

        public DeviceConnectionPage()
        {
            InitializeComponent();
            wifiService = DependencyService.Get<IWiFiSharingService>();

            refreshView.Command = new Command(async () => await ScanForDevices());
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await ScanForDevices();
        }

        private async Task ScanForDevices()
        {
            try
            {
                scanningIndicator.IsVisible = true;
                scanningIndicator.IsRunning = true;
                lblStatus.Text = "🔍 Scanning for devices...";

                availableDevices = await wifiService.ScanForDevicesAsync();
                devicesCollectionView.ItemsSource = availableDevices;

                if (availableDevices.Any())
                {
                    lblStatus.Text = $"✅ Found {availableDevices.Count} device(s)";
                }
                else
                {
                    lblStatus.Text = "❌ No devices found. Make sure other device has hotspot enabled.";
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"❌ Scan failed: {ex.Message}";
                await DisplayAlert("Scan Error", $"Failed to scan for devices: {ex.Message}", "OK");
            }
            finally
            {
                scanningIndicator.IsVisible = false;
                scanningIndicator.IsRunning = false;
                refreshView.IsRefreshing = false;
            }
        }

        private async void OnScanClicked(object sender, EventArgs e)
        {
            await ScanForDevices();
        }

        private async void OnRefreshClicked(object sender, EventArgs e)
        {
            await ScanForDevices();
        }

        private async void OnDeviceSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is WiFiDevice selectedDevice)
            {
                await ConnectToDevice(selectedDevice);
            }

            ((CollectionView)sender).SelectedItem = null;
        }

        private async Task ConnectToDevice(WiFiDevice device)
        {
            try
            {
                lblStatus.Text = $"🔗 Connecting to {device.Name}...";

                var success = await wifiService.ConnectToDeviceAsync(device.Name, "fileshare123");

                if (success)
                {
                    await wifiService.StartFileServerAsync(8080);
                    lblStatus.Text = $"✅ Connected to {device.Name}";
                    await DisplayAlert("Connected", $"Successfully connected to {device.Name}", "OK");
                    await Navigation.PopAsync();
                }
                else
                {
                    lblStatus.Text = $"❌ Failed to connect to {device.Name}";
                    await DisplayAlert("Connection Failed", $"Could not connect to {device.Name}. Please check the password and try again.", "OK");
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"❌ Connection error: {ex.Message}";
                await DisplayAlert("Error", $"Connection failed: {ex.Message}", "OK");
            }
        }
    }
}