using YouTubeHelper.Mobile.ViewModels;
using YouTubeHelper.Mobile.Views;
using YouTubeHelper.Shared;
using YouTubeHelper.Shared.Models;
using YouTubeHelper.Shared.Utilities;

namespace YouTubeHelper.Mobile
{
    public partial class AppShell : Shell
    {
        public static AppShell Instance { get; private set; }
        private Tab _currentTab;

        public AppShell()
        {
            InitializeComponent();
            BindingContext = new AppShellViewModel(this);
            Instance = this;
            _currentTab = WatchTab;
        }

        protected override bool OnBackButtonPressed()
        {
            bool anyPlayerOpen = false;

            AppShellViewModel.ChannelViewModels.ForEach(c =>
            {
                anyPlayerOpen |= c.ShowPlayer;

                c.ShowPlayer = false;
                c.CurrentVideoUrl = null;
            });

            if (!anyPlayerOpen)
            {
                TabBar.CurrentItem.CurrentItem = null;
                TabBar.CurrentItem.CurrentItem = TabBar.CurrentItem.Items.FirstOrDefault();
            }

            return true;
        }

        private bool _loaded;

        private async void Shell_Loaded(object sender, EventArgs e)
        {
            if (_loaded)
            {
                return;
            }

            await ConnectToDatabase();

            WatchTab.Items.Clear();
            SearchTab.Items.Clear();
            ExclusionsTab.Items.Clear();
            QueueTab.Items.Clear();

            int selectedSortModeIndex = Preferences.Default.Get(nameof(ChannelViewModel.SelectedSortModeIndex), 0);
            int selectedExclusionFilterIndex = Preferences.Default.Get(nameof(ChannelViewModel.SelectedExclusionFilterIndex), 0);

            // I specifically want this call to be synchronous to freeze the UI on "Loading...".
            // There is nothing else happening at this time, so there is no benefit to asynchronous.
            var channels = DatabaseEngine.ChannelCollection.FindAll().OrderByDescending(c => c.Index);
            foreach (Channel channel in channels)
            {
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
                        ShowExcludedVideos = true
                    }
                }
            });

            AppShellViewModel.ChannelViewModels.ForEach(c =>
            {
                c.Loading = false;
            });

            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await AppShellViewModel.UpdateNotification();
                    });

                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
            });

            _loaded = true;
        }

        private async Task ConnectToDatabase()
        {
            bool connected = false;

            try
            {
                string connectionString = await SecureStorage.Default.GetAsync("connection_string");
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
                var res = await DisplayPromptAsync(Mobile.Resources.Resources.Database, Mobile.Resources.Resources.EnterConnectionString);

                if (string.IsNullOrEmpty(res))
                {
                    Environment.Exit(1);
                }

                DatabaseEngine.ConnectionString = res;

                using (new BusyIndicator(this))
                {
                    if (DatabaseEngine.TestConnection() is { } err)
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
            await SecureStorage.Default.SetAsync("connection_string", DatabaseEngine.ConnectionString);
        }

        public AppShellViewModel AppShellViewModel => BindingContext as AppShellViewModel;

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
                    AppShellViewModel.QueueChannelViewModel.FindVideos();
                }
            }
        }

        public async Task HandleSharedLink(string videoId, string channelHandle)
        {
            if (_loaded)
            {
                //Toast.Make("Please close the app before sharing.", ToastDuration.Long);
                await DisplayAlert(string.Empty, Mobile.Resources.Resources.CloseAppBeforeShare, Mobile.Resources.Resources.OK);
                return;
            }
            
            while (!_loaded)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }


            Video video = default;
            string channelId = default;
            string channelPlaylist = default;

            if (!string.IsNullOrEmpty(videoId))
            {
                video = (await YouTubeApi.Instance.FindVideoDetails(new List<string> { videoId }, null, null, SortMode.AgeDesc)).FirstOrDefault();
                if (video is not null)
                {
                    // See if this video is excluded
                    if (await DatabaseEngine.ExcludedVideosCollection.FindByIdAsync(video.Id) is { } excludedVideo)
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
                ChannelViewModel foundChannelViewModel = default;
                foreach (var content in WatchTab.Items)
                {
                    if ((content.Content as ChannelView)?.BindingContext as ChannelViewModel is { } channelViewModel
                        && channelViewModel.Channel.ChannelPlaylist == channelPlaylist)
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
                        Channel = new Channel(nonPersistent: true)
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
                    QueueTab.Items.Insert(0, new ShellContent { Title = channelName, Content = channelView });

                    foundChannelViewModel.Loading = false;
                }

                foundChannelViewModel.Videos.Clear();

                if (video is not null)
                {
                    foundChannelViewModel.Videos.Add(new VideoViewModel(video, this, foundChannelViewModel) { IsDescriptionExpanded = true });
                }
            }
        }
    }
}