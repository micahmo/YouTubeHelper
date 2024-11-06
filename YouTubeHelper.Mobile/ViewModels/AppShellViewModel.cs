using Microsoft.Toolkit.Mvvm.ComponentModel;
using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;

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
            List<VideoViewModel> videosBeingDownloaded = (QueueTabSelected && QueueChannelViewModel != null ? new List<ChannelViewModel> { QueueChannelViewModel } : ChannelViewModels)
                .SelectMany(c => c.Videos.Where(v => v.HasStatus)).ToList();

            int activeDownloads = videosBeingDownloaded.Count;

            if (activeDownloads > 0)
            {
                if (await LocalNotificationCenter.Current.AreNotificationsEnabled() == false)
                {
                    await LocalNotificationCenter.Current.RequestNotificationPermission();
                }

                int totalProgress = 100 * videosBeingDownloaded.Count;
                double currentProgress = videosBeingDownloaded.Where(v => v.Video.Progress is not null).Select(v => v.Video.Progress!.Value).Sum();
                int cumulativeProgress = (int)((currentProgress / totalProgress) * 100);
                
                string newNotificationText = string.Format(Resources.Resources.ActiveDownloadsText, activeDownloads);
                
                if (_currentNotificationText != newNotificationText || _currentProgress != cumulativeProgress)
                {
                    var notification = new NotificationRequest
                    {
                        NotificationId = 100,
                        Title = Resources.Resources.ActiveDownloads,
                        Description = _currentNotificationText = newNotificationText,
                        Android =
                        {
                            Ongoing = true, 
                            IconLargeName = { ResourceName = "splash" }, 
                            IconSmallName = { ResourceName = "notification_icon" },
                            ProgressBar = new AndroidProgressBar
                            {
                                Max = 100,
                                Progress = (_currentProgress = cumulativeProgress).Value
                            },
                            Priority = AndroidPriority.Low
                        }
                    };
                    await LocalNotificationCenter.Current.Show(notification);
                }
            }
            else
            {
                _currentNotificationText = default;
                _currentProgress = default;
                LocalNotificationCenter.Current.Clear(100);
            }
        }

        private string? _currentNotificationText;
        private int? _currentProgress;
    }
}
