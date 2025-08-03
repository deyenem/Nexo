using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Forms;
using Nexo.Models;
using Nexo.Services;

namespace Nexo
{
    public partial class MainPage : ContentPage
    {
        private IFileService fileService;
        private IWiFiSharingService wifiService;
        private string currentPath = "";
        private List<string> pathHistory = new List<string>();

        public MainPage()
        {
            InitializeComponent();

            fileService = DependencyService.Get<IFileService>();
            wifiService = DependencyService.Get<IWiFiSharingService>();

            refreshView.Command = new Command(async () => await RefreshFiles());

            CheckInitialSetup();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            UpdateWiFiStatus();
            await CheckPermissionsAndLoad();
        }

        private async void CheckInitialSetup()
        {
            if (!await fileService.HasStoragePermissionAsync())
            {
                await DisplayAlert("Welcome!",
                    "This app needs storage permission to browse your files. Please grant permission on the next screen.",
                    "OK");
            }
        }

        private async Task CheckPermissionsAndLoad()
        {
            if (await fileService.HasStoragePermissionAsync())
            {
                await LoadFiles();
            }
            else
            {
                var granted = await fileService.RequestStoragePermissionAsync();
                if (granted)
                {
                    await LoadFiles();
                }
                else
                {
                    lblStatus.Text = "Storage permission required";
                    await DisplayAlert("Permission Required",
                        "Storage permission is required to browse files. Please enable it in settings.",
                        "OK");
                }
            }
        }

        private void UpdateWiFiStatus()
        {
            if (wifiService.IsAccessPointActive)
            {
                lblWiFiStatus.Text = "📡";
                lblWiFiStatus.TextColor = Color.LightGreen;
            }
            else if (wifiService.IsServerRunning)
            {
                lblWiFiStatus.Text = "🔗";
                lblWiFiStatus.TextColor = Color.LightBlue;
            }
            else
            {
                lblWiFiStatus.Text = "📶";
                lblWiFiStatus.TextColor = Color.White;
            }
        }

        private async Task LoadFiles()
        {
            try
            {
                lblStatus.Text = "Loading...";
                var files = await fileService.GetFilesAsync(currentPath);
                filesCollectionView.ItemsSource = files;
                lblCurrentPath.Text = string.IsNullOrEmpty(currentPath) ? "/" : currentPath;
                lblStatus.Text = $"{files.Count} items";

                btnBack.IsEnabled = pathHistory.Count > 0;
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Error loading files";
                await DisplayAlert("Error", $"Failed to load files: {ex.Message}", "OK");
            }
            finally
            {
                refreshView.IsRefreshing = false;
            }
        }

        private async Task RefreshFiles()
        {
            await LoadFiles();
        }

        private async void OnFileSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is FileItem selectedFile)
            {
                if (selectedFile.IsDirectory)
                {
                    pathHistory.Add(currentPath);
                    currentPath = selectedFile.Path;
                    await LoadFiles();
                }

                ((CollectionView)sender).SelectedItem = null;
            }
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            if (pathHistory.Count > 0)
            {
                currentPath = pathHistory.Last();
                pathHistory.RemoveAt(pathHistory.Count - 1);
                await LoadFiles();
            }
        }

        private async void OnRefreshClicked(object sender, EventArgs e)
        {
            await RefreshFiles();
        }

        private async void OnHomeClicked(object sender, EventArgs e)
        {
            pathHistory.Clear();
            currentPath = "";
            await LoadFiles();
        }

        private async void OnStorageClicked(object sender, EventArgs e)
        {
            try
            {
                var directories = await fileService.GetCommonDirectoriesAsync();
                var actions = directories.Select(d => d.Name).ToArray();

                var result = await DisplayActionSheet("Select Storage Location", "Cancel", null, actions);

                if (result != "Cancel" && !string.IsNullOrEmpty(result))
                {
                    var selectedDir = directories.FirstOrDefault(d => d.Name == result);
                    if (selectedDir != null)
                    {
                        pathHistory.Clear();
                        currentPath = selectedDir.Path;
                        await LoadFiles();
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load storage locations: {ex.Message}", "OK");
            }
        }

        private async void OnWiFiClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new WiFiSettingsPage());
        }

        private async void OnConnectClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new DeviceConnectionPage());
        }

        private async void OnShareFileClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is FileItem file)
            {
                if (wifiService.IsAccessPointActive || wifiService.IsServerRunning)
                {
                    await Navigation.PushAsync(new DeviceSelectionPage(file));
                }
                else
                {
                    var result = await DisplayAlert("WiFi Not Active",
                        "You need to create a hotspot or connect to a device first. Would you like to go to WiFi settings?",
                        "Yes", "No");

                    if (result)
                    {
                        await Navigation.PushAsync(new WiFiSettingsPage());
                    }
                }
            }
        }
    }
}