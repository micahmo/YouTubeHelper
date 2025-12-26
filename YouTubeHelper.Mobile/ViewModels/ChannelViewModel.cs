using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using Newtonsoft.Json;
using Polly;
using ServerStatusBot.Definitions;
using ServerStatusBot.Definitions.Api;
using ServerStatusBot.Definitions.Database.Models;
using ServerStatusBot.Definitions.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;
using YouTubeHelper.Mobile.Views;
using YouTubeHelper.Shared.Mappers;
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

                if (args.PropertyName is nameof(SelectedExclusionsModeIndex)
                    or nameof(SelectedSortModeIndex)
                    or nameof(SelectedExclusionFilterIndex)
                    or nameof(SearchByTitleTerm)
                    or nameof(EnableCountLimit)
                    or nameof(CountLimit))
                {
                    Preferences.Default.Set(nameof(SelectedSortModeIndex), SelectedSortModeIndex);
                    Preferences.Default.Set(nameof(SelectedExclusionsModeIndex), SelectedExclusionsModeIndex);
                    Preferences.Default.Set(nameof(SelectedExclusionFilterIndex), SelectedExclusionFilterIndex);

                    _listeningToPropertyChanges = false;
                    Page.AppShellViewModel.ChannelViewModels.ForEach(c =>
                    {
                        if (args.PropertyName == nameof(SelectedExclusionsModeIndex))
                        {
                            c.SelectedExclusionsModeIndex = SelectedExclusionsModeIndex;
#pragma warning disable CS0618
                            c.OnPropertyChanged(nameof(ShowExclusions));
                            OnPropertyChanged(nameof(ShowExclusions));
#pragma warning restore CS0618

                            OnPropertyChanged(nameof(ShowExclusionReasonFilter));
                        }

                        if (args.PropertyName is nameof(SelectedSortModeIndex))
                        {
                            c.SelectedSortModeIndex = SelectedSortModeIndex;
                        }

                        if (args.PropertyName is nameof(SelectedExclusionFilterIndex))
                        {
                            c.SelectedExclusionFilterIndex = SelectedExclusionFilterIndex;
                        }

                        if (args.PropertyName is nameof(EnableCountLimit) or nameof(CountLimit))
                        {
                            c.EnableCountLimit = EnableCountLimit;
                            c.CountLimit = CountLimit;
                        }

                        c.SearchByTitleTerm = SearchByTitleTerm;
                    });
                    _listeningToPropertyChanges = true;

                    OnPropertyChanged(nameof(SearchOptionsSummary));
                }
            };

            Videos.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SearchCount));

            Page.AppShellViewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(Page.AppShellViewModel.QueueTabSelected))
                {
                    OnPropertyChanged(nameof(ShowFab));
                }

                OnPropertyChanged(nameof(ShowExclusionReasonFilter));
            };
        }

        private static bool _listeningToPropertyChanges = true;

        public Channel? Channel
        {
            get => _channel!;
            init
            {
                _channel = value;

                if (_channel is not null)
                {
                    _channel.PropertyChanged += (_, channelArgs) =>
                    {
                        if (channelArgs.PropertyName is nameof(Channel.EnableDateRangeLimit)
                            or nameof(Channel.DateRangeLimit)
                            or nameof(Channel.EnableVideoLengthMinimum)
                            or nameof(Channel.VideoLengthMinimum)
                            or nameof(Channel.ExcludeDaysUtc)
                            or nameof(Channel.IncludeDaysUtc))
                        {
                            OnPropertyChanged(nameof(SearchOptionsSummary));
                        }

                        if (channelArgs.PropertyName is nameof(Channel.ExcludeDaysUtc) or nameof(Channel.IncludeDaysUtc))
                        {
                            SetupDaysOfWeek();
                            OnPropertyChanged(nameof(ExcludeDaysOfWeek));
                            OnPropertyChanged(nameof(IncludeDaysOfWeek));
                            OnPropertyChanged(nameof(ExcludeDaysSummary));
                            OnPropertyChanged(nameof(IncludeDaysSummary));
                        }
                    };

                    SetupDaysOfWeek();
                }
            }
        }
        private readonly Channel? _channel;

        private void SetupDaysOfWeek()
        {
            ExcludeDaysOfWeek = CreateDayCollectionFromList(_channel!.ExcludeDaysUtc);
            IncludeDaysOfWeek = CreateDayCollectionFromList(_channel!.IncludeDaysUtc);

            HookDayCollection(ExcludeDaysOfWeek, OnExcludeDaysChanged);
            HookDayCollection(IncludeDaysOfWeek, OnIncludeDaysChanged);
        }

        public MyObservableCollection<VideoViewModel> Videos { get; } = [];

        public ICommand ToggleEnableCountLimitCommand => _toggleEnableCountLimitCommand ??= new RelayCommand(() => EnableCountLimit = !EnableCountLimit);
        private RelayCommand? _toggleEnableCountLimitCommand;

        public ICommand ToggleEnableVideoLengthMinimumCommand => _toggleEnableVideoLengthMinimumCommand ??= new RelayCommand(() => Channel!.EnableVideoLengthMinimum = !Channel.EnableVideoLengthMinimum);
        private RelayCommand? _toggleEnableVideoLengthMinimumCommand;

        public ICommand ToggleEnableDateRangeLimitCommand => _toggleEnableDateRangeLimitCommand ??= new RelayCommand(() => Channel!.EnableDateRangeLimit = !Channel.EnableDateRangeLimit);
        private ICommand? _toggleEnableDateRangeLimitCommand;

        public ICommand ToggleSendNotificationsForNewVideosCommand => _toggleSendNotificationsForNewVideosCommand ??= new RelayCommand(() => Channel!.SendNotificationsForNewVideos = !Channel.SendNotificationsForNewVideos);
        private ICommand? _toggleSendNotificationsForNewVideosCommand;

        public ICommand ToggleAutoDownloadNewVideosCommand => _toggleAutoDownloadNewVideosCommand ??= new RelayCommand(() => Channel!.AutoDownloadNewVideos = !Channel.AutoDownloadNewVideos);
        private ICommand? _toggleAutoDownloadNewVideosCommand;

        public ICommand FindVideosCommand => _findVideosCommand ??= new RelayCommand(FindVideos);
        private ICommand? _findVideosCommand;

        public async void FindVideos()
        {
            if (_findInProgress)
            {
                return;
            }

            _findInProgress = true;
            IsRefreshing = false;

            await Policy
                .Handle<Exception>().FallbackAsync(_ => Task.CompletedTask, async ex =>
                    // This happens once we've retried and failed.
                    // Show that there was an unhandled error.
                    await MainThread.InvokeOnMainThreadAsync(async () => await Page.DisplayAlert(Resources.Resources.Error, string.Format(Resources.Resources.ErrorProcessingRequestMessage, ex.Message), Resources.Resources.OK)))
                .WrapAsync(Policy.Handle<Exception>().RetryAsync(5, (_, _) =>
                {
                    // Nothing to do
                }))
                .ExecuteAsync(async () => await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        using (new BusyIndicator(Page))
                        {
                            // Perform the lookup. Polly executes this on the main thread. Any exceptions will be handled, with a single retry.

                            Videos.Clear();

                            _hasSearchedAtLeastOnce = true;

                            // FindVideos
                            if (Page.AppShellViewModel.ChannelTabSelected)
                            {
                                List<string>? searchTerms = null;

                                if (!string.IsNullOrEmpty(SearchByTitleTerm))
                                {
                                    string searchByTitleTermTrimmed = SearchByTitleTerm.Trim();

                                    searchTerms = searchByTitleTermTrimmed.StartsWith('"')
                                        && searchByTitleTermTrimmed.EndsWith('"')
                                        && !string.IsNullOrEmpty(searchByTitleTermTrimmed.TrimStart('"').TrimEnd('"'))
                                        ? [searchByTitleTermTrimmed.TrimStart('"').TrimEnd('"')]
                                        : searchByTitleTermTrimmed.Split().ToList();

                                    // Update the history
                                    string? searchTermHistory = Preferences.Default.Get<string?>(nameof(SearchByTitleTerm), null);
                                    List<string> searchTermHistoryList;
                                    try
                                    {
                                        searchTermHistoryList = JsonConvert.DeserializeObject<List<string>>(searchTermHistory!) ?? [];
                                    }
                                    catch
                                    {
                                        searchTermHistoryList = [];
                                    }

                                    _ = searchTermHistoryList.RemoveAll(s => s.Equals(searchByTitleTermTrimmed, StringComparison.OrdinalIgnoreCase));
                                    searchTermHistoryList.Insert(0, searchByTitleTermTrimmed);
                                    searchTermHistoryList = searchTermHistoryList.Take(5).ToList();
                                    Preferences.Default.Set(nameof(SearchByTitleTerm), JsonConvert.SerializeObject(searchTermHistoryList));
                                }

                                List<Video> videos = await ServerApiClient.Instance.FindVideos(new FindVideosRequest
                                {
                                    Playlists = Channel!.RealPlaylistId.ToList(),
                                    ExclusionsMode = SelectedExclusionsMode.Value,
                                    ExclusionReasonFilter = SelectedExclusionFilter.Value,
                                    SortMode = SelectedSortMode.Value,
                                    SearchTerms = searchTerms,
                                    Count = EnableCountLimit && CountLimit.HasValue ? CountLimit.Value : int.MaxValue,
                                    DateRangeLimit = Channel?.EnableDateRangeLimit == true ? Channel.DateRangeLimit : null,
                                    VideoLengthMinimum = Channel?.EnableVideoLengthMinimum == true ? Channel.VideoLengthMinimum : null,
                                    ExcludeDaysUtc = Channel?.ExcludeDaysUtc,
                                    IncludeDaysUtc = Channel?.IncludeDaysUtc
                                });

                                List<VideoViewModel> videoViewModels = await Task.Run(() => videos.Select(v => new VideoViewModel(v, Page, this)).ToList());
                                Videos.AddRange(videoViewModels);

                                // Do not await this, as it slows the loading of the page
                                _ = QueueUtils.TryJoinDownloadGroup(videoViewModels);
                            }
                            else if (Page.AppShellViewModel.QueueTabSelected)
                            {
                                // Get the queue from the server
                                List<RequestData> distinctQueue = await ServerApiClient.Instance.GetQueue();

                                List<Video> queuedVideos = (await ServerApiClient.Instance.FindVideos(new FindVideosRequest
                                {
                                    ExclusionsMode = ExclusionsMode.ShowAll,
                                    VideoIds = distinctQueue.Select(queueItem => queueItem.VideoId).ToList(),
                                    Count = int.MaxValue
                                }))
                                    .OrderByDescending(video => distinctQueue.FirstOrDefault(v => v.VideoId == video.Id)?.DateAdded ?? DateTime.MinValue)
                                    .ToList();

                                foreach (Video? video in queuedVideos)
                                {
                                    VideoViewModel videoViewModel = new(video, Page, this);
                                    Videos.Add(videoViewModel);
                                    string requestId = distinctQueue.First(v => v.VideoId == video.Id).RequestGuid.ToString();

                                    // Do not await this, as it slows the loading of the queue page
                                    _ = ServerApiClient.Instance.JoinDownloadGroup(requestId, requestData => videoViewModel.UpdateCheck(requestId, requestData, showInAppNotifications: false));
                                }
                            }
                        }
                    }));

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
                _ = await Page.ShowPopupAsync(new FilterOptionsPopup(this));
            }
            finally
            {
                _isOptionsOpen = false;
            }
        }
        private static bool _isOptionsOpen;

        public IEnumerable<SortModeExtended> SortModeValues { get; } = Enum.GetValues(typeof(SortMode)).OfType<SortMode>().Select(m => new SortModeExtended(m)).ToList();

        public IEnumerable<ExclusionsModeExtended> ExclusionsModeValues { get; } = Enum.GetValues(typeof(ExclusionsMode)).OfType<ExclusionsMode>().Select(m => new ExclusionsModeExtended(m)).ToList();

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

        public ExclusionsModeExtended SelectedExclusionsMode => ExclusionsModeValues.ElementAt(SelectedExclusionsModeIndex);

        public int SelectedExclusionsModeIndex
        {
            get => _selectedExclusionsModeIndex;
            set
            {
                _selectedExclusionsModeIndex = value;

                // Have to do an explicit raise here since setting to 0 doesn't.
                OnPropertyChanged();
            }
        }
        private int _selectedExclusionsModeIndex = 1;

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
        private int _selectedExclusionFilterIndex = 0;

        public string? SearchByTitleTerm
        {
            get => _searchByTitleTerm;
            set => SetProperty(ref _searchByTitleTerm, value);
        }
        private string? _searchByTitleTerm;

        public bool EnableCountLimit
        {
            get => _enableCountLimit;
            set => SetProperty(ref _enableCountLimit, value);
        }
        private bool _enableCountLimit;

        public int? CountLimit
        {
            get => _countLimit;
            set => SetProperty(ref _countLimit, value);
        }
        private int? _countLimit;

        [Obsolete("This should ONLY be used for XAML binding")]
        public bool ShowExclusions => SelectedExclusionsMode.Value.HasFlag(ExclusionsMode.ShowExcluded);

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
                _ = SetProperty(ref _isFabOpen, value);
                _ = ChannelView!.ToggleFabExpander(_isFabOpen);
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

        public bool ShowExclusionReasonFilter => SelectedExclusionsMode.Value.HasFlag(ExclusionsMode.ShowExcluded) && Page.AppShellViewModel.ChannelTabSelected;

        public ICommand ShowSearchHistoryCommand => _showSearchHistoryCommand ??= new RelayCommand(async () =>
        {
            string? searchTermHistory = Preferences.Default.Get<string?>(nameof(SearchByTitleTerm), null);
            List<string> searchTermHistoryList;
            try
            {
                searchTermHistoryList = JsonConvert.DeserializeObject<List<string>>(searchTermHistory!) ?? [];
            }
            catch
            {
                searchTermHistoryList = [];
            }

            if (!searchTermHistoryList.Any())
            {
                await Snackbar.Make(
                    Resources.Resources.NoSearchHistoryFound,
                    duration: TimeSpan.FromSeconds(3),
                    action: null,
                    visualOptions: new SnackbarOptions
                    {
                        BackgroundColor = Colors.Black,
                        TextColor = Colors.White
                    }
                ).Show();

                return;
            }

            string? res = await Page.DisplayActionSheet(Channel?.VanityName, Resources.Resources.Cancel, Resources.Resources.Clear, searchTermHistoryList.ToArray());

            if (res == Resources.Resources.Clear)
            {
                Preferences.Default.Set<string?>(nameof(SearchByTitleTerm), null);
            }
            else if (res is not null && res != Resources.Resources.Cancel)
            {
                SearchByTitleTerm = res;
            }
        });
        private ICommand? _showSearchHistoryCommand;

        public ICommand ResetFiltersCommand => _resetFiltersCommand ??= new RelayCommand(() =>
        {
            SelectedSortModeIndex = 4;
            SelectedExclusionsModeIndex = 1;
            SearchByTitleTerm = null;
            EnableCountLimit = false;
        });
        private ICommand? _resetFiltersCommand;

        public string SearchOptionsSummary
        {
            get
            {
                List<string> parts =
                [
                    // Sort order
                    $"Sort: {SelectedSortMode.Description}"
                ];

                // Exclusions mode
                ExclusionsMode mode = SelectedExclusionsMode.Value;

                if (mode == ExclusionsMode.ShowAll)
                {
                    parts.Add($"Showing all videos (exclusions filtered by {SelectedExclusionFilter.Description})");
                }
                else if (mode == ExclusionsMode.ShowExcluded)
                {
                    parts.Add("Showing excluded videos");
                }
                else if (mode == ExclusionsMode.ShowNonExcluded)
                {
                    parts.Add($"Showing non-excluded videos ({SelectedExclusionFilter.Description})");
                }

                // Search term
                if (!string.IsNullOrWhiteSpace(SearchByTitleTerm))
                {
                    parts.Add($"Search term: \"{SearchByTitleTerm}\"");
                }

                // Max results
                if (EnableCountLimit && CountLimit.HasValue)
                {
                    parts.Add($"Max results: {CountLimit}");
                }
                else
                {
                    parts.Add("Max results: Unlimited");
                }

                // Date range
                if (Channel is { EnableDateRangeLimit: true, DateRangeLimit: { } })
                {
                    parts.Add($"Since: {Channel.DateRangeLimit.Value:yyyy-MM-dd}");
                }

                // Min length
                if (Channel is { EnableVideoLengthMinimum: true, VideoLengthMinimum: { } })
                {
                    TimeSpan length = Channel.VideoLengthMinimum.Value;
                    parts.Add($"Min length: {length.TotalSeconds}s");
                }

                // Days of week filters
                List<string> dayFilters = [];

                List<DayOfWeekItem> excludedDays = ExcludeDaysOfWeek!.Where(d => d.IsSelected).ToList();
                if (excludedDays.Count is > 0 and < 7)
                {
                    if (excludedDays.Count > 4)
                    {
                        IEnumerable<string> notExcluded = ExcludeDaysOfWeek!.Where(d => !d.IsSelected).Select(d => d.Day.ToString()[..3]);
                        dayFilters.Add($"Exclude all except {string.Join(", ", notExcluded)}");
                    }
                    else
                    {
                        IEnumerable<string> excluded = excludedDays.Select(d => d.Day.ToString()[..3]);
                        dayFilters.Add($"Exclude: {string.Join(", ", excluded)}");
                    }
                }

                List<DayOfWeekItem> includedDays = IncludeDaysOfWeek!.Where(d => d.IsSelected).ToList();
                if (includedDays.Count is > 0 and < 7)
                {
                    if (includedDays.Count > 4)
                    {
                        IEnumerable<string> notIncluded = IncludeDaysOfWeek!.Where(d => !d.IsSelected).Select(d => d.Day.ToString()[..3]);
                        dayFilters.Add($"Include all except {string.Join(", ", notIncluded)}");
                    }
                    else
                    {
                        IEnumerable<string> included = includedDays.Select(d => d.Day.ToString()[..3]);
                        dayFilters.Add($"Include: {string.Join(", ", included)}");
                    }
                }

                if (dayFilters.Count > 0)
                {
                    parts.Add(string.Join(", ", dayFilters));
                }

                return string.Join(Environment.NewLine, parts);
            }
        }

        #region Day of week stuff

        public ObservableCollection<DayOfWeekItem>? ExcludeDaysOfWeek { get; private set; }

        public ObservableCollection<DayOfWeekItem>? IncludeDaysOfWeek { get; private set; }

        public string ExcludeDaysSummary => BuildSummary(ExcludeDaysOfWeek!);

        public string IncludeDaysSummary => BuildSummary(IncludeDaysOfWeek!);

        private ObservableCollection<DayOfWeekItem> CreateDayCollectionFromList(List<DayOfWeek>? existing)
        {
            List<DayOfWeekItem> items = [];

            Array values = Enum.GetValues(typeof(DayOfWeek));
            foreach (object value in values)
            {
                DayOfWeek day = (DayOfWeek)value;
                bool isSelected = existing != null && existing.Contains(day);

                DayOfWeekItem item = new(day, isSelected);
                items.Add(item);
            }

            return new ObservableCollection<DayOfWeekItem>(items);
        }

        private void HookDayCollection(ObservableCollection<DayOfWeekItem> collection, EventHandler handler)
        {
            foreach (DayOfWeekItem item in collection)
            {
                item.SelectionChanged += handler;
            }
        }

        private void OnExcludeDaysChanged(object? sender, EventArgs e)
        {
            Channel!.ExcludeDaysUtc = BuildListFromCollection(ExcludeDaysOfWeek!);
            OnPropertyChanged(nameof(ExcludeDaysSummary));
        }

        private void OnIncludeDaysChanged(object? sender, EventArgs e)
        {
            Channel!.IncludeDaysUtc = BuildListFromCollection(IncludeDaysOfWeek!);
            OnPropertyChanged(nameof(IncludeDaysSummary));
        }

        private List<DayOfWeek> BuildListFromCollection(IEnumerable<DayOfWeekItem> collection) => collection.Where(i => i.IsSelected).Select(i => i.Day).ToList();

        private string BuildSummary(IEnumerable<DayOfWeekItem> collection)
        {
            List<DayOfWeek> selected = collection.Where(i => i.IsSelected).Select(i => i.Day).ToList();
            if (selected.Count == 0)
            {
                return "None";
            }

            if (selected.Count == 7)
            {
                return "All days";
            }
            // Use 3-letter abbreviations
            return string.Join(", ", selected.Select(d => d.ToString()[..3]));
        }
    }

    #endregion
}
