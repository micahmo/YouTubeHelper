using Android.App;
using Firebase.Messaging;
using Plugin.LocalNotification;

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