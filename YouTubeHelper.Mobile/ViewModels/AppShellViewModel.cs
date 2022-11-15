using Microsoft.Toolkit.Mvvm.ComponentModel;

namespace YouTubeHelper.Mobile.ViewModels
{
    public class AppShellViewModel : ObservableObject
    {
        private readonly AppShell _appShell;

        public AppShellViewModel(AppShell appShell) => _appShell = appShell;

        public bool WatchTabSelected => _appShell.CurrentItem?.CurrentItem == _appShell.WatchTab;

        public bool SearchTabSelected => _appShell.CurrentItem?.CurrentItem == _appShell.SearchTab;

        public bool ExclusionsTabSelected => _appShell.CurrentItem?.CurrentItem == _appShell.ExclusionsTab;

        public List<ChannelViewModel> ChannelViewModels { get; } = new();

        public void RaisePropertyChanged(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }
    }
}
