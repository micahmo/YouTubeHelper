using Android.Content;
using Android.OS;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using MongoDBHelpers;
using ServerStatusBot.Definitions.Api;
using ServerStatusBot.Definitions.Database.Models;
using ServerStatusBot.Definitions.Models;
using YouTubeHelper.Mobile.Notifications;
using YouTubeHelper.Mobile.ViewModels;
using YouTubeHelper.Mobile.Views;
using YouTubeHelper.Shared.Utilities;
using Environment = System.Environment;

namespace YouTubeHelper.Mobile
{
    public partial class AppShell : Shell
    {
        public static AppShell? Instance { get; private set; }
        private Tab? _currentTab;

        public AppShell()
        {
            InitializeComponent();
            BindingContext = new AppShellViewModel(this);
            Instance = this;
            _currentTab = WatchTab;
        }

        protected override bool OnBackButtonPressed()
        {
            if (_canClose)
            {
                Application.Current?.Quit();
            }
            
            bool anyPlayerOpen = false;

            // First, close any open players
            AppShellViewModel.ChannelViewModels.ForEach(c =>
            {
                anyPlayerOpen |= c.ShowPlayer;

                c.ShowPlayer = false;
                c.CurrentVideoUrl = null;
            });

            if (anyPlayerOpen)
            {
                return true;
            }

            // Next, re-select the main/watch tab
            if (!AppShellViewModel.WatchTabSelected)
            {
                AppShellViewModel.SelectWatchTab();
                return true;
            }

            // Or, select the first channel
            if (TabBar.CurrentItem.CurrentItem != TabBar.CurrentItem.Items.FirstOrDefault())
            {
                TabBar.CurrentItem.CurrentItem = null;
                TabBar.CurrentItem.CurrentItem = TabBar.CurrentItem.Items.FirstOrDefault();
                return true;
            }

            // Finally, prompt the user to close
            Snackbar.Make(
                Mobile.Resources.Resources.PressBackAgainToClose,
                duration: TimeSpan.FromSeconds(3),
                action: null,
                visualOptions: new SnackbarOptions
                {
                    BackgroundColor = Colors.Black,
                    TextColor = Colors.White
                }
            ).Show();

            _canClose = true;

            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                _canClose = false;
            });

            return true;
        }

        private bool _canClose;
        private bool _loaded;

        private async void Shell_Loaded(object sender, EventArgs e)
        {
            if (_loaded)
            {
                return;
            }

            BusyIndicator busyIndicator = new BusyIndicator(this, Mobile.Resources.Resources.ConnectingToDatabase);

            bool connectedToDatabase = await ConnectToDatabase(withPrompt: false);

            // If we didn't connect successfully, we need to hide the busy indicator, then connect again with a prompt and a separate busy indicator.
            if (!connectedToDatabase)
            {
                busyIndicator.Dispose();
                await ConnectToDatabase(withPrompt: true);
                busyIndicator = new BusyIndicator(this, Mobile.Resources.Resources.LoadingChannels);
            }

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

            busyIndicator.Text = Mobile.Resources.Resources.LoadingChannels;

            WatchTab.Items.Clear();
            SearchTab.Items.Clear();
            ExclusionsTab.Items.Clear();
            QueueTab.Items.Clear();

            int selectedSortModeIndex = Preferences.Default.Get(nameof(ChannelViewModel.SelectedSortModeIndex), 4);
            int selectedExclusionFilterIndex = Preferences.Default.Get(nameof(ChannelViewModel.SelectedExclusionFilterIndex), 0);

            List<Channel> channels = await ServerApiClient.Instance.GetChannels();
            foreach (Channel channel in channels.Reverse<Channel>())
            {
                channel.Changed += (_, _) => ServerApiClient.Instance.UpdateChannel(channel);

                ChannelViewModel channelViewModel = new(this)
                {
                    Channel = channel,
                    SelectedSortModeIndex = selectedSortModeIndex,
                    SelectedExclusionFilterIndex = selectedExclusionFilterIndex
                };
                AppShellViewModel.ChannelViewModels.Add(channelViewModel);

                var channelView = new ChannelView { BindingContext = channelViewModel };

                WatchTab.Items.Insert(0, new ShellContent { Title = channel.VanityName, Content = channelView });
                SearchTab.Items.Insert(0, new ShellContent { Title = channel.VanityName, Content = channelView });
                ExclusionsTab.Items.Insert(0, new ShellContent { Title = channel.VanityName, Content = channelView });
            }

            // Add one placeholder to the queue tab
            QueueTab.Items.Insert(0, new ShellContent
            {
                Title = Mobile.Resources.Resources.Queue, Content = new ChannelView
                {
                    BindingContext = AppShellViewModel.QueueChannelViewModel = new ChannelViewModel(this)
                    {
                        Loading = false,
                        SelectedSortModeIndex = selectedSortModeIndex,
                        SelectedExclusionFilterIndex = selectedExclusionFilterIndex
                    }
                }
            });

            // Make sure we know the first channel is actually selected
            TabBar.CurrentItem.CurrentItem = TabBar.CurrentItem.Items.FirstOrDefault();

            AppShellViewModel.ChannelViewModels.ForEach(c =>
            {
                c.Loading = false;
            });

            // Don't do any local notifications while we experiment with FCM
#pragma warning disable CS0162
            if (false)
            {
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await AppShellViewModel.UpdateNotification();
                        });

                        await Task.Delay(TimeSpan.FromSeconds(1));
                    }
                });
            }
#pragma warning restore CS0162

            busyIndicator.Dispose();

            // Check for battery restrictions
            _ = CheckBatteryOptimizations();

            _loaded = true;
        }

        private async Task CheckBatteryOptimizations()
        {
            PermissionStatus status = await Permissions.CheckStatusAsync<Permissions.Battery>();

            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Battery>();
            }

            if (status == PermissionStatus.Granted)
            {
                // Check if battery optimization is enabled
                if (DeviceInfo.Platform == DevicePlatform.Android)
                {
                    bool isIgnoringBatteryOptimizations = CheckIfIgnoringBatteryOptimizations();
                    if (!isIgnoringBatteryOptimizations)
                    {
                        await PromptForBatteryOptimizationSettings();
                    }
                }
            }
            else
            {
                // Ignore if user did not grant
            }
        }

        private bool CheckIfIgnoringBatteryOptimizations()
        {
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                Context context = Android.App.Application.Context;
                PowerManager? powerManager = (PowerManager?)context.GetSystemService(Context.PowerService);
                string? packageName = context.PackageName;
                return powerManager?.IsIgnoringBatteryOptimizations(packageName) == true;
            }
            
            return true;
        }

        private async Task PromptForBatteryOptimizationSettings()
        {
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                bool res = await DisplayAlert(Mobile.Resources.Resources.BatteryOptimizations, 
                    Mobile.Resources.Resources.BatteryOptimizationsPrompt,
                    Mobile.Resources.Resources.Yes, 
                    Mobile.Resources.Resources.No);

                if (res)
                {
                    Intent intent = new Intent(Android.Provider.Settings.ActionIgnoreBatteryOptimizationSettings);
                    intent.SetFlags(ActivityFlags.NewTask);
                    Android.App.Application.Context.StartActivity(intent);
                }
            }
        }

        private async Task<bool> ConnectToDatabase(bool withPrompt = true)
        {
            bool connected = false;

            try
            {
                string? connectionString = await SecureStorage.Default.GetAsync("connection_string");
                DatabaseEngine.ConnectionString = connectionString;
                if (string.IsNullOrEmpty(await DatabaseEngine.TestConnectionAsync()))
                {
                    connected = true;
                }
            }
            catch
            {
                // We'll fall into the next block which prompts the user to re-enter
            }

            if (withPrompt)
            {
                while (!connected)
                {
                    var res = await DisplayPromptAsync(Mobile.Resources.Resources.Database, Mobile.Resources.Resources.EnterConnectionString);

                    if (string.IsNullOrEmpty(res))
                    {
                        Environment.Exit(1);
                    }

                    DatabaseEngine.ConnectionString = res;

                    using (new BusyIndicator(this, Mobile.Resources.Resources.ConnectingToDatabase))
                    {
                        if (await DatabaseEngine.TestConnectionAsync() is { } err)
                        {
                            await DisplayAlert(Mobile.Resources.Resources.Error, string.Format(Mobile.Resources.Resources.ErrorConnectingToDatabase, err), Mobile.Resources.Resources.OK);
                        }
                        else
                        {
                            connected = true;
                        }
                    }
                }

                // We made it here, so we must have connected successfully. Save the connection string.
                await SecureStorage.Default.SetAsync("connection_string", DatabaseEngine.ConnectionString!);
            }

            if (connected)
            {
                await FirebaseService.InitializeAsync();
            }

            return connected;
        }

        private async void HandleQueueUpdates(RequestData requestData)
        {
            // If we're in queue mode, we go one step further and move the video to the top of the queue or add it if it's not already there
            if (AppShellViewModel is { QueueTabSelected: true, QueueChannelViewModel: { } queueChannel })
            {
                int indexOfCurrentVideo = queueChannel.Videos.ToList().FindIndex(videoViewModel => videoViewModel.Video.Id == requestData.VideoId);

                if (indexOfCurrentVideo > 0)
                {
                    // It's already in the list, so just remove and re-add at the beginning
                    VideoViewModel targetVideoViewModel = queueChannel.Videos[indexOfCurrentVideo];

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        queueChannel.Videos.Remove(targetVideoViewModel);
                        queueChannel.Videos.Insert(0, targetVideoViewModel);

                        // Scroll the view to the top so we can see the newly inserted item
                        queueChannel.ScrollToTopRequested?.Invoke();
                    });
                }
                else if (indexOfCurrentVideo < 0)
                {
                    if ((await YouTubeApi.Instance.FindVideoDetails(new List<string> { requestData.VideoId }, null, null, SortMode.AgeDesc)).FirstOrDefault() is { } newVideo)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            queueChannel.Videos.Insert(0, new VideoViewModel(newVideo, this, queueChannel));
                        });
                    }
                }
            }

            // If any videos that are currently being displayed match the ID in the request data, then start listening for updates
            // This will include the queue
            foreach (VideoViewModel videoViewModel in AppShellViewModel.ChannelViewModels.SelectMany(c => c.Videos).Union(AppShellViewModel.QueueChannelViewModel?.Videos ?? Enumerable.Empty<VideoViewModel>()).ToList())
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
            // If any videos that are currently being displayed match the ID in the updated video, then update it in the UI
            // This will include the queue
            foreach (VideoViewModel videoViewModel in AppShellViewModel.ChannelViewModels.SelectMany(c => c.Videos).Union(AppShellViewModel.QueueChannelViewModel?.Videos ?? Enumerable.Empty<VideoViewModel>()).ToList())
            {
                if (videoViewModel.Video.Id == updatedVideo.Id)
                {
                    videoViewModel.Video.Excluded = updatedVideo.Excluded;
                    videoViewModel.Video.ExclusionReason = updatedVideo.ExclusionReason;
                }
            }
        }

        public AppShellViewModel AppShellViewModel => (AppShellViewModel)BindingContext;

        private void Shell_Navigated(object _, ShellNavigatedEventArgs __)
        {
            AppShellViewModel.RaisePropertyChanged(nameof(AppShellViewModel.WatchTabSelected));
            AppShellViewModel.RaisePropertyChanged(nameof(AppShellViewModel.SearchTabSelected));
            AppShellViewModel.RaisePropertyChanged(nameof(AppShellViewModel.ExclusionsTabSelected));
            AppShellViewModel.RaisePropertyChanged(nameof(AppShellViewModel.QueueTabSelected));
            AppShellViewModel.RaisePropertyChanged(nameof(AppShellViewModel.NotQueueTabSelected));

            // Clear video lists when switching bottom tabs (NOT top tabs)
            if (CurrentItem.CurrentItem != _currentTab)
            {
                _currentTab = CurrentItem.CurrentItem as Tab;
                AppShellViewModel.ChannelViewModels.ForEach(c => c.Videos.Clear());

                // There's a weird issue where changing bottom tabs focuses the last top tab.
                // Manually fix that here.
                // ONLY do this when changing bottom tabs, otherwise this causes issues in VS 17.6.2.
                CurrentItem.CurrentItem.CurrentItem = CurrentItem.CurrentItem.Items.FirstOrDefault();

                if (AppShellViewModel.QueueTabSelected)
                {
                    AppShellViewModel.QueueChannelViewModel!.FindVideos();
                }
            }
        }

        public async Task HandleSharedLink(string? videoId, string? channelHandle)
        {
            while (!_loaded)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            BusyIndicator busyIndicator = new BusyIndicator(this, Mobile.Resources.Resources.HandlingSharedLink);

            Video? video = default;
            string? channelId = default;
            string? channelPlaylist = default;

            if (!string.IsNullOrEmpty(videoId))
            {
                busyIndicator.Text = Mobile.Resources.Resources.FindingSharedVideo;
                
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
                ChannelViewModel? foundChannelViewModel = default;
                foreach (var content in WatchTab.Items)
                {
                    if ((content.Content as ChannelView)?.BindingContext as ChannelViewModel is { } channelViewModel
                        && channelViewModel.Channel?.ChannelPlaylist == channelPlaylist)
                    {
                        foundChannelViewModel = channelViewModel;

                        try
                        {
                            WatchTab.CurrentItem = content;
                        }
                        catch
                        {
                            // This throws an exception, but it gets far enough.
                        }

                        break;
                    }
                }

                if (foundChannelViewModel is null)
                {
                    string channelName = await YouTubeApi.Instance.FindChannelName(channelId, Mobile.Resources.Resources.Unknown);

                    foundChannelViewModel = new(this)
                    {
                        Channel = new Channel(persistent: false)
                        {
                            VanityName = channelName,
                            ChannelPlaylist = channelPlaylist, 
                            ChannelId = channelId
                        },
                        SelectedSortModeIndex = Preferences.Default.Get(nameof(ChannelViewModel.SelectedSortModeIndex), 0),
                        SelectedExclusionFilterIndex = Preferences.Default.Get(nameof(ChannelViewModel.SelectedExclusionFilterIndex), 0)
                    };
                    AppShellViewModel.ChannelViewModels.Add(foundChannelViewModel);

                    var channelView = new ChannelView { BindingContext = foundChannelViewModel };

                    WatchTab.Items.Insert(0, new ShellContent { Title = channelName, Content = channelView });
                    SearchTab.Items.Insert(0, new ShellContent { Title = channelName, Content = channelView });
                    ExclusionsTab.Items.Insert(0, new ShellContent { Title = channelName, Content = channelView });

                    foundChannelViewModel.Loading = false;
                }

                foundChannelViewModel.Videos.Clear();

                if (video is not null)
                {
                    foundChannelViewModel.Videos.Add(new VideoViewModel(video, this, foundChannelViewModel) { IsDescriptionExpanded = true });
                }
            }

            busyIndicator.Dispose();
        }
    }
}