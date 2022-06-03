using Microsoft.Toolkit.Mvvm.ComponentModel;

namespace YouTubeHelper.ViewModels
{
    internal class MainControlViewModel : ObservableObject
    {
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
    }

    internal enum MainControlMode
    {
        Watch,
        Search
    }
}
