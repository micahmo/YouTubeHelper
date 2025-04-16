using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MongoDBHelpers;
using ServerStatusBot.Definitions.Api;
using ServerStatusBot.Definitions.Database.Models;
using YouTubeHelper.Utilities;

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
                Settings.Instance.PropertyChanged += Settings_PropertyChanged;
            }
        }

        private static async void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Exception? finalEx = null;

            if (e.PropertyName == nameof(Settings.Instance.ServerAddress))
            {
                for (int i = 0; i < 10; ++i)
                {
                    if (!string.IsNullOrEmpty(DatabaseEngine.ConnectionString))
                    {
                        try
                        {
                            await ServerApiClient.Instance.ReconnectAllGroups();
                            break;
                        }
                        catch (Exception ex)
                        {
                            finalEx = ex;
                        }

                        await Task.Delay(TimeSpan.FromSeconds(1));
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (finalEx != null)
            {
                Application.Current?.Dispatcher.Invoke(async () => await MessageBoxHelper.ShowCopyableText(Properties.Resources.UnexpectedError, Properties.Resources.Error, finalEx.ToString()));
            }
        }
    }
}
