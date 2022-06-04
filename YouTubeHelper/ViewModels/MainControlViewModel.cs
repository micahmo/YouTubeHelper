using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Toolkit.Mvvm.ComponentModel;
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
                        Dispatcher.CurrentDispatcher.BeginInvoke(() =>
                        {
                            ChannelViewModel channelViewModel = new(new Channel { VanityName = Resources.NewChannel }, this);
                            Channels.Insert(Channels.Count - 1, channelViewModel);
                            SelectedChannel = channelViewModel;
                        });
                    }
                }
            };

            _newChannelTab = new(new Channel(nonPersistent: true) { VanityName = "+" }, this);
        }

        public MainControlMode Mode
        {
            get => _mode;
            set
            {
                SetProperty(ref _mode, value);
                OnPropertyChanged(nameof(WatchMode));
                OnPropertyChanged(nameof(SearchMode));
            }
        }
        private MainControlMode _mode;

        public bool WatchMode => Mode == MainControlMode.Watch;

        public bool SearchMode => Mode == MainControlMode.Search;

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

        public bool IsPlayerExpanded
        {
            get => _isPlayerExpanded;
            set => SetProperty(ref _isPlayerExpanded, value);
        }
        private bool _isPlayerExpanded;

        public GridLength ExpandedGridLength { get; } = new(1, GridUnitType.Star);

        public GridLength CollapsedGridLength { get; } = new(15, GridUnitType.Pixel);


        public string ActiveVideo
        {
            get => _activeVideo;
            set => SetProperty(ref _activeVideo, value);
        }
        private string _activeVideo;

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

        #endregion

        public bool AllowCreateNewChannel { get; set; } = true;

        public void Load()
        {
            DatabaseEngine.ChannelCollection.FindAll().OrderBy(c => c.Index).ToList().ForEach(c =>
            {
                Channels.Add(new ChannelViewModel(c, this));
            });

            Channels.Add(_newChannelTab);
        }
    }

    public enum MainControlMode
    {
        Watch,
        Search
    }
}
