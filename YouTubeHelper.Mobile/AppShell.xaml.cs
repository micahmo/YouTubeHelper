using Android.Content;
using Android.OS;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Flurl;
using ServerStatusBot.Definitions;
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
        public static string ClientId { get; } = Guid.NewGuid().ToString();

        public static AppShell? Instance { get; private set; }
        private Tab? _currentTab;

        private int _selectedChannelTabIndex;

        private static readonly object _notificationLock = new();

        public AppShell()
        {
            InitializeComponent();
            BindingContext = new AppShellViewModel(this);
            Instance = this;
            _currentTab = ChannelTab;
        }

        protected override bool OnBackButtonPressed()
        {
            if (_canClose)
            {
                Environment.Exit(1);
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

            // Close the cookie browser
            if (Current.Navigation.NavigationStack.Count > 1)
            {
                Current.Navigation.PopAsync();
                return true;
            }

            // Next, select the first channel
            if (TabBar.CurrentItem.CurrentItem != TabBar.CurrentItem.Items.FirstOrDefault())
            {
                TabBar.CurrentItem.CurrentItem = null;
                TabBar.CurrentItem.CurrentItem = TabBar.CurrentItem.Items.FirstOrDefault();
                return true;
            }

            // Or, re-select the main/watch tab
            if (!AppShellViewModel.ChannelTabSelected)
            {
                AppShellViewModel.SelectChannelTab();
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

            BusyIndicator busyIndicator = new BusyIndicator(this, Mobile.Resources.Resources.ConnectingToServer);

            bool connectedToServer = await ConnectToServer(withPrompt: false);

            // If we didn't connect successfully, we need to hide the busy indicator, then connect again with a prompt and a separate busy indicator.
            if (!connectedToServer)
            {
                busyIndicator.Dispose();
                await ConnectToServer(withPrompt: true);
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
                await ServerApiClient.Instance.JoinChannelObjectUpdatesGroup(HandleChannelObjectUpdates);
            });

            busyIndicator.Text = Mobile.Resources.Resources.LoadingChannels;

            ChannelTab.Items.Clear();
            QueueTab.Items.Clear();

            int selectedSortModeIndex = Preferences.Default.Get(nameof(ChannelViewModel.SelectedSortModeIndex), 4);
            int selectedExclusionsModeIndex = Preferences.Default.Get(nameof(ChannelViewModel.SelectedExclusionsModeIndex), 1);
            int selectedExclusionFilterIndex = Preferences.Default.Get(nameof(ChannelViewModel.SelectedExclusionFilterIndex), 0);

            List<Channel> channels = await ServerApiClient.Instance.GetChannels();
            foreach (Channel channel in channels.Reverse<Channel>())
            {
                channel.Changed += (_, _) =>
                {
                    ServerApiClient.Instance.UpdateChannel(channel, ClientId);
                };

                ChannelViewModel channelViewModel = new(this)
                {
                    Channel = channel,
                    SelectedSortModeIndex = selectedSortModeIndex,
                    SelectedExclusionsModeIndex = selectedExclusionsModeIndex,
                    SelectedExclusionFilterIndex = selectedExclusionFilterIndex
                };
                AppShellViewModel.ChannelViewModels.Add(channelViewModel);

                var channelView = new ChannelView { BindingContext = channelViewModel };

                ChannelTab.Items.Insert(0, new ShellContent { Title = channel.VanityName, Content = channelView });
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
                        SelectedExclusionsModeIndex = selectedExclusionsModeIndex,
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

            busyIndicator.Dispose();

            // Check for battery restrictions
            _ = CheckBatteryOptimizations();

            SyncSelectedChannel();

            _loaded = true;
        }

        private void SyncSelectedChannel()
        {
            foreach (Tab tab in new[] { ChannelTab, QueueTab })
            {
                tab.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(Tab.CurrentItem))
                    {
                        if (s is Tab changedTab)
                        {
                            int index = changedTab.Items.IndexOf(changedTab.CurrentItem);
                            if (index >= 0)
                            {
                                _selectedChannelTabIndex = index;
                            }
                        }
                    }
                };
            }
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

        public async Task<bool> ConnectToServer(bool withPrompt = true)
        {
            bool connected = false;

            try
            {
                string? serverAddress = await SecureStorage.Default.GetAsync("server_address");
                ServerApiClient.SetBaseUrl(serverAddress!);
                Settings.Instance = await ServerApiClient.Instance.GetSettings();
                connected = true;
            }
            catch
            {
                // We'll fall into the next block which prompts the user to re-enter
            }

            if (withPrompt)
            {
                while (!connected)
                {
                    var res = await DisplayPromptAsync(Mobile.Resources.Resources.Server, Mobile.Resources.Resources.EnterServerAddress);

                    if (string.IsNullOrEmpty(res))
                    {
                        Environment.Exit(1);
                    }

                    ServerApiClient.SetBaseUrl(res);

                    using (new BusyIndicator(this, Mobile.Resources.Resources.ConnectingToServer))
                    {
                        string err = string.Empty;
                        try
                        {
                            Settings.Instance = await ServerApiClient.Instance.GetSettings();
                        }
                        catch (Exception ex)
                        {
                            err = ex.ToString();
                        }

                        if (!string.IsNullOrEmpty(err))
                        {
                            await DisplayAlert(Mobile.Resources.Resources.Error, string.Format(Mobile.Resources.Resources.ErrorConnectingToServer, err), Mobile.Resources.Resources.OK);
                        }
                        else
                        {
                            connected = true;
                        }
                    }
                }

                // We made it here, so we must have connected successfully. Save the server address.
                await SecureStorage.Default.SetAsync("server_address", ServerApiClient.BaseUrl!);
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
                    if ((await ServerApiClient.Instance.FindVideos(new FindVideosRequest
                        {
                            ExclusionsMode = ExclusionsMode.ShowAll,
                            VideoIds = new List<string> { requestData.VideoId },
                            SortMode = SortMode.AgeDesc,
                            Count = int.MaxValue
                        })).FirstOrDefault() is { } newVideo)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            queueChannel.Videos.Insert(0, new VideoViewModel(newVideo, this, queueChannel));

                            // Scroll the view to the top so we can see the newly inserted item
                            queueChannel.ScrollToTopRequested?.Invoke();
                        });
                    }
                }
            }

            // If any videos that are currently being displayed match the ID in the request data, then start listening for updates
            // This will include the queue
            foreach (VideoViewModel videoViewModel in AppShellViewModel.AllVideos)
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

            Video updatedVideo = updatedVideoArgs.Obj;

            // If any videos that are currently being displayed match the ID in the updated video, then update it in the UI
            // This will include the queue
            foreach (VideoViewModel videoViewModel in AppShellViewModel.AllVideos)
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
            lock (_notificationLock)
            {
                if (updatedChannelArgs.Originator == ClientId) return;

                Channel updatedChannel = updatedChannelArgs.Obj;

                ChannelViewModel? newlyAddedChannelViewModel = null;

                // Look for the channel
                foreach (Tab tab in new List<Tab> { ChannelTab })
                {
                    bool found = false;
                    foreach (ShellContent? content in tab.Items.ToList())
                    {
                        if (content.Content is ChannelView { BindingContext: ChannelViewModel channelViewModel } channelView && channelViewModel.Channel?.Id == updatedChannel.Id)
                        {
                            string currentTitle = string.Empty;
                            int currentIndex = default;
                            int newIndex = default;
                            MainThread.InvokeOnMainThreadAsync(() =>
                            {
                                currentTitle = content.Title;
                                currentIndex = tab.Items.IndexOf(content);
                                newIndex = updatedChannel.Index;
                            }).GetAwaiter().GetResult();


                            // We have it, but it's marked for deletion. Remove it!
                            if (updatedChannel.MarkForDeletion)
                            {
                                MainThread.InvokeOnMainThreadAsync(() =>
                                {
                                    try
                                    {
                                        tab.Items.Remove(content);
                                    }
                                    catch
                                    {
                                        // This throws an exception, but it gets far enough.
                                    }
                                }).GetAwaiter().GetResult();

                                AppShellViewModel.ChannelViewModels.Remove(channelViewModel);
                                channelViewModel.Channel!.MarkForDeletion = true;
                                channelViewModel.Channel!.Persistent = false;
                            }
                            else
                            {
                                // We have it! Update it
                                channelViewModel.Channel!.Persistent = false; // stop doing updates
                                channelViewModel.Channel.Identifier = updatedChannel.Identifier;
                                channelViewModel.Channel.ChannelPlaylist = updatedChannel.ChannelPlaylist;
                                channelViewModel.Channel.VanityName = updatedChannel.VanityName;
                                channelViewModel.Channel.Description = updatedChannel.Description;
                                channelViewModel.Channel.DateRangeLimit = updatedChannel.DateRangeLimit;
                                channelViewModel.Channel.EnableDateRangeLimit = updatedChannel.EnableDateRangeLimit;
                                channelViewModel.Channel.VideoLengthMinimum = updatedChannel.VideoLengthMinimum;
                                channelViewModel.Channel.EnableVideoLengthMinimum = updatedChannel.EnableVideoLengthMinimum;
                                channelViewModel.Channel.ExcludeDaysUtc = updatedChannel.ExcludeDaysUtc;
                                channelViewModel.Channel.IncludeDaysUtc = updatedChannel.IncludeDaysUtc;
                                channelViewModel.Channel.SendNotificationsForNewVideos = updatedChannel.SendNotificationsForNewVideos;
                                channelViewModel.Channel.AutoDownloadNewVideos = updatedChannel.AutoDownloadNewVideos;
                                channelViewModel.Channel.Persistent = true; // resume updates

                                // Force the UI to update my "moving" the channel.
                                // This can be if the title updates (which doesn't work with binding, so we have to remove and re-add)
                                // Or if the user does actually change the index, then we'll handle that here as well
                                if (currentIndex != newIndex || updatedChannel.VanityName != currentTitle)
                                {
                                    MainThread.InvokeOnMainThreadAsync(() => MoveChannel(tab, content, channelView, updatedChannel.VanityName, newIndex)).GetAwaiter().GetResult();
                                }
                            }

                            found = true;
                        }
                    }

                    // We don't have it, and it's not marked for deletion, so it must be new. Add it!
                    if (!found && !updatedChannel.MarkForDeletion)
                    {
                        if (newlyAddedChannelViewModel is null)
                        {
                            newlyAddedChannelViewModel = new(this)
                            {
                                Channel = updatedChannel,
                                SelectedSortModeIndex = Preferences.Default.Get(nameof(ChannelViewModel.SelectedSortModeIndex), 4),
                                SelectedExclusionsModeIndex = Preferences.Default.Get(nameof(ChannelViewModel.SelectedExclusionsModeIndex), 1),
                                SelectedExclusionFilterIndex = Preferences.Default.Get(nameof(ChannelViewModel.SelectedExclusionFilterIndex), 0),
                                Loading = false
                            };

                            AppShellViewModel.ChannelViewModels.Add(newlyAddedChannelViewModel);

                            updatedChannel.Changed += async (_, _) =>
                            {
                                await ServerApiClient.Instance.UpdateChannel(updatedChannel, ClientId);
                            };
                        }

                        MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            ChannelView channelView = new ChannelView { BindingContext = newlyAddedChannelViewModel };
                            tab.Items.Add(new ShellContent { Title = updatedChannel.VanityName, Content = channelView });
                        }).GetAwaiter().GetResult();
                    }
                }
            }
        }

        public AppShellViewModel AppShellViewModel => (AppShellViewModel)BindingContext;

        private void Shell_Navigated(object _, ShellNavigatedEventArgs __)
        {
            AppShellViewModel.RaisePropertyChanged(nameof(AppShellViewModel.ChannelTabSelected));
            AppShellViewModel.RaisePropertyChanged(nameof(AppShellViewModel.QueueTabSelected));

            // Clear video lists when switching bottom tabs (NOT top tabs)
            if (CurrentItem.CurrentItem != _currentTab)
            {
                _currentTab = CurrentItem.CurrentItem as Tab;
                AppShellViewModel.ChannelViewModels.ForEach(c => c.Videos.Clear());

                if (_currentTab != null && _selectedChannelTabIndex < _currentTab.Items.Count)
                {
                    _currentTab.CurrentItem = _currentTab.Items[_selectedChannelTabIndex];
                }

                if (AppShellViewModel.QueueTabSelected)
                {
                    AppShellViewModel.QueueChannelViewModel!.FindVideos();
                }
            }
        }

        public async Task HandleSharedLink(string rawUrl, bool downloadVideo = false)
        {
            string? videoId = YouTubeUtils.GetVideoIdFromUrl(rawUrl);
            if (!string.IsNullOrEmpty(YouTubeUtils.GetVideoIdFromUrl(rawUrl)))
            {
                await HandleSharedLink(videoId, null, null, null, downloadVideo: downloadVideo);
            }

            Url url = new Url(rawUrl);
            if (url.PathSegments.FirstOrDefault(p => p.StartsWith('@')) is { } channelHandle)
            {
                await HandleSharedLink(null, channelHandle, null, null, downloadVideo: downloadVideo);
            }

            if (url.PathSegments.Count >= 2
                && url.PathSegments[0].Equals("channel", StringComparison.OrdinalIgnoreCase)
                && url.PathSegments[1].StartsWith("UC", StringComparison.OrdinalIgnoreCase))
            {
                await HandleSharedLink(null, null, url.PathSegments[1], YouTubeUtils.ToChannelPlaylist(url.PathSegments[1]), downloadVideo: downloadVideo);
            }
        }

        public async Task HandleSharedLink(string? videoId, string? channelHandle, string? channelId, string? channelPlaylist, bool downloadVideo = false)
        {
            while (!_loaded)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            BusyIndicator busyIndicator = new BusyIndicator(this, Mobile.Resources.Resources.HandlingSharedLink);

            Video? video = default;

            if (!string.IsNullOrEmpty(videoId))
            {
                busyIndicator.Text = Mobile.Resources.Resources.FindingSharedVideo;

                video = (await ServerApiClient.Instance.FindVideos(new FindVideosRequest
                {
                    ExclusionsMode = ExclusionsMode.ShowAll,
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
                // We successfully identified a channel, so navigate to the watch tab
                if (!AppShellViewModel.ChannelTabSelected)
                {
                    AppShellViewModel.SelectChannelTab();
                }

                ChannelViewModel? foundChannelViewModel = default;
                foreach (var content in ChannelTab.Items)
                {
                    if ((content.Content as ChannelView)?.BindingContext as ChannelViewModel is { } channelViewModel
                        && channelViewModel.Channel?.ChannelPlaylist == channelPlaylist)
                    {
                        foundChannelViewModel = channelViewModel;

                        try
                        {
                            ChannelTab.CurrentItem = content;
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
                    string channelName = await ServerApiClient.Instance.FindChannelName(channelId, Mobile.Resources.Resources.Unknown);

                    foundChannelViewModel = new(this)
                    {
                        Channel = new Channel(persistent: false)
                        {
                            VanityName = channelName,
                            ChannelPlaylist = channelPlaylist
                        },
                        SelectedSortModeIndex = Preferences.Default.Get(nameof(ChannelViewModel.SelectedSortModeIndex), 4),
                        SelectedExclusionsModeIndex = Preferences.Default.Get(nameof(ChannelViewModel.SelectedExclusionsModeIndex), 1),
                        SelectedExclusionFilterIndex = Preferences.Default.Get(nameof(ChannelViewModel.SelectedExclusionFilterIndex), 0)
                    };
                    AppShellViewModel.ChannelViewModels.Add(foundChannelViewModel);

                    var channelView = new ChannelView { BindingContext = foundChannelViewModel };

                    ShellContent watchTabContent = new ShellContent { Title = channelName, Content = channelView };
                    ChannelTab.Items.Insert(0, watchTabContent);
                    ChannelTab.CurrentItem = watchTabContent;

                    foundChannelViewModel.Loading = false;
                }

                foundChannelViewModel.Videos.Clear();

                if (video is not null)
                {
                    VideoViewModel videoViewModel = new VideoViewModel(video, this, foundChannelViewModel) { IsDescriptionExpanded = true };
                    foundChannelViewModel.Videos.Add(videoViewModel);

                    Task _ = QueueUtils.TryJoinDownloadGroup(videoViewModel);

                    if (downloadVideo)
                    {
                        Task __ = videoViewModel.DownloadVideo("plex");
                    }
                }
            }

            busyIndicator.Dispose();
        }

        public async Task NavigateToQueueTab()
        {
            while (!_loaded)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                TabBar.CurrentItem = QueueTab;
                TabBar.CurrentItem.CurrentItem = QueueTab.Items.FirstOrDefault();
            });
        }

        public async Task HandleOpenInPlex(string plexRatingKey)
        {
            BusyIndicator busyIndicator = new BusyIndicator(this, Mobile.Resources.Resources.OpeningInPlex);

            try
            {
                string plexUri = $"plex://server://8316eb530162c189b29f3250d4734700515fc5f8/com.plexapp.plugins.library/library/metadata/{plexRatingKey}";
                await Launcher.Default.OpenAsync(new Uri(plexUri));
            }
            catch (Exception ex)
            {
                // Show message box for any errors instead of toast so we can see the full error
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert("Error opening Plex", ex.ToString(), "OK");
                });
            }
            finally
            {
                busyIndicator.Dispose();
            }
        }

        private static void MoveChannel(Tab tab, ShellContent content, ChannelView channelView, string? title, int newIndex)
        {
            ShellContent? previousCurrent = tab.CurrentItem;
            bool wasCurrent = tab.CurrentItem == content;
            int previousCurrentIndex = tab.Items.IndexOf(previousCurrent);

            int index = tab.Items.IndexOf(content);

            // Remove existing
            tab.Items.RemoveAt(index);

            // Create replacement with same content/binding, new title
            ShellContent replacement = new ShellContent { Title = title, Content = channelView };

            // Insert back at same index and keep selection
            tab.Items.Insert(newIndex, replacement);

            // If the page we just removed was the selected page, then the UI will default to selecting the first page, so we have to fix that.
            // Also if the previously selected page was the last page, that will also mess things up (because the index of the last page no longer exists)
            // And finally, if this is a real move (different indexes, then we ALWAYS fix it
            if (wasCurrent || previousCurrentIndex == tab.Items.Count - 1 || newIndex != index)
            {
                ShellContent? toRestore = wasCurrent ? replacement : previousCurrent;

                Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        tab.CurrentItem = null;
                        tab.CurrentItem = toRestore;
                    });
                });
            }
        }
    }
}