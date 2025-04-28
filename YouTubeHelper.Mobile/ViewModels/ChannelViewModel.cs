using System.Windows.Input;
using CommunityToolkit.Maui.Alerts;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using MongoDB.Driver;
using MongoDBHelpers;
using Newtonsoft.Json;
using Polly;
using ServerStatusBot.Definitions;
using ServerStatusBot.Definitions.Api;
using ServerStatusBot.Definitions.Database.Models;
using ServerStatusBot.Definitions.Models;
using YouTubeHelper.Mobile.Views;
using YouTubeHelper.Shared.Utilities;

namespace YouTubeHelper.Mobile.ViewModels
{
    public class ChannelViewModel : ObservableObject
    {
        public ChannelViewModel(AppShell page)
        {
            Page = page;

            PropertyChanged += (_, args) =>
            {
                if (!_listeningToPropertyChanges)
                {
                    return;
                }

                if (args.PropertyName is nameof(ShowExcludedVideos) or nameof(SelectedSortModeIndex) or nameof(SelectedExclusionFilterIndex) or nameof(ExactSearchTerm))
                {
                    Preferences.Default.Set(nameof(SelectedSortModeIndex), SelectedSortModeIndex);
                    Preferences.Default.Set(nameof(SelectedExclusionFilterIndex), SelectedExclusionFilterIndex);

                    _listeningToPropertyChanges = false;
                    Page.AppShellViewModel.ChannelViewModels.ForEach(c =>
                    {
                        c.ShowExcludedVideos = ShowExcludedVideos;

                        if (args.PropertyName is nameof(SelectedSortModeIndex))
                        {
                            c.SelectedSortModeIndex = SelectedSortModeIndex;
                        }

                        if (args.PropertyName is nameof(SelectedExclusionFilterIndex))
                        {
                            c.SelectedExclusionFilterIndex = SelectedExclusionFilterIndex;
                        }

                        c.ExactSearchTerm = ExactSearchTerm;
                    });
                    _listeningToPropertyChanges = true;
                }
            };

            Videos.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SearchCount));
        }

        private static bool _listeningToPropertyChanges = true;

        public Channel? Channel { get; init; }

        public MyObservableCollection<VideoViewModel> Videos { get; } = new();

        public ICommand ToggleShowExcludedVideosCommand => _toggleShowExcludedVideosCommand ??= new RelayCommand(() =>
        {
            ShowExcludedVideos = !ShowExcludedVideos;
        });
        private ICommand? _toggleShowExcludedVideosCommand;

        public ICommand ToggleEnableDateRangeLimitCommand => _toggleEnableDateRangeLimitCommand ??= new RelayCommand(() =>
        {
            Channel!.EnableDateRangeLimit = !Channel.EnableDateRangeLimit;
        });
        private ICommand? _toggleEnableDateRangeLimitCommand;

        public ICommand FindVideosCommand => _findVideosCommand ??= new RelayCommand(FindVideos);
        private ICommand? _findVideosCommand;

        public void FindVideos()
        {
            FindVideos(count: 10);
        }
        
        public async void FindVideos(int count)
        {
            if (_findInProgress)
            {
                return;
            }

            _findInProgress = true;
            IsRefreshing = false;

            await Policy
                .Handle<Exception>().FallbackAsync(_ => Task.CompletedTask, async ex =>
                {
                    // This happens once we've retried and failed.
                    // Show that there was an unhandled error.
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await Page.DisplayAlert(Resources.Resources.Error, string.Format(Resources.Resources.ErrorProcessingRequestMessage, ex.Message), Resources.Resources.OK);
                    });
                })
                .WrapAsync(Policy.Handle<Exception>().RetryAsync(5, async (ex, _) =>
                {
                    // This retries a few times and lets us reset things before we try again.
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (ex is MongoConnectionPoolPausedException)
                        {
                            DatabaseEngine.Reset();
                        }

                        return Task.CompletedTask;
                    });
                }))
                .ExecuteAsync(async () =>
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        using (new BusyIndicator(Page))
                        {
                            // Perform the lookup. Polly executes this on the main thread. Any exceptions will be handled, with a single retry.

                            Videos.Clear();

                            _hasSearchedAtLeastOnce = true;

                            // FindVideos
                            if (Page.AppShellViewModel.WatchTabSelected || Page.AppShellViewModel.SearchTabSelected)
                            {
                                List<Video> exclusions = await ServerApiClient.Instance.GetExcludedVideosByChannel(Channel!.ChannelPlaylist!);
                                List<string>? searchTerms = null;

                                //if (Page.SearchTabSelected && !string.IsNullOrEmpty(MainControlViewModel.LookupSearchTerm))
                                //{
                                //    (await YouTubeApi.Instance.SearchVideos(Channel, exclusions, MainControlViewModel.ShowExcludedVideos, MainControlViewModel.SelectedSortMode.Value, MainControlViewModel.LookupSearchTerm)).ToList().ForEach(v => Videos.Add(new VideoViewModel(v, MainControlViewModel, this)));
                                //}
                                //else
                                {
                                    if (Page.AppShellViewModel.SearchTabSelected)
                                    {
                                        if (!string.IsNullOrEmpty(ExactSearchTerm))
                                        {
                                            string exactSearchTermTrimmed = ExactSearchTerm.Trim();

                                            if (exactSearchTermTrimmed.StartsWith('"')
                                                && exactSearchTermTrimmed.EndsWith('"')
                                                && !string.IsNullOrEmpty(exactSearchTermTrimmed.TrimStart('"').TrimEnd('"')))
                                            {
                                                searchTerms = new List<string> { exactSearchTermTrimmed.TrimStart('"').TrimEnd('"') };
                                            }
                                            else
                                            {
                                                searchTerms = exactSearchTermTrimmed.Split().ToList();
                                            }

                                            // Update the history
                                            string? searchTermHistory = Preferences.Default.Get<string?>(nameof(ExactSearchTerm), null);
                                            List<string> searchTermHistoryList;
                                            try
                                            {
                                                searchTermHistoryList = JsonConvert.DeserializeObject<List<string>>(searchTermHistory!) ?? new();
                                            }
                                            catch
                                            {
                                                searchTermHistoryList = new();
                                            }

                                            searchTermHistoryList.RemoveAll(s => s.Equals(exactSearchTermTrimmed, StringComparison.OrdinalIgnoreCase));
                                            searchTermHistoryList.Insert(0, exactSearchTermTrimmed);
                                            searchTermHistoryList = searchTermHistoryList.Take(5).ToList();
                                            Preferences.Default.Set(nameof(ExactSearchTerm), JsonConvert.SerializeObject(searchTermHistoryList));
                                        }
                                    }

                                    IEnumerable<Video> videos = await YouTubeApi.Instance.FindVideos(
                                        Channel,
                                        exclusions,
                                        ShowExcludedVideos,
                                        SelectedSortMode.Value, searchTerms, (_, _) =>
                                        {
                                            // TODO: Update progress?
                                        },
                                        count,
                                        Page.AppShellViewModel.WatchTabSelected && Channel?.EnableDateRangeLimit == true ? Channel.DateRangeLimit : null,
                                        Page.AppShellViewModel.WatchTabSelected && Channel?.EnableVideoLengthMinimum == true ? Channel.VideoLengthMinimum : null);
                                    
                                    List<VideoViewModel> videoViewModels = await Task.Run(() => videos.Select(v => new VideoViewModel(v, Page, this)).ToList());
                                    Videos.AddRange(videoViewModels);

                                    try
                                    {
                                        List<RequestData> distinctQueue = await ServerApiClient.Instance.GetQueue();
                                        foreach (VideoViewModel? videoViewModel in videoViewModels)
                                        {
                                            Guid? requestId = distinctQueue.FirstOrDefault(v => v.VideoId! == videoViewModel.Video.Id)?.RequestGuid;
                                            if (requestId != null)
                                            {
                                                await ServerApiClient.Instance.JoinDownloadGroup(requestId!.ToString()!, requestData => videoViewModel.UpdateCheck(requestId!.ToString()!, requestData, showInAppNotifications: false));
                                            }
                                        }
                                    }
                                    catch
                                    {
                                        // Ignore this, because getting the queue isn't a big deal, and we don't want it to trip the outer retry.
                                    }

                                    // TODO: Reset progress?
                                }
                            }
                            // FindExclusions
                            else if (Page.AppShellViewModel.ExclusionsTabSelected)
                            {
                                List<Video> exclusions = await ServerApiClient.Instance.GetExcludedVideosByChannel(Channel!.ChannelPlaylist!);

                                if (SelectedExclusionFilter.Value != ExclusionReason.None)
                                {
                                    exclusions = exclusions.Where(v => SelectedExclusionFilter.Value.HasFlag(v.ExclusionReason)).ToList();
                                }

                                List<Video> videos = await ServerApiClient.Instance.FindVideoDetails(new FindVideoDetailsRequest
                                {
                                    VideoIds = exclusions.Select(v => v.Id).ToList(),
                                    ExcludedVideos = exclusions,
                                    Channel = Channel,
                                    SortMode = SelectedSortMode?.Value ?? SortMode.AgeDesc,
                                    Count = int.MaxValue
                                });
                                List<VideoViewModel>? videoViewModels = await Task.Run(() => videos.Select(v => new VideoViewModel(v, Page, this)).ToList());
                                Videos.AddRange(videoViewModels);
                            }
                            else if (Page.AppShellViewModel.QueueTabSelected)
                            {
                                // Get the queue from the server
                                List<RequestData> distinctQueue = await ServerApiClient.Instance.GetQueue();

                                // Get all excluded videos
                                List<Video> excludedVideos = await ServerApiClient.Instance.GetAllExcludedVideos();

                                List<Video> queuedVideos = (await ServerApiClient.Instance.FindVideoDetails(new FindVideoDetailsRequest
                                    {
                                        VideoIds = distinctQueue.Select(queueItem => queueItem.VideoId!).ToList(),
                                        ExcludedVideos = excludedVideos,
                                        Count = int.MaxValue
                                    }))
                                    .OrderByDescending(video => distinctQueue.FirstOrDefault(v => v.VideoId == video.Id)?.DateAdded ?? DateTime.MinValue)
                                    .ToList();

                                foreach (Video? video in queuedVideos)
                                {
                                    VideoViewModel videoViewModel = new VideoViewModel(video, Page, this);
                                    Videos.Add(videoViewModel);
                                    string requestId = distinctQueue.First(v => v.VideoId == video.Id).RequestGuid.ToString();

                                    // Do not await this, as it slows the loading of the queue page
                                    Task _ = ServerApiClient.Instance.JoinDownloadGroup(requestId, requestData => videoViewModel.UpdateCheck(requestId, requestData, showInAppNotifications: false));
                                }
                            }
                        }
                    });
                });

            _findInProgress = false;
        }

        private bool _findInProgress;
        private bool _hasSearchedAtLeastOnce;

        // This action is set by the View during binding
        // so that external callers can call back into the View to scroll up
        public Action? ScrollToTopRequested { get; set; }

        public ICommand ChannelOptionsCommand => _channelOptionsCommand ??= new RelayCommand(ChannelOptions);
        private ICommand? _channelOptionsCommand;

        private async void ChannelOptions()
        {
            if (_isOptionsOpen)
            {
                return;
            }

            _isOptionsOpen = true;

            try
            {
                List<string> options = new();

                if (Page.AppShellViewModel.WatchTabSelected)
                {
                    //options.Add(Resources.Resources.SearchWithLimit);
                    options.Add(Resources.Resources.SearchNoLimit);
                }

                if (Page.AppShellViewModel.SearchTabSelected)
                {
                    options.Add(Preferences.Default.Get<string?>(nameof(ExactSearchTerm), null) is null 
                        ? Resources.Resources.SearchHistoryNone 
                        : Resources.Resources.SearchHistory);
                }
                
                var action = await Page.DisplayActionSheet(Channel?.VanityName, Resources.Resources.Cancel, null, options.ToArray());

                if (action == Resources.Resources.SearchWithLimit)
                {
                    int initialValue = Preferences.Default.Get("SearchLimit", 250);
                    var res = await Page.DisplayPromptAsync(Resources.Resources.SearchLimit, Resources.Resources.EnterDesiredLimit, initialValue: initialValue.ToString(), keyboard: Keyboard.Numeric);

                    if (int.TryParse(res, out int limit))
                    {
                        FindVideos(limit);
                        Preferences.Default.Set("SearchLimit", limit);
                    }
                    else if (res is not null)
                    {
                        await Toast.Make(Resources.Resources.EnterNumericalLimit).Show();
                    }
                }
                else if (action == Resources.Resources.SearchNoLimit)
                {
                    FindVideos(int.MaxValue);
                }
                else if (action == Resources.Resources.SearchHistory)
                {
                    string? searchTermHistory = Preferences.Default.Get<string?>(nameof(ExactSearchTerm), null);
                    List<string> searchTermHistoryList;
                    try
                    {
                        searchTermHistoryList = JsonConvert.DeserializeObject<List<string>>(searchTermHistory!) ?? new();
                    }
                    catch
                    {
                        searchTermHistoryList = new();
                    }

                    var res = await Page.DisplayActionSheet(Channel?.VanityName, Resources.Resources.Cancel, Resources.Resources.Clear, searchTermHistoryList.ToArray());

                    if (res == Resources.Resources.Clear)
                    {
                        Preferences.Default.Set<string?>(nameof(ExactSearchTerm), null);
                    }
                    else if (res is not null && res != Resources.Resources.Cancel)
                    {
                        ExactSearchTerm = res;
                        FindVideos();
                    }
                }
            }
            finally
            {
                _isOptionsOpen = false;
            }
        }
        private static bool _isOptionsOpen;

        public IEnumerable<SortModeExtended> SortModeValues { get; } = Enum.GetValues(typeof(SortMode)).OfType<SortMode>().Select(m => new SortModeExtended(m)).ToList();

        public IEnumerable<ExclusionReasonExtended> ExclusionReasonValues { get; } = Enum.GetValues(typeof(ExclusionReason)).OfType<ExclusionReason>().Select(m => new ExclusionReasonExtended(m)).ToList();

        public SortModeExtended SelectedSortMode => SortModeValues.ElementAt(SelectedSortModeIndex);

        public int SelectedSortModeIndex
        {
            get => _selectedSortModeIndex;
            set
            {
                _selectedSortModeIndex = value;
                
                // Have to do an explicit raise here since setting to 0 doesn't.
                OnPropertyChanged();
            }
        }
        private int _selectedSortModeIndex = 4;

        public ExclusionReasonExtended SelectedExclusionFilter => ExclusionReasonValues.ElementAt(SelectedExclusionFilterIndex);

        public int SelectedExclusionFilterIndex
        {
            get => _selectedExclusionFilterIndex;
            set
            {
                _selectedExclusionFilterIndex = value;

                // Have to do an explicit raise here since setting to 0 doesn't.
                OnPropertyChanged(nameof(SelectedExclusionFilterIndex));
            }
        }
        private int _selectedExclusionFilterIndex;

        public string? ExactSearchTerm
        {
            get => _exactSearchTerm;
            set => SetProperty(ref _exactSearchTerm, value);
        }
        private string? _exactSearchTerm;

        public bool ShowExcludedVideos
        {
            get => _showExcludedVideos;
            set => SetProperty(ref _showExcludedVideos, value);
        }
        private bool _showExcludedVideos;

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => SetProperty(ref _isRefreshing, value);
        }
        private bool _isRefreshing;

        public AppShell Page { get; }

        public string SearchCount => _hasSearchedAtLeastOnce ? string.Format(Resources.Resources.SearchCount, Videos.Count) : Resources.Resources.Search;

        public bool Loading
        {
            get => _loading;
            set => SetProperty(ref _loading, value);
        }
        private bool _loading = true;

        public bool ShowPlayer
        {
            get => _showPlayer;
            set => SetProperty(ref _showPlayer, value);
        }
        private bool _showPlayer;

        public string? CurrentVideoUrl
        {
            get => _currentVideoUrl;
            set => SetProperty(ref _currentVideoUrl, value);
        }
        private string? _currentVideoUrl;
    }
}
