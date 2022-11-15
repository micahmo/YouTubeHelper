using Microsoft.Maui.Controls;

namespace YouTubeHelper.Mobile
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            MainPage = new AppShell();
        }
    }
}