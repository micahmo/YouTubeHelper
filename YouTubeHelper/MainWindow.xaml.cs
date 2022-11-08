using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ModernWpf.Controls;
using YouTubeHelper.Models;
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

            DataContext = MainControlViewModel;

            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            ApplicationSettings.Instance.Tracker.Track(this);
        }

        private void NavigationView_Loaded(object sender, RoutedEventArgs e)
        {
            MainControlViewModel.Load();
            NavigationView.SelectedItem = NavigationView.MenuItems.OfType<NavigationViewItem>().First();
            NavigationView.Content = MainControl;
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
            DatabaseEngine.Shutdown();
        }

        private static readonly MainControlViewModel MainControlViewModel = new();
        private static readonly MainControl MainControl = new() {DataContext = MainControlViewModel};

        private static readonly SettingsViewModel SettingsViewModel = new();
        private static readonly SettingsControl SettingsControl = new() { DataContext = SettingsViewModel };

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
                        bool res = DatabaseEngine.ExcludedVideosCollection.Upsert(new Video
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
                        bool res = DatabaseEngine.ExcludedVideosCollection.Upsert(new Video
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
                        bool res = DatabaseEngine.ExcludedVideosCollection.Upsert(new Video
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
