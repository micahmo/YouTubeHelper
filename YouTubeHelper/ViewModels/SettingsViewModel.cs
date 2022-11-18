using YouTubeHelper.Models;
using YouTubeHelper.Shared.Models;

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
