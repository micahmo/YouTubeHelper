using YouTubeHelper.Models;

namespace YouTubeHelper.ViewModels
{
    public class SettingsViewModel
    {
        public SettingsViewModel()
        {
            
        }

        public Settings Settings => Settings.Instance;
    }
}
