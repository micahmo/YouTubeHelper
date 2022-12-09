using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using YouTubeHelper.Models;
using YouTubeHelper.Properties;
using YouTubeHelper.Shared;
using YouTubeHelper.Shared.Models;
using YouTubeHelper.Shared.Utilities;

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
                        Dispatcher.CurrentDispatcher.BeginInvoke(() =>
                        {
                            ChannelViewModel channelViewModel = new(new Channel { VanityName = Resources.NewChannel, Index = Channels.Where(c => c != _newChannelTab).MaxBy(c => c.Channel.Index)?.Channel.Index + 1 ?? 0 }, this);
                            Channels.Insert(Channels.Count - 1, channelViewModel);
                            SelectedChannel = channelViewModel;
                        });
                    }
                }

                if (args.PropertyName == nameof(Mode))
                {
                    switch (Mode)
                    {
                        case MainControlMode.Search:
                            Channels.ToList().ForEach(c =>
                            {
                                c.Videos.Clear();
                            });
                            break;
                        case MainControlMode.Watch:
                            ExactSearchTerm = LookupSearchTerm = null;
                            Channels.ToList().ForEach(c =>
                            {
                                c.Videos.Clear();
                            });
                            break;
                        case MainControlMode.Exclusions:
                            ExactSearchTerm = LookupSearchTerm = null;
                            Channels.ToList().ForEach(c =>
                            {
                                c.Videos.Clear();
                            });
                            break;
                        default:
                            break;
                    }
                }
            };

            _newChannelTab = new(new Channel(nonPersistent: true) { VanityName = "+" }, this);
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
                OnPropertyChanged(nameof(WatchMode));
                OnPropertyChanged(nameof(SearchMode));
                OnPropertyChanged(nameof(ExclusionsMode));
            }
        }
        private MainControlMode _mode;

        public bool WatchMode => Mode == MainControlMode.Watch;

        public bool SearchMode => Mode == MainControlMode.Search;

        public bool ExclusionsMode => Mode == MainControlMode.Exclusions;

        public ObservableCollection<ChannelViewModel> Channels { get; } = new();

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }
        private bool _isBusy;

        public ChannelViewModel SelectedChannel
        {
            get => _selectedChannel;
            set => SetProperty(ref _selectedChannel, value);
        }
        private ChannelViewModel _selectedChannel;

        private readonly ChannelViewModel _newChannelTab;

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


        public string ActiveVideo
        {
            get => _activeVideo;
            set => SetProperty(ref _activeVideo, value);
        }
        private string _activeVideo;

        public string ActiveVideoTitle
        {
            get => _activeVideoTitle;
            set => SetProperty(ref _activeVideoTitle, value);
        }
        private string _activeVideoTitle;

        // This property never has a value, but it can be used to signal the view to resume playback without changing the ActiveVideo
        public object SignalPlayVideo { get; set; }

        // This property never has a value, but it can be used to signal the view to resume playback without changing the ActiveVideo
        public object SignalPauseVideo { get; set; }

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
            get => _selectedSortMode ?? SortModeValues.FirstOrDefault();
            set => SetProperty(ref _selectedSortMode, value);
        }
        private SortModeExtended _selectedSortMode;

        public int SelectedSortModeIndex
        {
            get => _selectedSortModeIndex;
            set => SetProperty(ref _selectedSortModeIndex, value);
        }
        private int _selectedSortModeIndex;

        public bool ShowExcludedVideos
        {
            get => _showExcludedVideos;
            set => SetProperty(ref _showExcludedVideos, value);
        }
        private bool _showExcludedVideos;

        public IEnumerable<ExclusionReasonExtended> ExclusionReasonValues { get; } = Enum.GetValues(typeof(ExclusionReason)).OfType<ExclusionReason>().Select(m => new ExclusionReasonExtended(m)).ToList();

        public ExclusionReasonExtended SelectedExclusionFilter
        {
            get => _exclusionFilter ?? ExclusionReasonValues.FirstOrDefault();
            set => SetProperty(ref _exclusionFilter, value);
        }
        private ExclusionReasonExtended _exclusionFilter;

        public int SelectedExclusionFilterIndex
        {
            get => _selectedExclusionFilterIndex;
            set => SetProperty(ref _selectedExclusionFilterIndex, value);
        }
        private int _selectedExclusionFilterIndex;

        public string ExactSearchTerm
        {
            get => _exactSearchTerm;
            set => SetProperty(ref _exactSearchTerm, value);
        }
        private string _exactSearchTerm;

        public string LookupSearchTerm
        {
            get => _lookupSearchTerm;
            set
            {
                // See if this is a URL and try to parse it
                if (Uri.TryCreate(value, new UriCreationOptions(), out Uri uri))
                {
                    var queryString = HttpUtility.ParseQueryString(uri.Query);
                    string videoId = queryString["v"];
                    if (!string.IsNullOrEmpty(videoId))
                    {
                        SetProperty(ref _lookupSearchTerm, videoId);
                        return;
                    }
                }

                SetProperty(ref _lookupSearchTerm, value);
            }
        }

        private string _lookupSearchTerm;

        #endregion

        public bool AllowCreateNewChannel { get; set; } = true;

        public async Task Load()
        {
            if (!_loaded)
            {
                var channels = (await DatabaseEngine.ChannelCollection.FindAllAsync()).OrderBy(c => c.Index);
                foreach (var c in channels)
                {
                    Channels.Add(new ChannelViewModel(c, this));
                }

                Channels.Add(_newChannelTab);

                try
                {
                    SelectedSortModeIndex = SortModeValues.ToList().IndexOf(SortModeValues.FirstOrDefault(s => s.Value == ApplicationSettings.Instance.SelectedSortMode));
                    if (Channels[ApplicationSettings.Instance.SelectedTabIndex] != _newChannelTab)
                    {
                        SelectedChannel = Channels[ApplicationSettings.Instance.SelectedTabIndex];
                    }
                }
                catch
                {
                    // ignored
                }

                _loaded = true;
            }
        }

        // Helps to prevent double-loading when using the app through RDP sessions.
        private bool _loaded;
    }

    public enum MainControlMode
    {
        Watch,
        Search,
        Exclusions
    }
}
