using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

        public ICommand LookupChannelCommand => _searchCommand ??= new RelayCommand(LookupChannel);
        private ICommand _searchCommand;

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
        private ICommand _findVideosCommand;

        private async void FindVideos()
        {
            _mainControlViewModel.IsBusy = true;

            Videos.Clear();

            try
            {
                (await YouTubeApi.Instance.FindVideos(Channel, SelectedSortMode.SortMode)).ToList().ForEach(v => Videos.Add(new VideoViewModel(v, _mainControlViewModel)));
            }
            finally
            {
                _mainControlViewModel.IsBusy = false;
            }
        }

        public ICommand MoveRightCommand => _moveRightCommand ??= new RelayCommand(MoveRight);
        private ICommand _moveRightCommand;

        private void MoveRight()
        {
            _mainControlViewModel.AllowCreateNewChannel = false;
            int previousIndex = _mainControlViewModel.Channels.IndexOf(this);
            _mainControlViewModel.Channels.Remove(this);
            _mainControlViewModel.Channels.Insert(Math.Min(_mainControlViewModel.Channels.Count - 1, previousIndex + 1), this);
            _mainControlViewModel.SelectedChannel = this;
            _mainControlViewModel.AllowCreateNewChannel = true;
        }

        public ICommand MoveLeftCommand => _moveLeftCommand ??= new RelayCommand(MoveLeft);
        private ICommand _moveLeftCommand;

        private void MoveLeft()
        {
            _mainControlViewModel.AllowCreateNewChannel = false;
            int previousIndex = _mainControlViewModel.Channels.IndexOf(this);
            _mainControlViewModel.Channels.Remove(this);
            _mainControlViewModel.Channels.Insert(Math.Max(0, previousIndex - 1), this);
            _mainControlViewModel.SelectedChannel = this;
            _mainControlViewModel.AllowCreateNewChannel = true;

            _mainControlViewModel.Channels.ToList().ForEach(c =>
            {
                c.Channel.Index = _mainControlViewModel.Channels.IndexOf(c);
                DatabaseEngine.ChannelCollection.Update(c.Channel);
            });
        }

        public string SearchGlyph
        {
            get => _searchGlyph;
            set => SetProperty(ref _searchGlyph, value);
        }
        private string _searchGlyph = Icons.Search;

        public ObservableCollection<VideoViewModel> Videos { get; } = new();

        #region Sorting

        public IEnumerable<SortModeExtended> SortModeValues => Enum.GetValues(typeof(SortMode)).OfType<SortMode>().Select(m => new SortModeExtended(m));

        public SortModeExtended SelectedSortMode
        {
            get => _selectedSortMode;
            set => SetProperty(ref _selectedSortMode, value);
        }
        private SortModeExtended _selectedSortMode;

        public int SelectedSortModeIndex
        {
            get => _selectedSortModeIndex;
            set => SetProperty(ref _selectedSortModeIndex, value);
        }
        private int _selectedSortModeIndex = 0;

        #endregion

        public Channel Channel { get; }

        private readonly MainControlViewModel _mainControlViewModel;
    }
}
