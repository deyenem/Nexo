using System;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Xamarin.Essentials;

namespace Nexo
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            CheckFirstLaunch();
        }

        private async void CheckFirstLaunch()
        {
            try
            {
                var hasLaunchedBefore = await SecureStorage.GetAsync("has_launched");

                if (string.IsNullOrEmpty(hasLaunchedBefore))
                {
                    // First launch - show welcome/setup
                    MainPage = new NavigationPage(new WelcomePage());
                }
                else
                {
                    // Normal launch
                    MainPage = new NavigationPage(new MainPage());
                }
            }
            catch
            {
                MainPage = new NavigationPage(new MainPage());
            }
        }
    }
}