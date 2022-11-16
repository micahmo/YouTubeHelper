using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
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

        public AppShell()
        {
            InitializeComponent();
            BindingContext = new AppShellViewModel(this);
            Instance = this;
        }

        protected override bool OnBackButtonPressed()
        {
            AppShellViewModel.ChannelViewModels.ForEach(c =>
            {
                c.ShowPlayer = false;
                c.CurrentVideoUrl = null;
            });

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

            var channels = DatabaseEngine.ChannelCollection.FindAll().AsEnumerable().OrderByDescending(c => c.Index).ToList();
            foreach (Channel channel in channels)
            {
                ChannelViewModel channelViewModel = new(this)
                {
                    Channel = channel, 
                    SelectedSortModeIndex = Preferences.Default.Get(nameof(ChannelViewModel.SelectedSortModeIndex), 0)
                };
                AppShellViewModel.ChannelViewModels.Add(channelViewModel);

                var channelView = new ChannelView { BindingContext = channelViewModel };

                WatchTab.Items.Insert(0, new ShellContent { Title = channel.VanityName, Content = channelView });
                SearchTab.Items.Insert(0, new ShellContent { Title = channel.VanityName, Content = channelView });
                ExclusionsTab.Items.Insert(0, new ShellContent { Title = channel.VanityName, Content = channelView });
            }

            AppShellViewModel.ChannelViewModels.ForEach(c =>
            {
                c.Loading = false;
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

            AppShellViewModel.ChannelViewModels.ForEach(c => c.Videos.Clear());

            CurrentItem.CurrentItem.CurrentItem = CurrentItem.CurrentItem.Items.FirstOrDefault();
        }

        public async Task HandleSharedVideoId(string videoId)
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

            Video video = (await YouTubeApi.Instance.FindVideoDetails(new List<string> { videoId }, null, null, SortMode.AgeDesc)).FirstOrDefault();
            if (video is not null)
            {
                // See if this video is excluded
                if (DatabaseEngine.ExcludedVideosCollection.FindById(video.Id) is { } excludedVideo)
                {
                    video.Excluded = true;
                    video.ExclusionReason = excludedVideo.ExclusionReason;
                }

                bool foundChannel = false;
                foreach (var content in WatchTab.Items)
                {
                    if ((content.Content as ChannelView)?.BindingContext as ChannelViewModel is { } channelViewModel
                        && channelViewModel.Channel.ChannelPlaylist == video.ChannelPlaylist)
                    {
                        try
                        {
                            WatchTab.CurrentItem = content;
                        }
                        catch
                        {
                            // This throws an exception, but it gets far enough.
                        }

                        channelViewModel.Videos.Clear();
                        channelViewModel.Videos.Add(new VideoViewModel(video, this, channelViewModel));
                        foundChannel = true;
                        break;
                    }
                }

                if (!foundChannel)
                {
                    ChannelViewModel channelViewModel = new(this)
                    {
                        Channel = new Channel(nonPersistent: true) {ChannelPlaylist = video.ChannelPlaylist, ChannelId = video.ChannelPlaylist.Replace("UU", "UC") },
                        SelectedSortModeIndex = Preferences.Default.Get(nameof(ChannelViewModel.SelectedSortModeIndex), 0)
                    };
                    AppShellViewModel.ChannelViewModels.Add(channelViewModel);

                    var channelView = new ChannelView { BindingContext = channelViewModel };

                    WatchTab.Items.Insert(0, new ShellContent { Title = Mobile.Resources.Resources.Unknown, Content = channelView });
                    SearchTab.Items.Insert(0, new ShellContent { Title = Mobile.Resources.Resources.Unknown, Content = channelView });
                    ExclusionsTab.Items.Insert(0, new ShellContent { Title = Mobile.Resources.Resources.Unknown, Content = channelView });

                    channelViewModel.Videos.Add(new VideoViewModel(video, this, channelViewModel));

                    channelViewModel.Loading = false;

                    //TabBar.CurrentItem = ShareTab;
                    //                    try
                    //                    {
                    //                        await Current.GoToAsync("//ShareTabRoute/Unknown");
                    //                    }
                    //                    catch
                    //                    {
                    ////
                    //                    }

                    //        ChannelViewModel channelViewModel = new(this)
                    //        {
                    //            Channel = new Channel(nonPersistent: true) { ChannelPlaylist = video.ChannelPlaylist, ChannelId = video.ChannelPlaylist.Replace("UU", "UC") },
                    //            SelectedSortModeIndex = Preferences.Default.Get(nameof(ChannelViewModel.SelectedSortModeIndex), 0)
                    //        };
                    //        AppShellViewModel.ChannelViewModels.Add(channelViewModel);

                    //        var channelView = new ChannelView { BindingContext = channelViewModel };
                    //        var shellContent = new ShellContent { Title = Mobile.Resources.Resources.Unknown, Content = channelView };

                    //        WatchTab.Items.Insert(0, shellContent);

                    //        // Remove all other content
                    //        while (WatchTab.Items.Any(c => c != shellContent))
                    //        {
                    //            try
                    //            {
                    //                foreach (var sc in WatchTab.Items.Where(c => c != shellContent).ToList())
                    //                    WatchTab.Items.Remove(sc);
                    //            }
                    //            catch {}
                    //        }

                    //        TabBar.Items.Remove(SearchTab);
                    //        TabBar.Items.Remove(ExclusionsTab);

                    //        channelViewModel.Videos.Add(new VideoViewModel(video, this, channelViewModel));

                    //        channelViewModel.Loading = false;
                }
            }
        }
    }
}