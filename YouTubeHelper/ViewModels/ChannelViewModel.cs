using System;
using System.Windows.Input;
using Microsoft.Toolkit.Mvvm.Input;
using YouTubeHelper.Models;

namespace YouTubeHelper.ViewModels
{
    public class ChannelViewModel
    {
        public ChannelViewModel(Channel channel, MainControlViewModel mainControlViewModel)
        {
            Channel = channel;
            _mainControlViewModel = mainControlViewModel;
        }

        public ICommand DeleteCommand => _deleteChannelCommand ??= new RelayCommand(Delete);
        private ICommand _deleteChannelCommand;

        private void Delete()
        {
            _mainControlViewModel.SelectedChannel = _mainControlViewModel.Channels[Math.Max(0, _mainControlViewModel.Channels.IndexOf(this) - 1)];
            _mainControlViewModel.Channels.Remove(this);
            DatabaseEngine.ChannelCollection.Delete(Channel.ObjectId);
        }

        public Channel Channel { get; }

        private MainControlViewModel _mainControlViewModel;
    }
}
