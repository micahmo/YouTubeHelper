using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using ServerStatusBot.Definitions;
using ServerStatusBot.Definitions.Api;
using ServerStatusBot.Definitions.Database.Models;
using YouTubeHelper.Models;
using YouTubeHelper.Properties;

namespace YouTubeHelper.ViewModels
{
    public class MainControlViewModel : ObservableObject
    {
        public MainControlViewModel()
        {
            PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(SelectedChannel) && SelectedChannel == _newChannelTab)
                {
                    if (AllowCreateNewChannel)
                    {
                        Dispatcher.CurrentDispatcher.BeginInvoke(async () =>
                        {
                            Channel channel = new Channel { VanityName = Resources.NewChannel, Index = Channels.Where(c => c != _newChannelTab).MaxBy(c => c.Channel.Index)?.Channel.Index + 1 ?? 0 };
                            ChannelViewModel channelViewModel = new(channel, this);
                            Channels.Insert(Channels.Count - 1, channelViewModel);
                            SelectedChannel = channelViewModel;

                            // Persist it!
                            await ServerApiClient.Instance.UpdateChannel(channel, MainWindow.ClientId);

                            // Listen for changes
                            channel.Changed += async (_, _) =>
                            {
                                await ServerApiClient.Instance.UpdateChannel(channel, MainWindow.ClientId);
                            };
                        });
                    }
                }

                if (args.PropertyName == nameof(Mode))
                {
                    ChannelViewModel? previouslySelectedChannel = SelectedChannel;
                    Channels.Clear();
                    RealChannels.ForEach(Channels.Add);
                    SelectedChannel = previouslySelectedChannel == _queueChannelTab ? RealChannels.FirstOrDefault() : previouslySelectedChannel ?? RealChannels.FirstOrDefault();

                    switch (Mode)
                    {
                        case MainControlMode.Channel:
                            Channels.ToList().ForEach(c =>
                            {
                                c.Videos.Clear();
                            });
                            break;
                        case MainControlMode.Queue:
                            Channels.Clear();
                            Channels.Add(_queueChannelTab!);
                            SelectedChannel = _queueChannelTab;
                            break;
                        default:
                            break;
                    }
                }

                if (args.PropertyName == nameof(CumulativeDownloadProgress))
                {
                    IEnumerable<VideoViewModel> videosBeingDownloaded = Channels.SelectMany(c => c.Videos.Where(v => !string.IsNullOrEmpty(v.Video.Status) && !v.Video.Excluded)).ToList();
                    int totalProgress = 100 * videosBeingDownloaded.Count();
                    double currentProgress = videosBeingDownloaded.Where(v => v.Video.Progress is not null).Select(v => v.Video.Progress!.Value).Sum();
                    double? cumulativeProgress = currentProgress / totalProgress;

                    Progress = (float)cumulativeProgress;
                    ProgressState = TaskbarItemProgressState.Normal;
                }
            };

            _newChannelTab = new(new Channel(persistent: false) { VanityName = "+" }, this);
            _queueChannelTab = new(new Channel(persistent: false) { VanityName = Resources.Queue }, this);
        }

        public void RaisePropertyChanged(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }

        public MainControlMode Mode
        {
            get => _mode;
            set
            {
                SetProperty(ref _mode, value);
                OnPropertyChanged(nameof(ChannelMode));
                OnPropertyChanged(nameof(QueueMode));
            }
        }
        private MainControlMode _mode;

        public bool ChannelMode => Mode == MainControlMode.Channel;

        public bool QueueMode => Mode == MainControlMode.Queue;

        public bool ShowExclusions => SelectedExclusionsMode.Value.HasFlag(ExclusionsMode.ShowExcluded);

        public ObservableCollection<ChannelViewModel> Channels { get; } = new();

        /// <summary>
        /// Represents the "real" list of channels from the database. Can be used to repopulate <see cref="Channels"/> when needed.
        /// </summary>
        public List<ChannelViewModel> RealChannels { get; } = new();

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }
        private bool _isBusy = true;

        public string CurrentDownloadDirectoryLabel => string.Format(Resources.CurrentDownloadDirectory, Settings.Instance!.DownloadDirectory);

        public string ActiveDownloadsCountLabel => string.Format(Resources.ActiveDownloads, Channels.Select(c => c.Videos.Count(v => !string.IsNullOrEmpty(v.Video.Status) && !v.Video.Excluded)).Sum());

        /// <summary>
        /// This object exists purely for a RaisePropertyChanged signal and should never be accessed directly.
        /// </summary>
        public object? CumulativeDownloadProgress;

        public ChannelViewModel? SelectedChannel
        {
            get => _selectedChannel;
            set => SetProperty(ref _selectedChannel, value);
        }
        private ChannelViewModel? _selectedChannel;

        private readonly ChannelViewModel _newChannelTab;
        private readonly ChannelViewModel _queueChannelTab;

        #region Active video

        public bool IsMainControlExpanded
        {
            get => _isMainControlExpanded;
            set => SetProperty(ref _isMainControlExpanded, value);
        }
        private bool _isMainControlExpanded = true;

        public bool IsPlayerExpanded
        {
            get => _isPlayerExpanded;
            set => SetProperty(ref _isPlayerExpanded, value);
        }
        private bool _isPlayerExpanded;

        public GridLength TwoStarGridLength { get; } = new(2, GridUnitType.Star);

        public GridLength OneStarGridLength { get; } = new(1, GridUnitType.Star);

        public GridLength FifteenPixelGridLength { get; } = new(15, GridUnitType.Pixel);

        public GridLength ZeroGridLength { get; } = new(0, GridUnitType.Pixel);


        public string? ActiveVideo
        {
            get => _activeVideo;
            set => SetProperty(ref _activeVideo, value);
        }
        private string? _activeVideo;

        public string? ActiveVideoTitle
        {
            get => _activeVideoTitle;
            set => SetProperty(ref _activeVideoTitle, value);
        }
        private string? _activeVideoTitle;

        // This property never has a value, but it can be used to signal the view to resume playback without changing the ActiveVideo
        public object? SignalPlayVideo { get; set; }

        // This property never has a value, but it can be used to signal the view to resume playback without changing the ActiveVideo
        public object? SignalPauseVideo { get; set; }

        public TimeSpan ActiveVideoElapsedTimeSpan
        {
            get => _activeVideoElapsedTimeSpan;
            set
            {
                SetProperty(ref _activeVideoElapsedTimeSpan, value);
                OnPropertyChanged(nameof(ActiveVideoRemainingTimeSpan));
                OnPropertyChanged(nameof(ActiveVideoTimeString));
            }
        }
        private TimeSpan _activeVideoElapsedTimeSpan;

        public TimeSpan ActiveVideoDuration
        {
            get => _activeVideoDuration;
            set
            {
                SetProperty(ref _activeVideoDuration, value);
                OnPropertyChanged(nameof(ActiveVideoRemainingTimeSpan));
                OnPropertyChanged(nameof(ActiveVideoTimeString));
            }
        }
        private TimeSpan _activeVideoDuration;

        public TimeSpan ActiveVideoRemainingTimeSpan => ActiveVideoDuration - ActiveVideoElapsedTimeSpan;

        public string ActiveVideoTimeString => $"{(int)ActiveVideoElapsedTimeSpan.TotalHours:D2}:{ActiveVideoElapsedTimeSpan.Minutes:D2}:{ActiveVideoElapsedTimeSpan.Seconds:D2}  /  " +
                                               $"{(int)ActiveVideoDuration.TotalHours:D2}:{ActiveVideoDuration.Minutes:D2}:{ActiveVideoDuration.Seconds:D2}  /  " +
                                               $"{(int)ActiveVideoRemainingTimeSpan.TotalHours:D2}:{ActiveVideoRemainingTimeSpan.Minutes:D2}:{ActiveVideoRemainingTimeSpan.Seconds:D2}";

        public float Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }
        private float _progress;

        public TaskbarItemProgressState ProgressState
        {
            get => _progressState;
            set => SetProperty(ref _progressState, value);
        }
        private TaskbarItemProgressState _progressState;

        #endregion

        #region Sorting and filtering

        public IEnumerable<SortModeExtended> SortModeValues { get; } = Enum.GetValues(typeof(SortMode)).OfType<SortMode>().Select(m => new SortModeExtended(m)).ToList();

        public SortModeExtended SelectedSortMode
        {
            get => _selectedSortMode ?? SortModeValues.First();
            set => SetProperty(ref _selectedSortMode, value);
        }
        private SortModeExtended? _selectedSortMode;

        public int SelectedSortModeIndex
        {
            get => _selectedSortModeIndex;
            set => SetProperty(ref _selectedSortModeIndex, value);
        }
        private int _selectedSortModeIndex = 4;

        public IEnumerable<ExclusionsModeExtended> ExclusionsModeValues { get; } = Enum.GetValues(typeof(ExclusionsMode)).OfType<ExclusionsMode>().Select(m => new ExclusionsModeExtended(m)).ToList();

        public int SelectedExclusionsModeIndex
        {
            get => _selectedExclusionsModeIndex;
            set => SetProperty(ref _selectedExclusionsModeIndex, value);
        }
        private int _selectedExclusionsModeIndex = 2;

        public ExclusionsModeExtended SelectedExclusionsMode
        {
            get => _selectedExclusionsMode ?? ExclusionsModeValues.First();
            set
            {
                SetProperty(ref _selectedExclusionsMode, value);
                OnPropertyChanged(nameof(ShowExclusions));
            }
        }
        private ExclusionsModeExtended? _selectedExclusionsMode;

        public IEnumerable<ExclusionReasonExtended> ExclusionReasonValues { get; } = Enum.GetValues(typeof(ExclusionReason)).OfType<ExclusionReason>().Select(m => new ExclusionReasonExtended(m)).ToList();

        public ExclusionReasonExtended SelectedExclusionFilter
        {
            get => _exclusionFilter ?? ExclusionReasonValues.First();
            set => SetProperty(ref _exclusionFilter, value);
        }
        private ExclusionReasonExtended? _exclusionFilter;

        public int SelectedExclusionFilterIndex
        {
            get => _selectedExclusionFilterIndex;
            set => SetProperty(ref _selectedExclusionFilterIndex, value);
        }
        private int _selectedExclusionFilterIndex = 4;

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

        #endregion

        public bool AllowCreateNewChannel { get; set; } = true;

        public async Task Load()
        {
            if (_loaded)
            {
                return;
            }

            IsBusy = true;

            List<Channel>? channels = null;

            // Load channels in the background
            await Task.Run(() =>
            {
                channels = ServerApiClient.Instance.GetChannels().GetAwaiter().GetResult();

                channels.ToList().ForEach(c =>
                {
                    c.Changed += async (_, _) =>
                    {
                        await ServerApiClient.Instance.UpdateChannel(c, MainWindow.ClientId);
                    };
                });
            });

            // Populate the UI with the channels we retrieved
            foreach (Channel c in channels ?? Enumerable.Empty<Channel>())
            {
                Channels.Add(new ChannelViewModel(c, this));
                await Task.Yield(); // allow UI to breathe
            }

            Channels.Add(_newChannelTab);

            try
            {
                SelectedSortModeIndex = SortModeValues.ToList().IndexOf(SortModeValues.First(s => s.Value == ApplicationSettings.Instance.SelectedSortMode));
                SelectedExclusionsModeIndex = ExclusionsModeValues.ToList().IndexOf(ExclusionsModeValues.First(m => m.Value == ApplicationSettings.Instance.SelectedExclusionsMode));
                SelectedExclusionFilterIndex = ExclusionReasonValues.ToList().IndexOf(ExclusionReasonValues.First(s => s.Value == ApplicationSettings.Instance.SelectedExclusionReason));
                if (Channels[ApplicationSettings.Instance.SelectedTabIndex] != _newChannelTab)
                {
                    SelectedChannel = Channels[ApplicationSettings.Instance.SelectedTabIndex];
                }
            }
            catch
            {
                // ignored
            }

            RealChannels.Clear();
            RealChannels.AddRange(Channels);

            IsBusy = false;

            _loaded = true;
        }
        
        // Helps to prevent double-loading when using the app through RDP sessions.
        private bool _loaded;
    }

    public enum MainControlMode
    {
        Channel,
        Queue
    }
}
