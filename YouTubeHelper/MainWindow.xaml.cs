using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ModernWpf.Controls;
using YouTubeHelper.Models;
using YouTubeHelper.Shared;
using YouTubeHelper.Shared.Models;
using YouTubeHelper.Utilities;
using YouTubeHelper.ViewModels;
using YouTubeHelper.Views;

namespace YouTubeHelper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            ApplicationSettings.Instance.Load();
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            ApplicationSettings.Instance.Tracker.Track(this);
        }

        private async void NavigationView_Loaded(object sender, RoutedEventArgs e)
        {
            await ConnectToDatabase();

            MainControlViewModel = new();
            MainControl = new() { DataContext = MainControlViewModel };
            SettingsViewModel = new();
            SettingsControl = new() { DataContext = SettingsViewModel };

            DataContext = MainControlViewModel;

            MainControlViewModel.Load();
            NavigationView.SelectedItem = NavigationView.MenuItems.OfType<NavigationViewItem>().First();
            NavigationView.Content = MainControl;
        }

        private static async Task ConnectToDatabase()
        {
            // See if we already have a connection string encrypted
            bool connected = false;
            try
            {
                byte[] connectionStringUnencryptedBytes = ProtectedData.Unprotect(ApplicationSettings.Instance.ConnectionString, null, DataProtectionScope.CurrentUser);
                string connectionString = Encoding.UTF8.GetString(connectionStringUnencryptedBytes);
                DatabaseEngine.ConnectionString = connectionString;
                if (string.IsNullOrEmpty(DatabaseEngine.TestConnection()))
                {
                    connected = true;
                }
            }
            catch
            {
                // We'll fall into the next block which prompts the user to re-enter
            }

            while (!connected)
            {
                var result = await MessageBoxHelper.ShowInputBox(Properties.Resources.EnterConnectionString, Properties.Resources.Database);

                if (result.Result == ContentDialogResult.None)
                {
                    Environment.Exit(1);
                }

                DatabaseEngine.ConnectionString = result.Text;

                if (DatabaseEngine.TestConnection() is { } error)
                {
                    await MessageBoxHelper.Show(string.Format(Properties.Resources.ErrorConnectingToDatabase, error), Properties.Resources.Error, MessageBoxButton.OK);
                }
                else
                {
                    connected = true;
                }
            }

            // We made it here, so we must have connected successfully. Save the connection string.
            byte[] connectionStringBytes = Encoding.UTF8.GetBytes(DatabaseEngine.ConnectionString);
            var connectionStringEncryptedBytes = ProtectedData.Protect(connectionStringBytes, null, DataProtectionScope.CurrentUser);
            ApplicationSettings.Instance.ConnectionString = connectionStringEncryptedBytes;
        }

        private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItem?.ToString() == Properties.Resources.Watch)
            {
                MainControlViewModel.Mode = MainControlMode.Watch;
            }
            else if (args.InvokedItem?.ToString() == Properties.Resources.Search)
            {
                MainControlViewModel.Mode = MainControlMode.Search;
            }
            else if (args.InvokedItem?.ToString() == Properties.Resources.Exclusions)
            {
                MainControlViewModel.Mode = MainControlMode.Exclusions;
            }

            NavigationView.Content = args.IsSettingsInvoked ? SettingsControl : MainControl;
            NavigationView.Header = args.IsSettingsInvoked ? Properties.Resources.Settings : null;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Deselects some controls when the window is clicked
            if (Keyboard.FocusedElement is TextBox)
            {
                Keyboard.ClearFocus();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ApplicationSettings.Instance.SelectedTabIndex = MainControlViewModel.Channels.IndexOf(MainControlViewModel.SelectedChannel);
            ApplicationSettings.Instance.SelectedSortMode = MainControlViewModel.SelectedSortMode.Value;
        }

        private static MainControlViewModel MainControlViewModel;
        private static MainControl MainControl;

        private static SettingsViewModel SettingsViewModel;
        private static SettingsControl SettingsControl;

        private async void AddWatchedIds_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (!_dialogOpen)
            {
                _dialogOpen = true;
                
                string input = await MessageBoxHelper.ShowPastableText(string.Format(
                        Properties.Resources.AddWatchedIdsMessage,
                        MainControlViewModel.SelectedChannel.Channel.VanityName,
                        MainControlViewModel.SelectedChannel.Channel.ChannelPlaylist),
                    Properties.Resources.MarkAsWatched);

                if (!string.IsNullOrWhiteSpace(input))
                {
                    string[] videoIds = input.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    int updatedCount = 0;
                    foreach (string videoId in videoIds)
                    {
                        bool res = DatabaseEngine.ExcludedVideosCollection.Upsert<Video, string>(new Video
                        {
                            Id = videoId,
                            ExclusionReason = ExclusionReason.Watched,
                            ChannelPlaylist = MainControlViewModel.SelectedChannel.Channel.ChannelPlaylist
                        });

                        if (res)
                        {
                            ++updatedCount;
                        }
                    }

                    await MessageBoxHelper.Show(string.Format(Properties.Resources.MarkedAsWatched, updatedCount, videoIds.Length - updatedCount), 
                        Properties.Resources.Success, MessageBoxButton.OK);
                }

                _dialogOpen = false;
            }
        }

        private async void AddWontWatchIds_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (!_dialogOpen)
            {
                _dialogOpen = true;
                
                string input = await MessageBoxHelper.ShowPastableText(string.Format(
                        Properties.Resources.AddWontWatchIdsMessage,
                        MainControlViewModel.SelectedChannel.Channel.VanityName,
                        MainControlViewModel.SelectedChannel.Channel.ChannelPlaylist),
                    Properties.Resources.MarkAsWontWatch);

                if (!string.IsNullOrWhiteSpace(input))
                {
                    string[] videoIds = input.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    int updatedCount = 0;
                    foreach (string videoId in videoIds)
                    {
                        bool res = DatabaseEngine.ExcludedVideosCollection.Upsert<Video, string>(new Video
                        {
                            Id = videoId,
                            ExclusionReason = ExclusionReason.WontWatch,
                            ChannelPlaylist = MainControlViewModel.SelectedChannel.Channel.ChannelPlaylist
                        });

                        if (res)
                        {
                            ++updatedCount;
                        }
                    }

                    await MessageBoxHelper.Show(string.Format(Properties.Resources.MarkedAsWontWatch, updatedCount, videoIds.Length - updatedCount),
                        Properties.Resources.Success, MessageBoxButton.OK);
                }

                _dialogOpen = false;
            }
        }

        private async void AddMightWatchIds_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (!_dialogOpen)
            {
                _dialogOpen = true;
                
                string input = await MessageBoxHelper.ShowPastableText(string.Format(
                        Properties.Resources.AddMightWatchIdsMessage,
                        MainControlViewModel.SelectedChannel.Channel.VanityName,
                        MainControlViewModel.SelectedChannel.Channel.ChannelPlaylist),
                    Properties.Resources.MarkAsMightWatch);

                if (!string.IsNullOrWhiteSpace(input))
                {
                    string[] videoIds = input.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    int updatedCount = 0;
                    foreach (string videoId in videoIds)
                    {
                        bool res = DatabaseEngine.ExcludedVideosCollection.Upsert<Video, string>(new Video
                        {
                            Id = videoId,
                            ExclusionReason = ExclusionReason.MightWatch,
                            ChannelPlaylist = MainControlViewModel.SelectedChannel.Channel.ChannelPlaylist
                        });

                        if (res)
                        {
                            ++updatedCount;
                        }
                    }

                    await MessageBoxHelper.Show(string.Format(Properties.Resources.MarkedAsMightWatch, updatedCount, videoIds.Length - updatedCount),
                        Properties.Resources.Success, MessageBoxButton.OK);
                }

                _dialogOpen = false;
            }
        }

        private bool _dialogOpen;

        private void ChangeView_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.D1))
            {
                MainControlViewModel.IsMainControlExpanded = true;
                MainControlViewModel.IsPlayerExpanded = false;
            }
            else if (Keyboard.IsKeyDown(Key.D2))
            {
                MainControlViewModel.IsMainControlExpanded = true;
                MainControlViewModel.IsPlayerExpanded = true;
            }
            else if (Keyboard.IsKeyDown(Key.D3))
            {
                MainControlViewModel.IsMainControlExpanded = false;
                MainControlViewModel.IsPlayerExpanded = true;
            }
            else if (Keyboard.IsKeyDown(Key.Escape))
            {
                MainControlViewModel.RaisePropertyChanged(nameof(MainControlViewModel.SignalPauseVideo));

                if (!MainControlViewModel.IsMainControlExpanded)
                {
                    MainControlViewModel.IsMainControlExpanded = true;
                }
                else if (MainControlViewModel.IsPlayerExpanded)
                {
                    MainControlViewModel.IsPlayerExpanded = false;
                }
                else if (MainControlViewModel.Channels.Count > 0
                         && MainControlViewModel.Channels.IndexOf(MainControlViewModel.SelectedChannel) != 0)
                {
                    MainControlViewModel.SelectedChannel = MainControlViewModel.Channels.FirstOrDefault();
                }
                else
                {
                    Close();
                }
            }
            else if (Keyboard.IsKeyDown(Key.F5))
            {
                ExecuteMainCommand();
            }
            else if (Keyboard.IsKeyDown(Key.PageUp) && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (MainControlViewModel.SelectedChannel is not null && MainControlViewModel.Channels.IndexOf(MainControlViewModel.SelectedChannel) - 1 >= 0)
                {
                    MainControlViewModel.SelectedChannel = MainControlViewModel.Channels.ElementAt(MainControlViewModel.Channels.IndexOf(MainControlViewModel.SelectedChannel) - 1);

                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        ExecuteMainCommand();
                    }
                }
            }
            else if (Keyboard.IsKeyDown(Key.PageDown) && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (MainControlViewModel.SelectedChannel is not null && MainControlViewModel.Channels.IndexOf(MainControlViewModel.SelectedChannel) + 1 < MainControlViewModel.Channels.Count - 1)
                {
                    MainControlViewModel.SelectedChannel = MainControlViewModel.Channels.ElementAt(MainControlViewModel.Channels.IndexOf(MainControlViewModel.SelectedChannel) + 1);

                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        ExecuteMainCommand();
                    }
                }
            }
        }

        private static void ExecuteMainCommand()
        {
            if (MainControlViewModel.ExclusionsMode)
            {
                MainControlViewModel.SelectedChannel?.FindExclusionsCommand?.Execute(null);
            }
            else
            {
                MainControlViewModel.SelectedChannel?.FindVideosCommand?.Execute(null);
            }
        }
    }
}
