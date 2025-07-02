using Bluegrams.Application;
using Bluegrams.Application.WPF;
using Flurl;
using ModernWpf.Controls;
using ServerStatusBot.Definitions;
using ServerStatusBot.Definitions.Api;
using ServerStatusBot.Definitions.Database.Models;
using ServerStatusBot.Definitions.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Resources;
using System.Windows.Threading;
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
        public static string ClientId { get; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Singleton instance
        /// </summary>
        public static MainWindow? Instance { get; private set; }

        private readonly WpfUpdateChecker _updateChecker;

        public MainWindow()
        {
            ApplicationSettings.Instance.Load();
            InitializeComponent();
            Instance = this;

            Title = $"{Properties.Resources.ApplicationName} {Versioning.GetInstalledMsiVersion()}";

            // Check for updates
            _updateChecker = new MyUpdateChecker("https://gist.githubusercontent.com/micahmo/2f8966f2a9acbc8d11d70d69dc75c34c/raw/YouTubeHelperVersionInfo.xml", this);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            ApplicationSettings.Instance.Tracker.Track(this);
        }

        private async void NavigationView_Loaded(object sender, RoutedEventArgs e)
        {
            await ConnectToServer();

            // Connect to queue updates over SignalR
            Task _ = Task.Run(async () =>
            {
                await ServerApiClient.Instance.SubscribeToHubEvents(
                    reconnecting: _ => Task.CompletedTask,
                    reconnected: async _ => { await ServerApiClient.Instance.ReconnectAllGroups(); },
                    closed: _ => Task.CompletedTask);

                await ServerApiClient.Instance.JoinQueueUpdatesGroup(HandleQueueUpdates);
                await ServerApiClient.Instance.JoinVideoObjectUpdatesGroup(HandleVideoObjectUpdates);
                await ServerApiClient.Instance.JoinChannelObjectUpdatesGroup(HandleChannelObjectUpdates);
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

        public static async Task ConnectToServer()
        {
            // See if we already have a server address encrypted
            bool connected = false;
            try
            {
                byte[] serverAddressUnencryptedBytes = ProtectedData.Unprotect(ApplicationSettings.Instance.ServerAddress!, null, DataProtectionScope.CurrentUser);
                string serverAddress = Encoding.UTF8.GetString(serverAddressUnencryptedBytes);
                ServerApiClient.SetBaseUrl(serverAddress);
                Settings.Instance = await ServerApiClient.Instance.GetSettings();
                connected = true;
            }
            catch
            {
                // We'll fall into the next block which prompts the user to re-enter
            }

            while (!connected)
            {
                var result = await MessageBoxHelper.ShowInputBox(Properties.Resources.EnterServerAddress, Properties.Resources.Server);

                if (result.Result == ContentDialogResult.None)
                {
                    Environment.Exit(1);
                }

                ServerApiClient.SetBaseUrl(result.Text);

                string error = string.Empty;
                try
                {
                    Settings.Instance = await ServerApiClient.Instance.GetSettings();
                }
                catch (Exception ex)
                {
                    error = ex.ToString();
                }

                if (!string.IsNullOrEmpty(error))
                {
                    await MessageBoxHelper.Show(string.Format(Properties.Resources.ErrorConnectingToServer, error), Properties.Resources.Error, MessageBoxButton.OK);
                }
                else
                {
                    connected = true;
                }
            }

            // We made it here, so we must have connected successfully. Save the server address.
            byte[] serverAddressBytes = Encoding.UTF8.GetBytes(ServerApiClient.BaseUrl!);
            byte[] serverAddressEncryptedBytes = ProtectedData.Protect(serverAddressBytes, null, DataProtectionScope.CurrentUser);
            ApplicationSettings.Instance.ServerAddress = serverAddressEncryptedBytes;
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
#if !DEBUG
            // Auto allows the user to Skip (updates are still available via F1)
            _updateChecker.CheckForUpdates(UpdateNotifyMode.Auto);
#endif
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
                        }, ClientId);

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
                        }, ClientId);

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
                        }, ClientId);

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
            if (MainControlViewModel?.IsBusy == true)
            {
                return;
            }

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
                else if ((NavigationViewItem)NavigationView.SelectedItem != WatchNavigationItem)
                {
                    NavigationView.SelectedItem = WatchNavigationItem;
                    HandleNavigationItemChanged(Properties.Resources.Watch, false);
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
            string rawUrl = Clipboard.GetText();
            
            await HandleSharedLink(rawUrl);
        }

        public async Task HandleSharedLink(string rawUrl)
        {
            MainControlViewModel!.IsBusy = true;

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
                video = (await ServerApiClient.Instance.FindVideos(new FindVideosRequest
                {
                    ShowExclusions = true,
                    VideoIds = new List<string> { videoId },
                    SortMode = SortMode.AgeDesc,
                    Count = int.MaxValue
                })).FirstOrDefault();

                if (video is not null)
                {
                    channelPlaylist = video.ChannelPlaylist;
                    channelId = YouTubeUtils.ToChannelId(channelPlaylist);
                }
            }

            if (!string.IsNullOrEmpty(channelHandle))
            {
                channelId = await ServerApiClient.Instance.FindChannelId(channelHandle);
                channelPlaylist = YouTubeUtils.ToChannelPlaylist(channelId);
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
                    string channelName = await ServerApiClient.Instance.FindChannelName(channelId, Properties.Resources.Unknown);

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
                    VideoViewModel videoViewModel = new VideoViewModel(video, MainControlViewModel, foundChannelViewModel) { IsDescriptionExpanded = true };
                    foundChannelViewModel.Videos.Add(videoViewModel);

                    await QueueUtils.TryJoinDownloadGroup(videoViewModel);
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
                        if ((await ServerApiClient.Instance.FindVideos(new FindVideosRequest
                            {
                                ShowExclusions = true,
                                VideoIds = new List<string> { requestData.VideoId },
                                SortMode = SortMode.AgeDesc,
                                Count = int.MaxValue
                            })).FirstOrDefault() is { } newVideo)
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

        private void HandleVideoObjectUpdates(ObjectChangedEventArgs<Video> updatedVideoArgs)
        {
            if (updatedVideoArgs.Originator == ClientId) return;
            
            if (MainControlViewModel == null) return;

            Video updatedVideo = updatedVideoArgs.Obj;

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

        private void HandleChannelObjectUpdates(ObjectChangedEventArgs<Channel> updatedChannelArgs)
        {
            if (updatedChannelArgs.Originator == ClientId) return;

            if (MainControlViewModel == null) return;

            Channel updatedChannel = updatedChannelArgs.Obj;

            // Look for the channel
            bool found = false;
            foreach (ChannelViewModel channelViewModel in MainControlViewModel.Channels.ToList())
            {
                if (channelViewModel.Channel.Id == updatedChannel.Id)
                {
                    // We have it, but it's marked for deletion. Remove it!
                    if (updatedChannel.MarkForDeletion)
                    {
                        if (MainControlViewModel.SelectedChannel == channelViewModel)
                        {
                            Application.Current.Dispatcher.BeginInvoke(() => MainControlViewModel.SelectedChannel = MainControlViewModel.Channels[Math.Max(0, MainControlViewModel.Channels.IndexOf(channelViewModel) - 1)]);
                        }
                        Application.Current.Dispatcher.BeginInvoke(() => MainControlViewModel.Channels.Remove(channelViewModel));
                        channelViewModel.Channel.Persistent = false;
                    }
                    else
                    {
                        // We have it, update the properties
                        channelViewModel.Channel.Persistent = false; // stop doing updates
                        channelViewModel.Channel.Identifier = updatedChannel.Identifier;
                        channelViewModel.Channel.ChannelPlaylist = updatedChannel.ChannelPlaylist;
                        channelViewModel.Channel.ChannelId = updatedChannel.ChannelId;
                        channelViewModel.Channel.VanityName = updatedChannel.VanityName;
                        channelViewModel.Channel.Description = updatedChannel.Description;
                        channelViewModel.Channel.DateRangeLimit = updatedChannel.DateRangeLimit;
                        channelViewModel.Channel.EnableDateRangeLimit = updatedChannel.EnableDateRangeLimit;
                        channelViewModel.Channel.VideoLengthMinimum = updatedChannel.VideoLengthMinimum;
                        channelViewModel.Channel.EnableVideoLengthMinimum = updatedChannel.EnableVideoLengthMinimum;
                        channelViewModel.Channel.Persistent = true; // resume updates
                    }

                    found = true;
                }
            }

            // We don't have it, and it's not marked for deletion, so it must be new. Add it!
            if (!found && !updatedChannel.MarkForDeletion)
            {
                ChannelViewModel channelViewModel = new(updatedChannel, MainControlViewModel);
                Application.Current.Dispatcher.BeginInvoke(() => MainControlViewModel.Channels.Insert(MainControlViewModel.Channels.Count - 1, channelViewModel));

                // Listen for changes
                updatedChannel.Changed += async (_, _) =>
                {
                    await ServerApiClient.Instance.UpdateChannel(updatedChannel, ClientId);
                };
            }
        }

        private void AboutBoxCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // Get the icon
            Uri iconUri = new Uri("pack://application:,,,/Images/logo.ico", UriKind.Absolute);
            StreamResourceInfo? info = Application.GetResourceStream(iconUri);

            BitmapSource? bitmapIcon = null;
            if (info != null)
            {
                using Icon icon = new Icon(info.Stream);
                bitmapIcon = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }

            new AboutBox(bitmapIcon, showLanguageSelection: false)
            {
                Owner = this,
                UpdateChecker = _updateChecker,
            }.ShowDialog();
        }
    }
}
