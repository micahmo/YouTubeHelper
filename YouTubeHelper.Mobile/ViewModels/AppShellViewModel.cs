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

        public List<ChannelViewModel> ChannelViewModels { get; } = new();

        public void RaisePropertyChanged(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }

        public async Task UpdateNotification()
        {
            int activeDownloads = ChannelViewModels.Select(c => c.Videos.Count(v => v.HasStatus)).Sum();

            if (activeDownloads > 0)
            {
                if (await LocalNotificationCenter.Current.AreNotificationsEnabled() == false)
                {
                    await LocalNotificationCenter.Current.RequestNotificationPermission();
                }

                var notification = new NotificationRequest
                {
                    NotificationId = 100,
                    Title = Resources.Resources.ActiveDownloads,
                    Description = string.Format(Resources.Resources.ActiveDownloadsText, activeDownloads),
                    Android = { Ongoing = true, IconLargeName = { ResourceName = "splash" }, IconSmallName = { ResourceName = "notification_icon" } }
                };
                await LocalNotificationCenter.Current.Show(notification);
            }
            else
            {
                LocalNotificationCenter.Current.Clear(100);
            }
        }
    }
}
