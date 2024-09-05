using Microsoft.Toolkit.Mvvm.ComponentModel;
using Plugin.LocalNotification;

namespace YouTubeHelper.Mobile.ViewModels
{
    public class AppShellViewModel : ObservableObject
    {
        private readonly AppShell _appShell;

        public AppShellViewModel(AppShell appShell) => _appShell = appShell;

        public bool WatchTabSelected => _appShell.CurrentItem?.CurrentItem == _appShell.WatchTab;

        public bool SearchTabSelected => _appShell.CurrentItem?.CurrentItem == _appShell.SearchTab;

        public bool ExclusionsTabSelected => _appShell.CurrentItem?.CurrentItem == _appShell.ExclusionsTab;

        public bool QueueTabSelected => _appShell.CurrentItem?.CurrentItem == _appShell.QueueTab;

        public bool NotQueueTabSelected => _appShell.CurrentItem?.CurrentItem != _appShell.QueueTab;

        public List<ChannelViewModel> ChannelViewModels { get; } = new();

        public ChannelViewModel? QueueChannelViewModel { get; set; }

        public void RaisePropertyChanged(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }

        public async Task UpdateNotification()
        {
            int activeDownloads = (QueueTabSelected && QueueChannelViewModel != null ? new List<ChannelViewModel> { QueueChannelViewModel } : ChannelViewModels)
                .Select(c => c.Videos.Count(v => v.HasStatus)).Sum();

            if (activeDownloads > 0)
            {
                if (await LocalNotificationCenter.Current.AreNotificationsEnabled() == false)
                {
                    await LocalNotificationCenter.Current.RequestNotificationPermission();
                }

                string newNotificationText = string.Format(Resources.Resources.ActiveDownloadsText, activeDownloads);
                if (_currentNotificationText != newNotificationText)
                {
                    var notification = new NotificationRequest
                    {
                        NotificationId = 100,
                        Title = Resources.Resources.ActiveDownloads,
                        Description = _currentNotificationText = newNotificationText,
                        Android = { Ongoing = true, IconLargeName = { ResourceName = "splash" }, IconSmallName = { ResourceName = "notification_icon" } }
                    };
                    await LocalNotificationCenter.Current.Show(notification);
                }
            }
            else
            {
                _currentNotificationText = default;
                LocalNotificationCenter.Current.Clear(100);
            }
        }

        private string? _currentNotificationText;
    }
}
