using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using MongoDB.Driver;
using YouTubeHelper.Mobile.Views;
using YouTubeHelper.Shared;
using YouTubeHelper.Shared.Models;
using YouTubeHelper.Shared.Utilities;

namespace YouTubeHelper.Mobile.ViewModels
{
    public class ChannelViewModel : ObservableObject
    {
        public ChannelViewModel(AppShell page)
        {
            Page = page;

            PropertyChanged += (_, args) =>
            {
                if (!_listeningToPropertyChanges)
                {
                    return;
                }

                if (args.PropertyName is nameof(ShowExcludedVideos) or nameof(SelectedSortMode) or nameof(SelectedExclusionFilter) or nameof(ExactSearchTerm))
                {
                    Preferences.Default.Set(nameof(SelectedSortModeIndex), SelectedSortModeIndex);
                    
                    _listeningToPropertyChanges = false;
                    Page.AppShellViewModel.ChannelViewModels.ForEach(c =>
                    {
                        c.ShowExcludedVideos = ShowExcludedVideos;
                        c.SelectedSortModeIndex = SortModeValues.ToList().IndexOf(SelectedSortMode);
                        c.SelectedExclusionFilterIndex = ExclusionReasonValues.ToList().IndexOf(SelectedExclusionFilter);
                        c.ExactSearchTerm = ExactSearchTerm;
                    });
                    _listeningToPropertyChanges = true;
                }
            };

            Videos.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SearchCount));
        }

        private static bool _listeningToPropertyChanges = true;

        public Channel Channel { get; set; }

        public ObservableCollection<VideoViewModel> Videos { get; } = new();

        public ICommand ToggleShowExcludedVideosCommand => _toggleShowExcludedVideosCommand ??= new RelayCommand(() =>
        {
            ShowExcludedVideos = !ShowExcludedVideos;
        });
        private ICommand _toggleShowExcludedVideosCommand;

        public ICommand FindVideosCommand => _findVideosCommand ??= new RelayCommand(FindVideos);
        private ICommand _findVideosCommand;

        public async void FindVideos()
        {
            IsRefreshing = false;
            using (new BusyIndicator(Page))
            {
                Videos.Clear();

                // FindVideos
                if (Page.AppShellViewModel.WatchTabSelected || Page.AppShellViewModel.SearchTabSelected)
                {
                    List<Video> exclusions = DatabaseEngine.ExcludedVideosCollection.Find(v => v.ChannelPlaylist == Channel.ChannelPlaylist).ToList();
                    List<string> searchTerms = null;

                    //if (Page.SearchTabSelected && !string.IsNullOrEmpty(MainControlViewModel.LookupSearchTerm))
                    //{
                    //    (await YouTubeApi.Instance.SearchVideos(Channel, exclusions, MainControlViewModel.ShowExcludedVideos, MainControlViewModel.SelectedSortMode.Value, MainControlViewModel.LookupSearchTerm)).ToList().ForEach(v => Videos.Add(new VideoViewModel(v, MainControlViewModel, this)));
                    //}
                    //else
                    {
                        if (Page.AppShellViewModel.SearchTabSelected)
                        {
                            if (!string.IsNullOrEmpty(ExactSearchTerm))
                            {
                                searchTerms = ExactSearchTerm.Split().ToList();
                            }
                        }

                        (await YouTubeApi.Instance.FindVideos(Channel, exclusions, ShowExcludedVideos, SelectedSortMode?.Value ?? SortMode.DurationPlusRecency, searchTerms, (progress, indeterminate) =>
                        {
                            // TODO: Update progress?
                        })).ToList().ForEach(v => Videos.Add(new VideoViewModel(v, Page, this)));

                        // TODO: Reset progress?
                    }
                }
                // FindExclusions
                else if (Page.AppShellViewModel.ExclusionsTabSelected)
                {
                    List<Video> exclusions = DatabaseEngine.ExcludedVideosCollection.Find(v => v.ChannelPlaylist == Channel.ChannelPlaylist).ToList();

                    if (SelectedExclusionFilter.Value != ExclusionReason.None)
                    {
                        exclusions = exclusions.Where(v => SelectedExclusionFilter.Value.HasFlag(v.ExclusionReason)).ToList();
                    }

                    (await YouTubeApi.Instance.FindVideoDetails(exclusions.Select(v => v.Id).ToList(), exclusions, Channel, SelectedSortMode?.Value ?? SortMode.DurationPlusRecency, count: int.MaxValue)).ToList().ForEach(v => Videos.Add(new VideoViewModel(v, Page, this)));
                }
            }
        }

        public IEnumerable<SortModeExtended> SortModeValues { get; } = Enum.GetValues(typeof(SortMode)).OfType<SortMode>().Select(m => new SortModeExtended(m)).ToList();

        public IEnumerable<ExclusionReasonExtended> ExclusionReasonValues { get; } = Enum.GetValues(typeof(ExclusionReason)).OfType<ExclusionReason>().Select(m => new ExclusionReasonExtended(m)).ToList();

        public SortModeExtended SelectedSortMode
        {
            get => _selectedSortMode;
            set => SetProperty(ref _selectedSortMode, value);
        }
        private SortModeExtended _selectedSortMode;

        public int SelectedSortModeIndex
        {
            get => _selectedSortModeIndex;
            set
            {
                _selectedSortModeIndex = value;
                
                // Have to do an explicit raise here since setting to 0 doesn't.
                OnPropertyChanged(nameof(SelectedSortModeIndex));
            }
        }
        private int _selectedSortModeIndex;

        public ExclusionReasonExtended SelectedExclusionFilter
        {
            get => _selectedExclusionFilter ?? ExclusionReasonValues.FirstOrDefault();
            set => SetProperty(ref _selectedExclusionFilter, value);
        }
        private ExclusionReasonExtended _selectedExclusionFilter;

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

        public bool ShowExcludedVideos
        {
            get => _showExcludedVideos;
            set => SetProperty(ref _showExcludedVideos, value);
        }
        private bool _showExcludedVideos;

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => SetProperty(ref _isRefreshing, value);
        }
        private bool _isRefreshing;

        public AppShell Page { get; }

        public string SearchCount => Videos.Any() ? string.Format(Resources.Resources.SearchCount, Videos.Count) : Resources.Resources.Search;

        public bool Loading
        {
            get => _loading;
            set => SetProperty(ref _loading, value);
        }
        private bool _loading = true;
    }
}
