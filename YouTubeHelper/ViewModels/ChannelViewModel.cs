using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Shell;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using YouTubeHelper.Models;
using YouTubeHelper.Properties;
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
                }
            };

            Videos.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(CountLabel));
            };
        }

        public ICommand DeleteCommand => _deleteChannelCommand ??= new RelayCommand(Delete);
        private ICommand _deleteChannelCommand;

        private void Delete()
        {
            MainControlViewModel.SelectedChannel = MainControlViewModel.Channels[Math.Max(0, MainControlViewModel.Channels.IndexOf(this) - 1)];
            MainControlViewModel.Channels.Remove(this);
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
            MainControlViewModel.IsBusy = true;
            MainControlViewModel.IsPlayerExpanded = false;

            Videos.Clear();

            try
            {
                List<Video> exclusions = DatabaseEngine.ExcludedVideosCollection.Find(v => v.ChannelPlaylist == Channel.ChannelPlaylist).ToList();
                List<string> searchTerms = null;

                if (MainControlViewModel.Mode == MainControlMode.Search && !string.IsNullOrEmpty(MainControlViewModel.LookupSearchTerm))
                {
                    (await YouTubeApi.Instance.SearchVideos(Channel, exclusions, MainControlViewModel.ShowExcludedVideos, MainControlViewModel.SelectedSortMode.Value, MainControlViewModel.LookupSearchTerm)).ToList().ForEach(v => Videos.Add(new VideoViewModel(v, MainControlViewModel, this)));
                }
                else
                {
                    if (MainControlViewModel.Mode == MainControlMode.Search)
                    {
                        if (!string.IsNullOrEmpty(MainControlViewModel.ExactSearchTerm))
                        {
                            searchTerms = MainControlViewModel.ExactSearchTerm.Split().ToList();
                        }
                    }

                    (await YouTubeApi.Instance.FindVideos(Channel, exclusions, MainControlViewModel.ShowExcludedVideos, MainControlViewModel.SelectedSortMode.Value, searchTerms, (progress, indeterminate) =>
                    {
                        MainControlViewModel.Progress = progress;
                        MainControlViewModel.ProgressState = indeterminate ? TaskbarItemProgressState.Indeterminate : TaskbarItemProgressState.Normal;
                    })).ToList().ForEach(v => Videos.Add(new VideoViewModel(v, MainControlViewModel, this)));

                    MainControlViewModel.Progress = 0;
                    MainControlViewModel.ProgressState = TaskbarItemProgressState.Normal;
                }
            }
            finally
            {
                MainControlViewModel.IsBusy = false;
            }
        }

        public ICommand FindExclusionsCommand => _findExclusionsCommand ??= new RelayCommand(FindExclusions);
        private ICommand _findExclusionsCommand;

        private async void FindExclusions()
        {
            MainControlViewModel.IsBusy = true;

            Videos.Clear();

            try
            {
                List<Video> exclusions = DatabaseEngine.ExcludedVideosCollection.Find(v => v.ChannelPlaylist == Channel.ChannelPlaylist).ToList();

                if (MainControlViewModel.SelectedExclusionFilter.Value != ExclusionReason.None)
                {
                    exclusions = exclusions.Where(v => MainControlViewModel.SelectedExclusionFilter.Value.HasFlag(v.ExclusionReason)).ToList();
                }

                (await YouTubeApi.Instance.FindVideoDetails(exclusions.Select(v => v.Id).ToList(), exclusions, Channel, MainControlViewModel.SelectedSortMode.Value, count: int.MaxValue)).ToList().ForEach(v => Videos.Add(new VideoViewModel(v, MainControlViewModel, this)));
            }
            finally
            {
                MainControlViewModel.IsBusy = false;
            }
        }

        public ICommand MoveRightCommand => _moveRightCommand ??= new RelayCommand(MoveRight);
        private ICommand _moveRightCommand;

        private void MoveRight()
        {
            MainControlViewModel.AllowCreateNewChannel = false;
            int previousIndex = MainControlViewModel.Channels.IndexOf(this);
            MainControlViewModel.Channels.Remove(this);
            MainControlViewModel.Channels.Insert(Math.Min(MainControlViewModel.Channels.Count - 1, previousIndex + 1), this);
            MainControlViewModel.SelectedChannel = this;
            MainControlViewModel.AllowCreateNewChannel = true;

            MainControlViewModel.Channels.ToList().ForEach(c =>
            {
                c.Channel.Index = MainControlViewModel.Channels.IndexOf(c);
                DatabaseEngine.ChannelCollection.Update(c.Channel);
            });
        }

        public ICommand MoveLeftCommand => _moveLeftCommand ??= new RelayCommand(MoveLeft);
        private ICommand _moveLeftCommand;

        private void MoveLeft()
        {
            MainControlViewModel.AllowCreateNewChannel = false;
            int previousIndex = MainControlViewModel.Channels.IndexOf(this);
            MainControlViewModel.Channels.Remove(this);
            MainControlViewModel.Channels.Insert(Math.Max(0, previousIndex - 1), this);
            MainControlViewModel.SelectedChannel = this;
            MainControlViewModel.AllowCreateNewChannel = true;

            MainControlViewModel.Channels.ToList().ForEach(c =>
            {
                c.Channel.Index = MainControlViewModel.Channels.IndexOf(c);
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

        public bool WatchMode => MainControlViewModel.Mode == MainControlMode.Watch;

        public bool SearchMode => MainControlViewModel.Mode == MainControlMode.Search;

        public bool ExclusionsMode => MainControlViewModel.Mode == MainControlMode.Exclusions;

        public string CountLabel => string.Format(Resources.CountLabel, Videos.Count);

        public Channel Channel { get; }

        public MainControlViewModel MainControlViewModel { get; }
    }
}
