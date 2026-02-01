using Android.App;
using Firebase.Messaging;
using Plugin.LocalNotification;
using ServerStatusBot.Definitions.Api;

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

        public override async void OnNewToken(string token)
        {
            base.OnNewToken(token);

            _ = await ServerApiClient.Instance.RegisterDeviceId(token);
        }

        public static async Task HandleNotificationData(IDictionary<string, string> data)
        {
            // Check if this is a notification dismissal message
            if (data.TryGetValue("notificationId", out string? notificationIdStr) &&
                data.TryGetValue("clientId", out string? clientId))
            {
                // This is a dismissal message
                if (int.TryParse(notificationIdStr, out int notificationId))
                {
                    // Only dismiss if this dismissal didn't originate from this client
                    if (clientId != AppShell.ClientId)
                    {
#if ANDROID
                        AndroidUtils.DismissNotification(global::Android.App.Application.Context, notificationId, broadcast: false);
#endif
                    }
                }

                return; // Don't process as a regular notification
            }

            // Otherwise, handle this as a regular video notification
            data.TryGetValue("title", out string? title);
            data.TryGetValue("body", out string? body);
            data.TryGetValue("tag", out string? tag);
            data.TryGetValue("progress", out string? progressStr);
            bool hasProgress = double.TryParse(progressStr, out double progress);
            data.TryGetValue("done", out string? doneStr);
            bool isDone = bool.TryParse(doneStr, out bool done) && done;
            data.TryGetValue("newVideo", out string? newVideoStr);
            bool isNewVideo = bool.TryParse(newVideoStr, out bool newVideo) && newVideo;
            data.TryGetValue("failed", out string? failedStr);
            bool isFailed = bool.TryParse(failedStr, out bool failed) && failed;
            data.TryGetValue("videoUrl", out string? videoUrl);
            data.TryGetValue("thumbnailUrl", out string? thumbnailUrl);
            data.TryGetValue("channelThumbnailUrl", out string? channelThumbnailUrl);
            data.TryGetValue("plexRatingKey", out string? plexRatingKey);
            data.TryGetValue("channelName", out string? channelName);

            if (title is not null && body is not null && tag is not null)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (await LocalNotificationCenter.Current.AreNotificationsEnabled() == false)
                    {
                        await LocalNotificationCenter.Current.RequestNotificationPermission();
                    }
                });

#if ANDROID
                AndroidNotificationHelper.Show(
                    title: title,
                    channelName: channelName,
                    body: body,
                    videoUrl: videoUrl,
                    thumbnailPath: await Utilities.GetCachedImagePath(thumbnailUrl),
                    channelThumbnailPath: await Utilities.GetCachedImagePath(channelThumbnailUrl),
                    notificationChannelId: isDone || isNewVideo ? "completion" : "progress",
                    notificationId: int.Parse(tag),
                    isDone: isDone,
                    isNewVideo: isNewVideo,
                    isFailed: isFailed,
                    hasProgress: hasProgress,
                    progress: progress,
                    plexRatingKey: plexRatingKey
                );
#endif

            }
        }
    }
}