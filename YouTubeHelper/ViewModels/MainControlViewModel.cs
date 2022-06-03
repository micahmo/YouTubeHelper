using System.Collections.ObjectModel;
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
                if (args.PropertyName == nameof(SelectedChannel) && SelectedChannel == NewChannelTab)
                {
                    Dispatcher.CurrentDispatcher.BeginInvoke(() =>
                    {
                        Channel channel = new Channel { Name = Resources.NewChannel };
                        Channels.Insert(Channels.Count - 1, channel);
                        SelectedChannel = channel;
                    });
                }
            };
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

        public ObservableCollection<Channel> Channels { get; } = new() { NewChannelTab };

        public Channel SelectedChannel
        {
            get => _selectedChannel;
            set => SetProperty(ref _selectedChannel, value);
        }
        private Channel _selectedChannel;

        private static readonly Channel NewChannelTab = new() { Name = "+" };
    }

    public enum MainControlMode
    {
        Watch,
        Search
    }
}
