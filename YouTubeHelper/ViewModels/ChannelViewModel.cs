using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using Polly;
using ServerStatusBot.Definitions;
using ServerStatusBot.Definitions.Api;
using ServerStatusBot.Definitions.Database.Models;
using ServerStatusBot.Definitions.Models;
using YouTubeHelper.Properties;
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

            Videos.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(CountLabel));
            };

            Channel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(Channel.EnableDateRangeLimit)
                    or nameof(Channel.DateRangeLimit)
                    or nameof(Channel.EnableVideoLengthMinimum) or nameof(Channel.VideoLengthMinimum))
                {
                    OnPropertyChanged(nameof(SearchOptionsSummary));
                }
            };
        }

        public ICommand DeleteCommand => _deleteChannelCommand ??= new RelayCommand(Delete);
        private ICommand? _deleteChannelCommand;

        private async void Delete()
        {
            MainControlViewModel.SelectedChannel = MainControlViewModel.Channels[Math.Max(0, MainControlViewModel.Channels.IndexOf(this) - 1)];
            MainControlViewModel.Channels.Remove(this);
            Channel.MarkForDeletion = true;
            Channel.Persistent = false; // Stop doing updates!
            await ServerApiClient.Instance.UpdateChannel(Channel, MainWindow.ClientId);
        }

        public ICommand LookupChannelCommand => _searchCommand ??= new RelayCommand(LookupChannel);
        private ICommand? _searchCommand;

        private async void LookupChannel()
        {
            if (await ServerApiClient.Instance.PopulateChannel(Channel, MainWindow.ClientId))
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
                            if (MainControlViewModel.SearchByTitleTerm.StartsWith('"')
                                && MainControlViewModel.SearchByTitleTerm.EndsWith('"')
                                && !string.IsNullOrEmpty(MainControlViewModel.SearchByTitleTerm.TrimStart('"').TrimEnd('"')))
                            {
                                searchTerms = new List<string> { MainControlViewModel.SearchByTitleTerm.TrimStart('"').TrimEnd('"') };
                            }
                            else
                            {
                                searchTerms = MainControlViewModel.SearchByTitleTerm.Split().ToList();
                            }
                        }

                        List<Video> videos = await ServerApiClient.Instance.FindVideos(new FindVideosRequest
                        {
                            Channel = Channel,
                            ShowExclusions = MainControlViewModel.ShowExclusions,
                            ExclusionReasonFilter = MainControlViewModel.SelectedExclusionsMode.Value.HasFlag(ExclusionsMode.ShowNonExcluded) ? null : MainControlViewModel.SelectedExclusionFilter.Value,
                            SortMode = MainControlViewModel.SelectedSortMode.Value,
                            SearchTerms = searchTerms,
                            Count = MainControlViewModel is { EnableCountLimit: true, CountLimit: { } } ? MainControlViewModel.CountLimit.Value : int.MaxValue,
                            DateRangeLimit = Channel.EnableDateRangeLimit ? Channel.DateRangeLimit : null,
                            VideoLengthMinimum = Channel.EnableVideoLengthMinimum ? Channel.VideoLengthMinimum : null
                        });

                        List<VideoViewModel> videoViewModels = await Task.Run(() => videos.Select(v => new VideoViewModel(v, MainControlViewModel, this)).ToList());
                        Application.Current.Dispatcher.Invoke(() => { Videos.AddRange(videoViewModels); });

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
                        ShowExclusions = true,
                        VideoIds = distinctQueue.Select(queueItem => queueItem.VideoId).ToList(),
                        Count = int.MaxValue
                    }))
                    .OrderByDescending(video => distinctQueue.FirstOrDefault(v => v.VideoId == video.Id)?.DateAdded ?? DateTimeOffset.MinValue)
                    .ToList();

                foreach (Video video in queuedVideos)
                {
                    VideoViewModel videoViewModel = new VideoViewModel(video, MainControlViewModel, this);
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

        private async void MoveRight()
        {
            MainControlViewModel.AllowCreateNewChannel = false;
            int previousIndex = MainControlViewModel.Channels.IndexOf(this);
            MainControlViewModel.Channels.Remove(this);
            MainControlViewModel.Channels.Insert(Math.Min(MainControlViewModel.Channels.Count - 1, previousIndex + 1), this);
            MainControlViewModel.SelectedChannel = this;
            MainControlViewModel.AllowCreateNewChannel = true;

            // Fix up the "real" channels list
            MainControlViewModel.RealChannels.Clear();
            MainControlViewModel.RealChannels.AddRange(MainControlViewModel.Channels);

            foreach (ChannelViewModel c in MainControlViewModel.Channels.Where(c => c.Channel.Persistent).ToList())
            {
                c.Channel.Index = MainControlViewModel.Channels.IndexOf(c);
                await ServerApiClient.Instance.UpdateChannel(c.Channel, MainWindow.ClientId);
            }
        }

        public ICommand MoveLeftCommand => _moveLeftCommand ??= new RelayCommand(MoveLeft);
        private ICommand? _moveLeftCommand;

        private async void MoveLeft()
        {
            MainControlViewModel.AllowCreateNewChannel = false;
            int previousIndex = MainControlViewModel.Channels.IndexOf(this);
            MainControlViewModel.Channels.Remove(this);
            MainControlViewModel.Channels.Insert(Math.Max(0, previousIndex - 1), this);
            MainControlViewModel.SelectedChannel = this;
            MainControlViewModel.AllowCreateNewChannel = true;

            // Fix up the "real" channels list
            MainControlViewModel.RealChannels.Clear();
            MainControlViewModel.RealChannels.AddRange(MainControlViewModel.Channels);

            foreach (ChannelViewModel c in MainControlViewModel.Channels.Where(c => c.Channel.Persistent).ToList())
            {
                c.Channel.Index = MainControlViewModel.Channels.IndexOf(c);
                await ServerApiClient.Instance.UpdateChannel(c.Channel, MainWindow.ClientId);
            }
        }

        public string SearchGlyph
        {
            get => _searchGlyph;
            set => SetProperty(ref _searchGlyph, value);
        }
        private string _searchGlyph = Icons.Search;

        public MyObservableCollection<VideoViewModel> Videos { get; } = new();

        public bool ChannelMode => MainControlViewModel.Mode == MainControlMode.Channel;

        public bool QueueMode => MainControlViewModel.Mode == MainControlMode.Queue;

        public string CountLabel => string.Format(Resources.CountLabel, Videos.Count);

        public Channel Channel { get; }

        public MainControlViewModel MainControlViewModel { get; }

        public string SearchOptionsSummary
        {
            get
            {
                List<string> parts = new List<string>
                {
                    // Sort order
                    $"Sort: {MainControlViewModel.SelectedSortMode.Description}"
                };

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

                return string.Join(" | ", parts);
            }
        }
    }
}
