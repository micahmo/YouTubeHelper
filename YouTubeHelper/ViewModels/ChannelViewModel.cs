using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using YouTubeHelper.Models;
using YouTubeHelper.Utilities;

namespace YouTubeHelper.ViewModels
{
    public class ChannelViewModel : ObservableObject
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

        public ICommand SearchCommand => _searchCommand ??= new RelayCommand(Search);
        private ICommand _searchCommand;

        private async void Search()
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

        public string SearchGlyph
        {
            get => _searchGlyph;
            set => SetProperty(ref _searchGlyph, value);
        }
        private string _searchGlyph = Icons.Search;

        public Channel Channel { get; }

        private readonly MainControlViewModel _mainControlViewModel;
    }
}
