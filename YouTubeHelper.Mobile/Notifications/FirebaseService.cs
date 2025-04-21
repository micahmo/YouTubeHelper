using Plugin.Firebase.CloudMessaging;
using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;
using ServerStatusBot.Definitions.Api;

namespace YouTubeHelper.Mobile.Notifications
{
    public class FirebaseService
    {
        public static async Task InitializeAsync()
        {
            string? token = await CrossFirebaseCloudMessaging.Current.GetTokenAsync();

            if (!string.IsNullOrEmpty(token))
            {
                await ServerApiClient.Instance.RegisterDeviceId(token);

                CrossFirebaseCloudMessaging.Current.NotificationReceived += async (_, args) =>
                {
                    string? title = args.Notification?.Title;
                    string? body = args.Notification?.Body;
                    string? tag = null;
                    args.Notification?.Data.TryGetValue("tag", out tag);

                    if (title is not null && body is not null && tag is not null)
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            if (await LocalNotificationCenter.Current.AreNotificationsEnabled() == false)
                            {
                                await LocalNotificationCenter.Current.RequestNotificationPermission();
                            }
                        });

                        NotificationRequest notification = new NotificationRequest
                        {
                            NotificationId = int.Parse(tag),
                            Title = title,
                            Description = body,
                            Android =
                            {
                                Ongoing = false,
                                IconSmallName = { ResourceName = "notification_icon" },
                                Priority = AndroidPriority.Min
                            }
                        };
                        await LocalNotificationCenter.Current.Show(notification);
                    }
                };
            }
        }
    }
}
