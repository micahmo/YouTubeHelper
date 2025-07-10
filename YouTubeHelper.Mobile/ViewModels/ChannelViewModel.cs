using CommunityToolkit.Maui.Views;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using Newtonsoft.Json;
using Polly;
using ServerStatusBot.Definitions;
using ServerStatusBot.Definitions.Api;
using ServerStatusBot.Definitions.Database.Models;
using ServerStatusBot.Definitions.Models;
using System.Windows.Input;
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

                if (args.PropertyName is nameof(SelectedExclusionsModeIndex) or nameof(SelectedSortModeIndex) or nameof(SelectedExclusionFilterIndex) or nameof(SearchByTitleTerm) or nameof(EnableCountLimit) or nameof(CountLimit))
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
                            c.OnPropertyChanged(nameof(ShowExclusions));
                            OnPropertyChanged(nameof(ShowExclusions));
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

        public ICommand ToggleEnableDateRangeLimitCommand => _toggleEnableDateRangeLimitCommand ??= new RelayCommand(() =>
        {
            Channel!.EnableDateRangeLimit = !Channel.EnableDateRangeLimit;
        });
        private ICommand? _toggleEnableDateRangeLimitCommand;

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
                            if (Page.AppShellViewModel.ChannelTabSelected)
                            {
                                List<string>? searchTerms = null;


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
                                    ShowExclusions = ShowExclusions,
                                    ExclusionReasonFilter = SelectedExclusionsMode.Value.HasFlag(ExclusionsMode.ShowNonExcluded) ? null : SelectedExclusionFilter.Value,
                                    SortMode = SelectedSortMode.Value,
                                    SearchTerms = searchTerms,
                                    Count = EnableCountLimit && CountLimit.HasValue ? CountLimit.Value : int.MaxValue,
                                    DateRangeLimit = Channel?.EnableDateRangeLimit == true ? Channel.DateRangeLimit : null,
                                    VideoLengthMinimum = Channel?.EnableVideoLengthMinimum == true ? Channel.VideoLengthMinimum : null
                                });

                                List<VideoViewModel> videoViewModels = await Task.Run(() => videos.Select(v => new VideoViewModel(v, Page, this)).ToList());
                                Videos.AddRange(videoViewModels);

                                await QueueUtils.TryJoinDownloadGroup(videoViewModels);
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
                Popup popup = new Popup();

                VerticalStackLayout contentLayout = new VerticalStackLayout
                {
                    Padding = 20,
                    BackgroundColor = Colors.White
                };

                Label channelNameLabel = new Label
                {
                    Text = Channel?.VanityName,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 16
                };

                BoxView spacer0 = new BoxView
                {
                    HeightRequest = 20,
                    Color = Colors.Transparent
                };

                Label searchOptionsSummaryLabel = new Label
                {
                    Text = SearchOptionsSummary,
                    TextColor = Colors.Gray,
                };

                // Search term
                Entry searchTermEntry = new Entry
                {
                    Margin = new Thickness(-5, 0, 0, 0),
                    Placeholder = "Search by title",
                    Text = SearchByTitleTerm,
                };
                searchTermEntry.TextChanged += (_, _) =>
                {
                    SearchByTitleTerm = searchTermEntry.Text;
                    searchOptionsSummaryLabel.Text = SearchOptionsSummary;
                };

                // Max results
                Entry maxResultsEntry = new Entry
                {
                    HorizontalOptions = LayoutOptions.End,
                    Text = CountLimit?.ToString(),
                    IsEnabled = EnableCountLimit
                };
                maxResultsEntry.TextChanged += (_, _) =>
                {
                    if (int.TryParse(maxResultsEntry.Text, out int maxResults))
                    {
                        CountLimit = maxResults;
                    }
                    else
                    {
                        maxResultsEntry.Text = null;
                        CountLimit = null;
                    }

                    searchOptionsSummaryLabel.Text = SearchOptionsSummary;
                };

                CheckBox enableMaxResultsCheckBox = new CheckBox
                {
                    Margin = new Thickness(-10, 0, 0, 0),
                    IsChecked = EnableCountLimit
                };
                enableMaxResultsCheckBox.CheckedChanged += (_, _) =>
                {
                    maxResultsEntry.IsEnabled = EnableCountLimit = enableMaxResultsCheckBox.IsChecked;
                    searchOptionsSummaryLabel.Text = SearchOptionsSummary;
                };

                Label enableMaxResultsLabel = new Label
                {
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalOptions = LayoutOptions.Center,
                    Text = "Maximum number of results"
                };

                BoxView spacer1 = new BoxView
                {
                    HeightRequest = 20,
                    Color = Colors.Transparent
                };

                HorizontalStackLayout maxResultsOptionsLayout = new HorizontalStackLayout
                {
                    Children =
                    {
                        enableMaxResultsCheckBox,
                        enableMaxResultsLabel,
                        maxResultsEntry
                    },
                    VerticalOptions = LayoutOptions.Center
                };

                // Date range
                DatePicker dateRangeLimitDatePicker = new DatePicker
                {
                    HorizontalOptions = LayoutOptions.End,
                    Date = Channel!.DateRangeLimit ?? DateTime.Now.Date,
                    IsEnabled = Channel!.EnableDateRangeLimit
                };
                dateRangeLimitDatePicker.DateSelected += (_, _) =>
                {
                    Channel.DateRangeLimit = dateRangeLimitDatePicker.Date;
                    searchOptionsSummaryLabel.Text = SearchOptionsSummary;
                };

                CheckBox enableDateRangeLimitCheckBox = new CheckBox
                {
                    Margin = new Thickness(-10, 0, 0, 0),
                    IsChecked = Channel!.EnableDateRangeLimit
                };
                enableDateRangeLimitCheckBox.CheckedChanged += (_, _) =>
                {
                    dateRangeLimitDatePicker.IsEnabled = Channel.EnableDateRangeLimit = enableDateRangeLimitCheckBox.IsChecked;
                    searchOptionsSummaryLabel.Text = SearchOptionsSummary;
                };

                Label enableDataRangeLimitLabel = new Label
                {
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalOptions = LayoutOptions.Center,
                    Text = "Do not show videos before"
                };

                BoxView spacer2 = new BoxView
                {
                    HeightRequest = 20,
                    Color = Colors.Transparent
                };

                HorizontalStackLayout dateRangeOptionsLayout = new HorizontalStackLayout
                {
                    Children =
                    {
                        enableDateRangeLimitCheckBox,
                        enableDataRangeLimitLabel,
                        dateRangeLimitDatePicker
                    },
                    VerticalOptions = LayoutOptions.Center
                };

                // Time limit
                Entry videoLengthMinimumEntry = new Entry
                {
                    HorizontalOptions = LayoutOptions.End,
                    Text = Channel?.VideoLengthMinimumInSeconds?.ToString(),
                    IsEnabled = Channel!.EnableVideoLengthMinimum
                };
                videoLengthMinimumEntry.TextChanged += (_, _) =>
                {
                    if (int.TryParse(videoLengthMinimumEntry.Text, out int videoLengthMinimum))
                    {
                        Channel.VideoLengthMinimum = TimeSpan.FromSeconds(videoLengthMinimum);
                    }
                    else
                    {
                        videoLengthMinimumEntry.Text = null;
                        Channel.VideoLengthMinimum = null;
                    }

                    searchOptionsSummaryLabel.Text = SearchOptionsSummary;
                };

                CheckBox enableVideoLengthMinimumCheckBox = new CheckBox
                {
                    Margin = new Thickness(-10, 0, 0, 0),
                    IsChecked = Channel!.EnableVideoLengthMinimum
                };
                enableVideoLengthMinimumCheckBox.CheckedChanged += (_, _) =>
                {
                    videoLengthMinimumEntry.IsEnabled = Channel.EnableVideoLengthMinimum = enableVideoLengthMinimumCheckBox.IsChecked;
                    searchOptionsSummaryLabel.Text = SearchOptionsSummary;
                };

                Label enableVideoLengthMinimumLabel = new Label
                {
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalOptions = LayoutOptions.Center,
                    Text = "Do not show videos shorter than"
                };

                BoxView spacer3 = new BoxView
                {
                    HeightRequest = 20,
                    Color = Colors.Transparent
                };

                HorizontalStackLayout videoLengthMinimumOptionsLayout = new HorizontalStackLayout
                {
                    Children =
                    {
                        enableVideoLengthMinimumCheckBox,
                        enableVideoLengthMinimumLabel,
                        videoLengthMinimumEntry
                    },
                    VerticalOptions = LayoutOptions.Center
                };

                BoxView spacer4 = new BoxView
                {
                    HeightRequest = 20,
                    Color = Colors.Transparent
                };

                contentLayout.Add(channelNameLabel);
                contentLayout.Add(spacer0);
                contentLayout.Add(searchTermEntry);
                contentLayout.Add(spacer1);
                contentLayout.Add(maxResultsOptionsLayout);
                contentLayout.Add(spacer2);
                contentLayout.Add(dateRangeOptionsLayout);
                contentLayout.Add(spacer3);
                contentLayout.Add(videoLengthMinimumOptionsLayout);
                contentLayout.Add(spacer4);
                contentLayout.Add(searchOptionsSummaryLabel);

                popup.Content = contentLayout;

                await Page.ShowPopupAsync(popup);
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

        public string SearchOptionsSummary
        {
            get
            {
                List<string> parts = new List<string>
                {
                    // Sort order
                    $"Sort: {SelectedSortMode.Description}"
                };

                // Exclusions mode
                ExclusionsMode mode = SelectedExclusionsMode.Value;

                if (mode == ExclusionsMode.ShowAll)
                {
                    parts.Add("Showing: All videos");
                }
                else if (mode == ExclusionsMode.ShowExcluded)
                {
                    parts.Add($"Showing: Excluded videos");
                }
                else if (mode == ExclusionsMode.ShowNonExcluded)
                {
                    parts.Add("Showing: Non-excluded videos");
                }

                if (ShowExclusions)
                {
                    parts.Add($"Filtering exclusions: \"{SelectedExclusionFilter.Description}\"");
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

                return string.Join(Environment.NewLine, parts);
            }
        }
    }
}
