using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
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
                    Dispatcher.CurrentDispatcher.BeginInvoke(() =>
                    {
                        ChannelViewModel channelViewModel = new(new Channel { VanityName = Resources.NewChannel }, this);
                        Channels.Insert(Channels.Count - 1, channelViewModel);
                        SelectedChannel = channelViewModel;
                    });
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

        public ChannelViewModel SelectedChannel
        {
            get => _selectedChannel;
            set => SetProperty(ref _selectedChannel, value);
        }
        private ChannelViewModel _selectedChannel;

        private readonly ChannelViewModel _newChannelTab;

        public void Load()
        {
            DatabaseEngine.ChannelCollection.FindAll().ToList().ForEach(c =>
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
