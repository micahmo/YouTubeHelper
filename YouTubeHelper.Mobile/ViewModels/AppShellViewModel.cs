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

        public void RaisePropertyChanged(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }

        public void SelectChannelTab()
        {
            _appShell.CurrentItem.CurrentItem = _appShell.ChannelTab;
        }

        private string? _currentNotificationText;
        private int? _currentProgress;
    }
}
