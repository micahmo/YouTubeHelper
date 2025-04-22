using Android.App;
using Firebase.Messaging;
using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;

namespace YouTubeHelper.Mobile.Platforms.Android
{
    [Service(Name = "com.micahmo.YouTubeHelper.MyFirebaseMessagingService")]
    [IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
    public class MyFirebaseMessagingService : FirebaseMessagingService
    {
        public override async void OnMessageReceived(RemoteMessage message)
        {
            base.OnMessageReceived(message);

            await HandleNotificationData(message.Data);
        }

        public static async Task HandleNotificationData(IDictionary<string, string> data)
        {
            data.TryGetValue("title", out string? title);
            data.TryGetValue("body", out string? body);
            data.TryGetValue("tag", out string? tag);
            data.TryGetValue("progress", out string? progressStr);
            bool hasProgress = double.TryParse(progressStr, out double progress);

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
                        AutoCancel = false,
                        IconSmallName = { ResourceName = "notification_icon" },
                        Priority = AndroidPriority.Min,
                        ProgressBar = !hasProgress ? null : new AndroidProgressBar
                        {
                            Max = 100,
                            Progress = (int)progress
                        },
                    }
                };

                await LocalNotificationCenter.Current.Show(notification);
            }
        }
    }
}