using ServerStatusBot.Definitions.Database.Models;

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
