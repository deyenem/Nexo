using System;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Xamarin.Essentials;

namespace Nexo
{
    public partial class WelcomePage : ContentPage
    {
        public WelcomePage()
        {
            InitializeComponent();
        }

        private async void OnGetStartedClicked(object sender, EventArgs e)
        {
            try
            {
                // Mark as launched
                await SecureStorage.SetAsync("has_launched", "true");

                // Navigate to main page
                Application.Current.MainPage = new NavigationPage(new MainPage());
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to continue: {ex.Message}", "OK");
            }
        }

        private async void OnSetupWiFiClicked(object sender, EventArgs e)
        {
            try
            {
                // Mark as launched
                await SecureStorage.SetAsync("has_launched", "true");

                // Navigate to WiFi settings first
                var mainPage = new MainPage();
                var navigationPage = new NavigationPage(mainPage);
                Application.Current.MainPage = navigationPage;

                // Then push WiFi settings page
                await navigationPage.PushAsync(new WiFiSettingsPage());
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to open WiFi settings: {ex.Message}", "OK");
            }
        }
    }
}