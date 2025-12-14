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

        protected override void OnResume()
        {
            base.OnResume();

#if !DEBUG
            _ = Task.Run(async () =>
            {
                UpdateChecker updateChecker = new UpdateChecker();
                await updateChecker.CheckForUpdatesAsync();
            });
#endif
        }
    }
}