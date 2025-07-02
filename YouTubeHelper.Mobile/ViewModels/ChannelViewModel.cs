using System.Windows.Input;
using CommunityToolkit.Maui.Alerts;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
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

                if (args.PropertyName is nameof(ShowExcludedVideos) or nameof(SelectedSortModeIndex) or nameof(SelectedExclusionFilterIndex) or nameof(SearchByTitleTerm))
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

                        c.SearchByTitleTerm = SearchByTitleTerm;
                    });
                    _listeningToPropertyChanges = true;
                }
            };

            Videos.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SearchCount));

            Page.AppShellViewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(Page.AppShellViewModel.QueueTabSelected))
                {
                    OnPropertyChanged(nameof(ShowFab));
                }
            };
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
                .WrapAsync(Policy.Handle<Exception>().RetryAsync(5, (_, _) =>
                {
                    // Nothing to do
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
                                List<string>? searchTerms = null;

                                if (Page.AppShellViewModel.SearchTabSelected)
                                {
                                    if (!string.IsNullOrEmpty(SearchByTitleTerm))
                                    {
                                        string searchByTitleTermTrimmed = SearchByTitleTerm.Trim();

                                        if (searchByTitleTermTrimmed.StartsWith('"')
                                            && searchByTitleTermTrimmed.EndsWith('"')
                                            && !string.IsNullOrEmpty(searchByTitleTermTrimmed.TrimStart('"').TrimEnd('"')))
                                        {
                                            searchTerms = new List<string> { searchByTitleTermTrimmed.TrimStart('"').TrimEnd('"') };
                                        }
                                        else
                                        {
                                            searchTerms = searchByTitleTermTrimmed.Split().ToList();
                                        }

                                        // Update the history
                                        string? searchTermHistory = Preferences.Default.Get<string?>(nameof(SearchByTitleTerm), null);
                                        List<string> searchTermHistoryList;
                                        try
                                        {
                                            searchTermHistoryList = JsonConvert.DeserializeObject<List<string>>(searchTermHistory!) ?? new();
                                        }
                                        catch
                                        {
                                            searchTermHistoryList = new();
                                        }

                                        searchTermHistoryList.RemoveAll(s => s.Equals(searchByTitleTermTrimmed, StringComparison.OrdinalIgnoreCase));
                                        searchTermHistoryList.Insert(0, searchByTitleTermTrimmed);
                                        searchTermHistoryList = searchTermHistoryList.Take(5).ToList();
                                        Preferences.Default.Set(nameof(SearchByTitleTerm), JsonConvert.SerializeObject(searchTermHistoryList));
                                    }

                                    List<Video> videos = await ServerApiClient.Instance.FindVideos(new FindVideosRequest
                                    {
                                        Channel = Channel,
                                        ShowExclusions = ShowExcludedVideos,
                                        SortMode = SelectedSortMode.Value,
                                        SearchTerms = searchTerms,
                                        Count = count,
                                        DateRangeLimit = Page.AppShellViewModel.WatchTabSelected && Channel?.EnableDateRangeLimit == true ? Channel.DateRangeLimit : null,
                                        VideoLengthMinimum = Page.AppShellViewModel.WatchTabSelected && Channel?.EnableVideoLengthMinimum == true ? Channel.VideoLengthMinimum : null
                                    });

                                    List<VideoViewModel> videoViewModels = await Task.Run(() => videos.Select(v => new VideoViewModel(v, Page, this)).ToList());
                                    Videos.AddRange(videoViewModels);

                                    await QueueUtils.TryJoinDownloadGroup(videoViewModels);
                                }
                            }
                            // FindExclusions
                            else if (Page.AppShellViewModel.ExclusionsTabSelected)
                            {
                                List<Video> videos = await ServerApiClient.Instance.FindVideos(new FindVideosRequest
                                {
                                    ShowExclusions = true,
                                    ExclusionReasonFilter = SelectedExclusionFilter.Value,
                                    Channel = Channel,
                                    SortMode = SelectedSortMode.Value,
                                    Count = int.MaxValue
                                });
                                List<VideoViewModel> videoViewModels = await Task.Run(() => videos.Select(v => new VideoViewModel(v, Page, this)).ToList());
                                Videos.AddRange(videoViewModels);
                            }
                            else if (Page.AppShellViewModel.QueueTabSelected)
                            {
                                // Get the queue from the server
                                List<RequestData> distinctQueue = await ServerApiClient.Instance.GetQueue();

                                List<Video> queuedVideos = (await ServerApiClient.Instance.FindVideos(new FindVideosRequest
                                    {
                                        ShowExclusions = true,
                                        VideoIds = distinctQueue.Select(queueItem => queueItem.VideoId).ToList(),
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
                    options.Add(Preferences.Default.Get<string?>(nameof(SearchByTitleTerm), null) is null 
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
                    string? searchTermHistory = Preferences.Default.Get<string?>(nameof(SearchByTitleTerm), null);
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
                        Preferences.Default.Set<string?>(nameof(SearchByTitleTerm), null);
                    }
                    else if (res is not null && res != Resources.Resources.Cancel)
                    {
                        SearchByTitleTerm = res;
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

        public string? SearchByTitleTerm
        {
            get => _searchByTitleTerm;
            set => SetProperty(ref _searchByTitleTerm, value);
        }
        private string? _searchByTitleTerm;

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

        public bool IsFabOpen
        {
            get => _isFabOpen;
            set
            {
                SetProperty(ref _isFabOpen, value);
                ChannelView?.AnimateDimBackground(_isFabOpen);
                ChannelView?.UpdateFabIcon(_isFabOpen);
            }
        }
        private bool _isFabOpen;

        // For when it's closed via tapping on the dim area
        public ICommand CloseFabCommand => _closeFabCommand ??= new RelayCommand(() => IsFabOpen = false);
        private ICommand? _closeFabCommand;

        /// <summary>
        /// Whether or not the FAB should be shown
        /// </summary>
        public bool ShowFab => !Page.AppShellViewModel.QueueTabSelected;

        /// <summary>
        /// A reference to the view control
        /// </summary>
        public ChannelView? ChannelView { get; set; }
    }
}
