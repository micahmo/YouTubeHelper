using Microsoft.Toolkit.Mvvm.ComponentModel;

namespace YouTubeHelper.Mobile.ViewModels
{
    public class AppShellViewModel : ObservableObject
    {
        private readonly AppShell _appShell;

        public AppShellViewModel(AppShell appShell) => _appShell = appShell;

        public bool ChannelTabSelected => _appShell.CurrentItem?.CurrentItem == _appShell.ChannelTab;

        public bool QueueTabSelected => _appShell.CurrentItem?.CurrentItem == _appShell.QueueTab;

        public List<ChannelViewModel> ChannelViewModels { get; } = new();

        public ChannelViewModel? QueueChannelViewModel { get; set; }

        /// <summary>
        /// Returns all current <see cref="VideoViewModel"/>s from all <see cref="ChannelViewModels"/> and the <see cref="QueueChannelViewModel"/>.
        /// </summary>
        public List<VideoViewModel> AllVideos => ChannelViewModels.SelectMany(c => c.Videos).Union(QueueChannelViewModel?.Videos ?? Enumerable.Empty<VideoViewModel>()).ToList();

        public void RaisePropertyChanged(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }

        public void SelectChannelTab()
        {
            _appShell.CurrentItem.CurrentItem = _appShell.ChannelTab;
        }
    }
}
