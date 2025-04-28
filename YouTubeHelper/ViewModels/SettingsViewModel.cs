using ServerStatusBot.Definitions.Database.Models;
using System.Windows.Input;
using Microsoft.Toolkit.Mvvm.Input;
using YouTubeHelper.Models;
using ServerStatusBot.Definitions.Api;
using System.Threading.Tasks;
using System;
using System.Windows;
using YouTubeHelper.Utilities;

namespace YouTubeHelper.ViewModels
{
    public class SettingsViewModel
    {
        public SettingsViewModel()
        {
            
        }

        // For binding
        public Settings Settings => Settings.Instance!;

        public ICommand ChangeServerAddressCommand => _changeServerAddressCommand ??= new RelayCommand(async () =>
        {
            ApplicationSettings.Instance.ServerAddress = null;
            await MainWindow.ConnectToServer();

            // After reconnecting, re-hook up SignalR
            Exception? finalEx = null;

            for (int i = 0; i < 10; ++i)
            {
                if (!string.IsNullOrEmpty(ServerApiClient.BaseUrl))
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

            if (finalEx != null)
            {
                Application.Current?.Dispatcher.Invoke(async () => await MessageBoxHelper.ShowCopyableText(Properties.Resources.UnexpectedError, Properties.Resources.Error, finalEx.ToString()));
            }
        });
        private ICommand? _changeServerAddressCommand;
    }
}
