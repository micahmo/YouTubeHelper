using System.ComponentModel;
using System.Windows.Controls;
using ServerStatusBot.Definitions.Api;
using ServerStatusBot.Definitions.Database.Models;

namespace YouTubeHelper.Views
{
    /// <summary>
    /// Interaction logic for SettingsControl.xaml
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        private static bool _registeredForSettingsChanges;
        
        public SettingsControl()
        {
            InitializeComponent();

            if (!_registeredForSettingsChanges)
            {
                _registeredForSettingsChanges = true;
                Settings.Instance!.PropertyChanged += Settings_PropertyChanged;
            }
        }

        private static async void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            await ServerApiClient.Instance.UpdateSettings(Settings.Instance!);
        }
    }
}
