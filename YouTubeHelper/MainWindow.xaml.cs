using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Flurl;
using ModernWpf.Controls;
using MongoDBHelpers;
using ServerStatusBot.Definitions.Api;
using ServerStatusBot.Definitions.Database.Models;
using ServerStatusBot.Definitions.Models;
using YouTubeHelper.Models;
using YouTubeHelper.Shared.Utilities;
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

            // Connect to queue updates over SignalR
            Task _ = Task.Run(async () =>
            {
                await ServerApiClient.Instance.SubscribeToHubEvents(
                    reconnecting: _ => Task.CompletedTask,
                    reconnected: async _ => { await ServerApiClient.Instance.ReconnectAllGroups(); },
                    closed: _ => Task.CompletedTask);

                await ServerApiClient.Instance.JoinQueueUpdatesGroup(HandleQueueUpdates);
                await ServerApiClient.Instance.JoinVideoObjectUpdatesGroup(HandleVideoObjectUpdates);
            });

            MainControlViewModel = new();
            MainControl = new() { DataContext = MainControlViewModel };
            SettingsViewModel = new();
            SettingsControl = new() { DataContext = SettingsViewModel };

            DataContext = MainControlViewModel;

            NavigationView.SelectedItem = NavigationView.MenuItems.OfType<NavigationViewItem>().First();
            NavigationView.Content = MainControl;

            // Let the UI render before loading
            await Dispatcher.Yield(DispatcherPriority.Background);

            await MainControlViewModel.Load();
        }

        private static async Task ConnectToDatabase()
        {
            // See if we already have a connection string encrypted
            bool connected = false;
            try
            {
                byte[] connectionStringUnencryptedBytes = ProtectedData.Unprotect(ApplicationSettings.Instance.ConnectionString!, null, DataProtectionScope.CurrentUser);
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
            byte[] connectionStringBytes = Encoding.UTF8.GetBytes(DatabaseEngine.ConnectionString!);
            var connectionStringEncryptedBytes = ProtectedData.Protect(connectionStringBytes, null, DataProtectionScope.CurrentUser);
            ApplicationSettings.Instance.ConnectionString = connectionStringEncryptedBytes;
        }

        private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            HandleNavigationItemChanged(args.InvokedItem?.ToString(), args.IsSettingsInvoked);
        }

        private void HandleNavigationItemChanged(string? name, bool isSettingsInvoked)
        {
            if (name == Properties.Resources.Watch)
            {
                MainControlViewModel!.Mode = MainControlMode.Watch;
            }
            else if (name == Properties.Resources.Search)
            {
                MainControlViewModel!.Mode = MainControlMode.Search;
            }
            else if (name == Properties.Resources.Exclusions)
            {
                MainControlViewModel!.Mode = MainControlMode.Exclusions;
            }
            else if (name == Properties.Resources.Queue)
            {
                MainControlViewModel!.Mode = MainControlMode.Queue;
                MainControlViewModel.SelectedChannel?.LoadQueueCommand?.Execute(null);
            }

            NavigationView.Content = isSettingsInvoked ? SettingsControl : MainControl;
            NavigationView.Header = isSettingsInvoked ? Properties.Resources.Settings : null;

            Dispatcher.BeginInvoke(() =>
            {
                Keyboard.Focus((NavigationView.Content as MainControl)?.Expander);
            });
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
            if (MainControlViewModel!.SelectedChannel is not null)
            {
                ApplicationSettings.Instance.SelectedTabIndex = MainControlViewModel.Channels.IndexOf(MainControlViewModel.SelectedChannel);
            }

            ApplicationSettings.Instance.SelectedSortMode = MainControlViewModel.SelectedSortMode.Value;
            ApplicationSettings.Instance.SelectedExclusionReason = MainControlViewModel.SelectedExclusionFilter.Value;
        }

        private static MainControlViewModel? MainControlViewModel;
        private static MainControl? MainControl;

        private static SettingsViewModel? SettingsViewModel;
        private static SettingsControl? SettingsControl;

        private async void AddWatchedIds_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (!_dialogOpen)
            {
                _dialogOpen = true;
                
                string? input = await MessageBoxHelper.ShowPastableText(string.Format(
                        Properties.Resources.AddWatchedIdsMessage,
                        MainControlViewModel!.SelectedChannel?.Channel.VanityName,
                        MainControlViewModel.SelectedChannel?.Channel.ChannelPlaylist),
                    Properties.Resources.MarkAsWatched);

                if (!string.IsNullOrWhiteSpace(input))
                {
                    string[] videoIds = input.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    int updatedCount = 0;
                    foreach (string videoId in videoIds)
                    {
                        bool res = await ServerApiClient.Instance.UpdateVideoWithUpdateResult(new Video
                        {
                            Id = videoId,
                            ExclusionReason = ExclusionReason.Watched,
                            ChannelPlaylist = MainControlViewModel.SelectedChannel?.Channel.ChannelPlaylist
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
                
                string? input = await MessageBoxHelper.ShowPastableText(string.Format(
                        Properties.Resources.AddWontWatchIdsMessage,
                        MainControlViewModel!.SelectedChannel?.Channel.VanityName,
                        MainControlViewModel.SelectedChannel?.Channel.ChannelPlaylist),
                    Properties.Resources.MarkAsWontWatch);

                if (!string.IsNullOrWhiteSpace(input))
                {
                    string[] videoIds = input.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    int updatedCount = 0;
                    foreach (string videoId in videoIds)
                    {
                        bool res = await ServerApiClient.Instance.UpdateVideoWithUpdateResult(new Video
                        {
                            Id = videoId,
                            ExclusionReason = ExclusionReason.WontWatch,
                            ChannelPlaylist = MainControlViewModel.SelectedChannel?.Channel.ChannelPlaylist
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
                
                string? input = await MessageBoxHelper.ShowPastableText(string.Format(
                        Properties.Resources.AddMightWatchIdsMessage,
                        MainControlViewModel!.SelectedChannel?.Channel.VanityName,
                        MainControlViewModel.SelectedChannel?.Channel.ChannelPlaylist),
                    Properties.Resources.MarkAsMightWatch);

                if (!string.IsNullOrWhiteSpace(input))
                {
                    string[] videoIds = input.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    int updatedCount = 0;
                    foreach (string videoId in videoIds)
                    {
                        bool res = await ServerApiClient.Instance.UpdateVideoWithUpdateResult(new Video
                        {
                            Id = videoId,
                            ExclusionReason = ExclusionReason.MightWatch,
                            ChannelPlaylist = MainControlViewModel.SelectedChannel?.Channel.ChannelPlaylist
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
                MainControlViewModel!.IsMainControlExpanded = true;
                MainControlViewModel.IsPlayerExpanded = false;
            }
            else if (Keyboard.IsKeyDown(Key.D2))
            {
                MainControlViewModel!.IsMainControlExpanded = true;
                MainControlViewModel.IsPlayerExpanded = true;
            }
            else if (Keyboard.IsKeyDown(Key.D3))
            {
                MainControlViewModel!.IsMainControlExpanded = false;
                MainControlViewModel.IsPlayerExpanded = true;
            }
            else if (Keyboard.IsKeyDown(Key.Escape))
            {
                MainControlViewModel!.RaisePropertyChanged(nameof(MainControlViewModel.SignalPauseVideo));

                if (!MainControlViewModel.IsMainControlExpanded)
                {
                    MainControlViewModel.IsMainControlExpanded = true;
                }
                else if (MainControlViewModel.IsPlayerExpanded)
                {
                    MainControlViewModel.IsPlayerExpanded = false;
                }
                else if (MainControlViewModel.Channels.Count > 0
                         && MainControlViewModel.Channels.IndexOf(MainControlViewModel.SelectedChannel!) != 0)
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
                if (MainControlViewModel!.SelectedChannel is not null && MainControlViewModel.Channels.IndexOf(MainControlViewModel.SelectedChannel) - 1 >= 0)
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
                if (MainControlViewModel!.SelectedChannel is not null && MainControlViewModel.Channels.IndexOf(MainControlViewModel.SelectedChannel) + 1 < MainControlViewModel.Channels.Count - 1)
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
            if (MainControlViewModel!.ExclusionsMode)
            {
                MainControlViewModel.SelectedChannel?.FindExclusionsCommand?.Execute(null);
            }
            else if (MainControlViewModel.QueueMode)
            {
                MainControlViewModel.SelectedChannel?.LoadQueueCommand?.Execute(null);
            }
            else
            {
                MainControlViewModel.SelectedChannel?.FindVideosCommand?.Execute(null);
            }
        }

        private async void HandlePaste_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            MainControlViewModel!.IsBusy = true;

            string rawUrl = Clipboard.GetText();
            string? videoId = default;
            string? channelHandle = default;
            string? channelId = default;
            string? channelPlaylist = default;
            Video? video = default;

            if (!string.IsNullOrEmpty(rawUrl))
            {
                Url url = new Url(rawUrl);

                if (url.QueryParams.FirstOrDefault(q => q.Name == "v").Value is string id)
                {
                    videoId = id;
                }
                else if (url.Authority == "youtu.be")
                {
                    videoId = url.PathSegments[0];
                }
                else if (url.Authority == "www.youtube.com" && url.PathSegments.Count >= 2 && url.PathSegments[0] == "live")
                {
                    videoId = url.PathSegments[1];
                }

                if (url.PathSegments.FirstOrDefault(p => p.StartsWith('@')) is { } cid)
                {
                    channelHandle = cid;
                }
            }

            if (!string.IsNullOrEmpty(videoId))
            {
                video = (await YouTubeApi.Instance.FindVideoDetails(new List<string> { videoId }, null, null, SortMode.AgeDesc)).FirstOrDefault();
                if (video is not null)
                {
                    // See if this video is excluded
                    if (await ServerApiClient.Instance.GetExcludedVideoById(video.Id) is { } excludedVideo)
                    {
                        video.Excluded = true;
                        video.ExclusionReason = excludedVideo.ExclusionReason;
                    }

                    channelPlaylist = video.ChannelPlaylist;
                    channelId = YouTubeApi.Instance.ToChannelId(channelPlaylist);
                }
            }

            if (!string.IsNullOrEmpty(channelHandle))
            {
                channelId = await YouTubeApi.Instance.FindChannelId(channelHandle);
                channelPlaylist = YouTubeApi.Instance.ToChannelPlaylist(channelId);
            }

            if (!string.IsNullOrEmpty(channelId) && !string.IsNullOrEmpty(channelPlaylist))
            {
                // Navigate to the watch tab if we're on the queue tab
                if ((NavigationViewItem)NavigationView.SelectedItem != WatchNavigationItem)
                {
                    NavigationView.SelectedItem = WatchNavigationItem;
                    HandleNavigationItemChanged(Properties.Resources.Watch, false);
                }

                ChannelViewModel? foundChannelViewModel = default;
                foreach (ChannelViewModel channelViewModel in MainControlViewModel.Channels)
                {
                    if (channelViewModel.Channel.ChannelPlaylist == channelPlaylist)
                    {
                        foundChannelViewModel = channelViewModel;
                        
                        MainControlViewModel.SelectedChannel = foundChannelViewModel;
                        break;
                    }
                }

                if (foundChannelViewModel is null)
                {
                    string channelName = await YouTubeApi.Instance.FindChannelName(channelId, Properties.Resources.Unknown);

                    foundChannelViewModel = new(new Channel(persistent: false)
                    {
                        VanityName = channelName,
                        ChannelPlaylist = channelPlaylist,
                        ChannelId = channelId
                    }, MainControlViewModel);
                    MainControlViewModel.Channels.Insert(0, foundChannelViewModel);
                    MainControlViewModel.RealChannels.Insert(0, foundChannelViewModel);
                    MainControlViewModel.SelectedChannel = foundChannelViewModel;
                }

                foundChannelViewModel.Videos.Clear();

                if (video is not null)
                {
                    foundChannelViewModel.Videos.Add(new VideoViewModel(video, MainControlViewModel, foundChannelViewModel) { IsDescriptionExpanded = true });
                }
            }

            MainControlViewModel.IsBusy = false;
        }

        private async void HandleQueueUpdates(RequestData requestData)
        {
            if (MainControlViewModel == null) return;

            // If we're in queue mode, we go one step further and move the video to the top of the queue or add it if it's not already there
            if (MainControlViewModel.QueueMode)
            {
                if (MainControlViewModel.Channels.FirstOrDefault() is { } queueChannel)
                {
                    int indexOfCurrentVideo = queueChannel.Videos.ToList().FindIndex(videoViewModel => videoViewModel.Video.Id == requestData.VideoId);

                    if (indexOfCurrentVideo > 0)
                    {
                        // It's already in the list, so just remove and re-add at the beginning
                        VideoViewModel targetVideoViewModel = queueChannel.Videos[indexOfCurrentVideo];

                        await Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            queueChannel.Videos.Remove(targetVideoViewModel);
                            queueChannel.Videos.Insert(0, targetVideoViewModel);
                        });
                    }
                    else if (indexOfCurrentVideo < 0)
                    {
                        if ((await YouTubeApi.Instance.FindVideoDetails(new List<string> { requestData.VideoId }, null, null, SortMode.AgeDesc)).FirstOrDefault() is { } newVideo)
                        {
                            await Application.Current.Dispatcher.BeginInvoke(() =>
                            {
                                queueChannel.Videos.Insert(0, new VideoViewModel(newVideo, MainControlViewModel, queueChannel));
                            });
                        }
                    }
                }
            }

            // If any videos that are currently being displayed match the ID in the request data, then start listening for updates
            // This will include the queue
            foreach (VideoViewModel videoViewModel in MainControlViewModel.Channels.SelectMany(c => c.Videos).ToList())
            {
                if (videoViewModel.Video.Id == requestData.VideoId)
                {
                    videoViewModel.Video.Excluded = false;
                    videoViewModel.Video.ExclusionReason = ExclusionReason.None;

                    await ServerApiClient.Instance.JoinDownloadGroup(
                        requestData.RequestGuid.ToString(),
                        requestDataUpdate => videoViewModel.UpdateCheck(requestData.RequestGuid.ToString(), requestDataUpdate, showInAppNotifications: false));
                }
            }
        }

        private void HandleVideoObjectUpdates(Video updatedVideo)
        {
            if (MainControlViewModel == null) return;

            // If any videos that are currently being displayed match the ID in the updated video, then update it in the UI
            // This will include the queue
            foreach (VideoViewModel videoViewModel in MainControlViewModel.Channels.SelectMany(c => c.Videos).ToList())
            {
                if (videoViewModel.Video.Id == updatedVideo.Id)
                {
                    videoViewModel.Video.Excluded = updatedVideo.Excluded;
                    videoViewModel.Video.ExclusionReason = updatedVideo.ExclusionReason;
                }
            }
        }
    }
}
