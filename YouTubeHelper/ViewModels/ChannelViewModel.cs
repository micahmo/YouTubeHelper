using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using Polly;
using ServerStatusBot.Definitions;
using ServerStatusBot.Definitions.Api;
using ServerStatusBot.Definitions.Database.Models;
using ServerStatusBot.Definitions.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;
using YouTubeHelper.Properties;
using YouTubeHelper.Shared.Mappers;
using YouTubeHelper.Shared.Utilities;
using YouTubeHelper.Utilities;

namespace YouTubeHelper.ViewModels
{
    public class ChannelViewModel : ObservableObject
    {
        public ChannelViewModel(Channel channel, MainControlViewModel mainControlViewModel)
        {
            Channel = channel;
            MainControlViewModel = mainControlViewModel;

            MainControlViewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MainControlViewModel.Mode))
                {
                    OnPropertyChanged(nameof(ChannelMode));
                    OnPropertyChanged(nameof(QueueMode));
                }

                if (args.PropertyName is nameof(MainControlViewModel.SelectedSortMode)
                    or nameof(MainControlViewModel.SelectedExclusionsMode)
                    or nameof(MainControlViewModel.SelectedExclusionFilter)
                    or nameof(MainControlViewModel.SearchByTitleTerm)
                    or nameof(MainControlViewModel.EnableCountLimit)
                    or nameof(MainControlViewModel.CountLimit))
                {
                    OnPropertyChanged(nameof(SearchOptionsSummary));
                }
            };

            Videos.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CountLabel));

            Channel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(Channel.EnableDateRangeLimit)
                    or nameof(Channel.DateRangeLimit)
                    or nameof(Channel.EnableVideoLengthMinimum)
                    or nameof(Channel.VideoLengthMinimum)
                    or nameof(Channel.ExcludeDaysUtc)
                    or nameof(Channel.IncludeDaysUtc))
                {
                    OnPropertyChanged(nameof(SearchOptionsSummary));
                }

                if (args.PropertyName is nameof(channel.ExcludeDaysUtc) or nameof(channel.IncludeDaysUtc))
                {
                    SetupDaysOfWeek();
                    OnPropertyChanged(nameof(ExcludeDaysOfWeek));
                    OnPropertyChanged(nameof(IncludeDaysOfWeek));
                    OnPropertyChanged(nameof(ExcludeDaysSummary));
                    OnPropertyChanged(nameof(IncludeDaysSummary));
                }

                if (args.PropertyName == nameof(channel.ThumbnailUrl))
                {
                    OnPropertyChanged(nameof(HasThumbnail));
                }
            };

            SetupDaysOfWeek();
        }

        private void SetupDaysOfWeek()
        {
            ExcludeDaysOfWeek = CreateDayCollectionFromList(Channel.ExcludeDaysUtc);
            IncludeDaysOfWeek = CreateDayCollectionFromList(Channel.IncludeDaysUtc);

            HookDayCollection(ExcludeDaysOfWeek, OnExcludeDaysChanged);
            HookDayCollection(IncludeDaysOfWeek, OnIncludeDaysChanged);
        }

        public ICommand DeleteCommand => _deleteChannelCommand ??= new RelayCommand(Delete);
        private ICommand? _deleteChannelCommand;

        private async void Delete()
        {
            MainControlViewModel.SelectedChannel = MainControlViewModel.Channels[Math.Max(0, MainControlViewModel.Channels.IndexOf(this) - 1)];
            _ = MainControlViewModel.Channels.Remove(this);
            Channel.MarkForDeletion = true;
            Channel.Persistent = false; // Stop doing updates!
            _ = await ServerApiClient.Instance.UpdateChannel(Channel, MainWindow.ClientId);
        }

        public ICommand LookupChannelCommand => _searchCommand ??= new RelayCommand(LookupChannel);
        private ICommand? _searchCommand;

        private async void LookupChannel()
        {
            if (await ServerApiClient.Instance.PopulateChannel(Channel, MainWindow.ClientId, persist: true))
            {
                SearchGlyph = Icons.Check;
                await Task.Delay(TimeSpan.FromSeconds(5));
                SearchGlyph = Icons.Search;
            }
            else
            {
                SearchGlyph = Icons.X;
                await Task.Delay(TimeSpan.FromSeconds(5));
                SearchGlyph = Icons.Search;
            }
        }

        public ICommand FindVideosCommand => _findVideosCommand ??= new RelayCommand(FindVideos);
        private ICommand? _findVideosCommand;

        private async void FindVideos()
        {
            MainControlViewModel.IsPlayerExpanded = false;

            Videos.Clear();

            await Policy
                .Handle<Exception>().RetryAsync(5, (ex, _) =>
                {
                    // Nothing to do
                })
                .ExecuteAsync(async () =>
                {
                    try
                    {
                        MainControlViewModel.IsBusy = true;

                        List<string>? searchTerms = null;

                        if (!string.IsNullOrEmpty(MainControlViewModel.SearchByTitleTerm))
                        {
                            searchTerms = MainControlViewModel.SearchByTitleTerm.StartsWith('"') && MainControlViewModel.SearchByTitleTerm.EndsWith('"') && !string.IsNullOrEmpty(MainControlViewModel.SearchByTitleTerm.TrimStart('"').TrimEnd('"'))
                                ? [MainControlViewModel.SearchByTitleTerm.TrimStart('"').TrimEnd('"')]
                                : MainControlViewModel.SearchByTitleTerm.Split().ToList();
                        }

                        List<Video> videos = await ServerApiClient.Instance.FindVideos(new FindVideosRequest
                        {
                            Playlists = Channel.RealPlaylistId.ToList(),
                            ExclusionsMode = MainControlViewModel.SelectedExclusionsMode.Value,
                            ExclusionReasonFilter = MainControlViewModel.SelectedExclusionFilter.Value,
                            SortMode = MainControlViewModel.SelectedSortMode.Value,
                            SearchTerms = searchTerms,
                            Count = MainControlViewModel is { EnableCountLimit: true, CountLimit: { } } ? MainControlViewModel.CountLimit.Value : int.MaxValue,
                            DateRangeLimit = Channel.EnableDateRangeLimit ? Channel.DateRangeLimit : null,
                            VideoLengthMinimum = Channel.EnableVideoLengthMinimum ? Channel.VideoLengthMinimum : null,
                            ExcludeDaysUtc = Channel.ExcludeDaysUtc,
                            IncludeDaysUtc = Channel.IncludeDaysUtc
                        });

                        List<VideoViewModel> videoViewModels = await Task.Run(() => videos.Select(v => new VideoViewModel(v, MainControlViewModel, this)).ToList());
                        Application.Current.Dispatcher.Invoke(() => Videos.AddRange(videoViewModels));

                        Task _ = QueueUtils.TryJoinDownloadGroup(videoViewModels);

                        MainControlViewModel.Progress = 0;
                        MainControlViewModel.ProgressState = TaskbarItemProgressState.Normal;
                    }
                    finally
                    {
                        MainControlViewModel.IsBusy = false;
                    }
                });
        }

        public ICommand LoadQueueCommand => _loadQueueCommand ??= new RelayCommand(LoadQueue);
        private ICommand? _loadQueueCommand;

        private async void LoadQueue()
        {
            Videos.Clear();

            try
            {
                MainControlViewModel.IsBusy = true;

                // Get the queue from the server
                List<RequestData> distinctQueue = await ServerApiClient.Instance.GetQueue();

                List<Video> queuedVideos = (await ServerApiClient.Instance.FindVideos(new FindVideosRequest
                {
                    ExclusionsMode = ExclusionsMode.ShowAll,
                    VideoIds = distinctQueue.Select(queueItem => queueItem.VideoId).ToList(),
                    Count = int.MaxValue
                }))
                    .OrderByDescending(video => distinctQueue.FirstOrDefault(v => v.VideoId == video.Id)?.DateAdded ?? DateTimeOffset.MinValue)
                    .ToList();

                foreach (Video video in queuedVideos)
                {
                    VideoViewModel videoViewModel = new(video, MainControlViewModel, this);
                    Videos.Add(videoViewModel);
                    string requestId = distinctQueue.First(v => v.VideoId == video.Id).RequestGuid.ToString();

                    // Do not await this, as it slows the loading of the queue page
                    Task _ = ServerApiClient.Instance.JoinDownloadGroup(requestId, requestData => videoViewModel.UpdateCheck(requestId, requestData, showInAppNotifications: false));
                }
            }
            finally
            {
                MainControlViewModel.IsBusy = false;
            }
        }

        public ICommand MoveRightCommand => _moveRightCommand ??= new RelayCommand(MoveRight);
        private ICommand? _moveRightCommand;

        private async void MoveRight() => await MoveRelativeAsync(+1);

        public ICommand MoveLeftCommand => _moveLeftCommand ??= new RelayCommand(MoveLeft);
        private ICommand? _moveLeftCommand;

        private async void MoveLeft() => await MoveRelativeAsync(-1);

        public string SearchGlyph
        {
            get => _searchGlyph;
            set => SetProperty(ref _searchGlyph, value);
        }
        private string _searchGlyph = Icons.Search;

        public string DeleteGlyph
        {
            get => _deleteGlyph;
            set => SetProperty(ref _deleteGlyph, value);
        }
        private string _deleteGlyph = Icons.Delete;

        public MyObservableCollection<VideoViewModel> Videos { get; } = [];

        public bool ChannelMode => MainControlViewModel.Mode == MainControlMode.Channel;

        public bool QueueMode => MainControlViewModel.Mode == MainControlMode.Queue;

        public string CountLabel => string.Format(Resources.CountLabel, Videos.Count);

        public Channel Channel { get; }

        public MainControlViewModel MainControlViewModel { get; }

        public string SearchOptionsSummary
        {
            get
            {
                List<string> parts =
                [
                    // Sort order
                    $"Sort: {MainControlViewModel.SelectedSortMode.Description}"
                ];

                // Exclusions mode
                ExclusionsMode mode = MainControlViewModel.SelectedExclusionsMode.Value;

                if (mode == ExclusionsMode.ShowAll)
                {
                    parts.Add($"Showing all videos (exclusions filtered by {MainControlViewModel.SelectedExclusionFilter.Description})");
                }
                else if (mode == ExclusionsMode.ShowExcluded)
                {
                    parts.Add($"Showing excluded videos ({MainControlViewModel.SelectedExclusionFilter.Description})");
                }
                else if (mode == ExclusionsMode.ShowNonExcluded)
                {
                    parts.Add("Showing non-excluded videos");
                }

                // Search term
                if (!string.IsNullOrWhiteSpace(MainControlViewModel.SearchByTitleTerm))
                {
                    parts.Add($"Search term: \"{MainControlViewModel.SearchByTitleTerm}\"");
                }

                // Max results
                if (MainControlViewModel.EnableCountLimit && MainControlViewModel.CountLimit.HasValue)
                {
                    parts.Add($"Max results: {MainControlViewModel.CountLimit}");
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

                return string.Join(" | ", parts);
            }
        }

        private async Task MoveRelativeAsync(int delta)
        {
            int currentIndex = MainControlViewModel.Channels.IndexOf(this);
            int targetIndex = currentIndex + delta;

            if (currentIndex < 0 || targetIndex < 0 || targetIndex >= MainControlViewModel.Channels.Count)
            {
                // Can't go anywhere
                return;
            }

            ChannelViewModel targetChannelViewModel = MainControlViewModel.Channels[targetIndex];

            if (!Channel.Persistent || !targetChannelViewModel.Channel.Persistent)
            {
                // Don't edit temp channels
                return;
            }

            // Persist-only: swap indices based on desired new tab order
            Channel.Index = targetIndex;
            targetChannelViewModel.Channel.Index = currentIndex;

            _ = await ServerApiClient.Instance.UpdateChannel(Channel, Guid.Empty.ToString());
            _ = await ServerApiClient.Instance.UpdateChannel(targetChannelViewModel.Channel, Guid.Empty.ToString());
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
            Channel.ExcludeDaysUtc = BuildListFromCollection(ExcludeDaysOfWeek!);
            OnPropertyChanged(nameof(ExcludeDaysSummary));
        }

        private void OnIncludeDaysChanged(object? sender, EventArgs e)
        {
            Channel.IncludeDaysUtc = BuildListFromCollection(IncludeDaysOfWeek!);
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

        #endregion

        public bool HasThumbnail => !string.IsNullOrWhiteSpace(Channel.ThumbnailUrl);
    }
}
