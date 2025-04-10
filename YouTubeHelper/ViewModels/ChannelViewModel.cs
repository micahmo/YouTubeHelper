using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDBHelpers;
using Polly;
using ServerStatusBot.Definitions.Api;
using ServerStatusBot.Definitions.Database;
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
                    OnPropertyChanged(nameof(SearchMode));
                    OnPropertyChanged(nameof(WatchMode));
                    OnPropertyChanged(nameof(ExclusionsMode));
                    OnPropertyChanged(nameof(QueueMode));
                }
            };

            Videos.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(CountLabel));
            };
        }

        public ICommand DeleteCommand => _deleteChannelCommand ??= new RelayCommand(Delete);
        private ICommand? _deleteChannelCommand;

        private async void Delete()
        {
            MainControlViewModel.SelectedChannel = MainControlViewModel.Channels[Math.Max(0, MainControlViewModel.Channels.IndexOf(this) - 1)];
            MainControlViewModel.Channels.Remove(this);
            await Collections.ChannelCollection.DeleteAsync(Channel.Id);
        }

        public ICommand LookupChannelCommand => _searchCommand ??= new RelayCommand(LookupChannel);
        private ICommand? _searchCommand;

        private async void LookupChannel()
        {
            if (await YouTubeApi.Instance.PopulateChannel(Channel))
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
            bool noLimit = (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                           && !Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl);

            MainControlViewModel.IsPlayerExpanded = false;

            Videos.Clear();

            await Policy
                .Handle<Exception>().RetryAsync(5, (ex, _) =>
                {
                    // This retries a few times and lets us reset things before we try again.
                    if (ex is MongoConnectionPoolPausedException)
                    {
                        DatabaseEngine.Reset();
                    }

                    return Task.CompletedTask;
                })
                .ExecuteAsync(async () =>
                {
                    try
                    {
                        MainControlViewModel.IsBusy = true;

                        List<Video> exclusions = await Collections.ExcludedVideosCollection.FindByConditionAsync(v => v.ChannelPlaylist == Channel.ChannelPlaylist);
                        List<string>? searchTerms = null;

                        if (MainControlViewModel.Mode == MainControlMode.Search && !string.IsNullOrEmpty(MainControlViewModel.LookupSearchTerm))
                        {
                            var videos = await YouTubeApi.Instance.SearchVideos(Channel, exclusions, MainControlViewModel.ShowExcludedVideos, MainControlViewModel.SelectedSortMode.Value, MainControlViewModel.LookupSearchTerm, noLimit ? int.MaxValue : 10);
                            var videoViewModels = await Task.Run(() => videos.Select(v => new VideoViewModel(v, MainControlViewModel, this)).ToList());
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Videos.AddRange(videoViewModels);
                            });
                        }
                        else
                        {
                            if (MainControlViewModel.Mode == MainControlMode.Search)
                            {
                                if (!string.IsNullOrEmpty(MainControlViewModel.ExactSearchTerm))
                                {
                                    if (MainControlViewModel.ExactSearchTerm.StartsWith('"')
                                        && MainControlViewModel.ExactSearchTerm.EndsWith('"')
                                        && !string.IsNullOrEmpty(MainControlViewModel.ExactSearchTerm.TrimStart('"').TrimEnd('"')))
                                    {
                                        searchTerms = new List<string> { MainControlViewModel.ExactSearchTerm.TrimStart('"').TrimEnd('"') };
                                    }
                                    else
                                    {
                                        searchTerms = MainControlViewModel.ExactSearchTerm.Split().ToList();
                                    }
                                }
                            }

                            IEnumerable<Video> videos = await YouTubeApi.Instance.FindVideos(
                                Channel, exclusions,
                                MainControlViewModel.ShowExcludedVideos,
                                MainControlViewModel.SelectedSortMode.Value,
                                searchTerms,
                                (progress, indeterminate) =>
                                {
                                    MainControlViewModel.Progress = progress;
                                    MainControlViewModel.ProgressState = indeterminate ? TaskbarItemProgressState.Indeterminate : TaskbarItemProgressState.Normal;
                                },
                                noLimit ? int.MaxValue : 10,
                                MainControlViewModel.Mode == MainControlMode.Watch && Channel.EnableDateRangeLimit ? Channel.DateRangeLimit : null,
                                MainControlViewModel.Mode == MainControlMode.Watch && Channel.EnableVideoLengthMinimum ? Channel.VideoLengthMinimum : null);

                            List<VideoViewModel> videoViewModels = await Task.Run(() => videos.Select(v => new VideoViewModel(v, MainControlViewModel, this)).ToList());
                            Application.Current.Dispatcher.Invoke(() => { Videos.AddRange(videoViewModels); });

                            try
                            {
                                List<RequestData> distinctQueue = await ServerApiClient.Instance.GetQueue();
                                videoViewModels.ForEach(videoViewModel =>
                                {
                                    Guid? requestId = distinctQueue.FirstOrDefault(v => v.VideoId! == videoViewModel.Video.Id)?.RequestGuid;
                                    if (requestId != null)
                                    {
                                        videoViewModel.StartUpdateCheck(requestId!.ToString()!, showInAppNotifications: false);
                                    }
                                });
                            }
                            catch
                            {
                                // Ignore this, because getting the queue isn't a big deal, and we don't want it to trip the outer retry.
                            }

                            MainControlViewModel.Progress = 0;
                            MainControlViewModel.ProgressState = TaskbarItemProgressState.Normal;
                        }
                    }
                    finally
                    {
                        MainControlViewModel.IsBusy = false;
                    }
                });
        }

        public ICommand FindExclusionsCommand => _findExclusionsCommand ??= new RelayCommand(FindExclusions);
        private ICommand? _findExclusionsCommand;

        private async void FindExclusions()
        {
            Videos.Clear();

            await Policy
                .Handle<Exception>().RetryAsync(5, (ex, _) =>
                {
                    // This retries a few times and lets us reset things before we try again.
                    if (ex is MongoConnectionPoolPausedException)
                    {
                        DatabaseEngine.Reset();
                    }

                    return Task.CompletedTask;
                })
                .ExecuteAsync(async () =>
                {
                    try
                    {
                        MainControlViewModel.IsBusy = true;
                        List<Video> exclusions = await Collections.ExcludedVideosCollection.FindByConditionAsync(v => v.ChannelPlaylist == Channel.ChannelPlaylist);

                        if (MainControlViewModel.SelectedExclusionFilter.Value != ExclusionReason.None)
                        {
                            exclusions = exclusions.Where(v => MainControlViewModel.SelectedExclusionFilter.Value.HasFlag(v.ExclusionReason)).ToList();
                        }

                        IEnumerable<Video> videos = await YouTubeApi.Instance.FindVideoDetails(exclusions.Select(v => v.Id).ToList(), exclusions, Channel, MainControlViewModel.SelectedSortMode.Value, count: int.MaxValue);
                        List<VideoViewModel> videoViewModels = await Task.Run(() => videos.Select(v => new VideoViewModel(v, MainControlViewModel, this)).ToList());
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Videos.AddRange(videoViewModels);
                        });
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

                // Get all excluded videos
                List<Video> excludedVideos = await Collections.ExcludedVideosCollection.FindAllAsync();

                List<Video> queuedVideos = (await YouTubeApi.Instance.FindVideoDetails(
                    distinctQueue.Select(queueItem => queueItem.VideoId!).ToList(),
                    excludedVideos: excludedVideos,
                    customSort: videos => videos.OrderByDescending(video => distinctQueue.FirstOrDefault(v => v.VideoId! == video.Id)?.DateAdded ?? DateTimeOffset.MinValue).ToList(),
                    count: int.MaxValue
                )).ToList();

                queuedVideos.ForEach(video =>
                {
                    VideoViewModel videoViewModel = new VideoViewModel(video, MainControlViewModel, this);
                    Videos.Add(videoViewModel);
                    videoViewModel.StartUpdateCheck(distinctQueue.First(v => v.VideoId! == video.Id).RequestGuid.ToString(), showInAppNotifications: false);
                });
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

            foreach (ChannelViewModel c in MainControlViewModel.Channels.ToList())
            {
                c.Channel.Index = MainControlViewModel.Channels.IndexOf(c);
                await Collections.ChannelCollection.UpdateAsync<Channel, ObjectId>(c.Channel);
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

            foreach (ChannelViewModel c in MainControlViewModel.Channels.ToList())
            {
                c.Channel.Index = MainControlViewModel.Channels.IndexOf(c);
                await Collections.ChannelCollection.UpdateAsync<Channel, ObjectId>(c.Channel);
            }
        }

        public string SearchGlyph
        {
            get => _searchGlyph;
            set => SetProperty(ref _searchGlyph, value);
        }
        private string _searchGlyph = Icons.Search;

        public MyObservableCollection<VideoViewModel> Videos { get; } = new();

        public bool WatchMode => MainControlViewModel.Mode == MainControlMode.Watch;

        public bool SearchMode => MainControlViewModel.Mode == MainControlMode.Search;

        public bool ExclusionsMode => MainControlViewModel.Mode == MainControlMode.Exclusions;

        public bool QueueMode => MainControlViewModel.Mode == MainControlMode.Queue;

        public string CountLabel => string.Format(Resources.CountLabel, Videos.Count);

        public Channel Channel { get; }

        public MainControlViewModel MainControlViewModel { get; }
    }
}
