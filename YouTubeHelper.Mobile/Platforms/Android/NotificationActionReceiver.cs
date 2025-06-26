using Android.App;
using Android.Content;
using ServerStatusBot.Definitions;
using ServerStatusBot.Definitions.Api;

namespace YouTubeHelper.Mobile.Platforms.Android
{
    [BroadcastReceiver(Enabled = true, Exported = true)]
    [IntentFilter(new[] { "com.micahmo.youtubehelper.NOTIFICATION_ACTION" })]
    public class NotificationActionReceiver : BroadcastReceiver
    {
        public override async void OnReceive(Context? context, Intent? intent)
        {
            if (context is null || intent is null)
            {
                return;
            }
            
            string? actionType = intent.GetStringExtra("actionType");

            switch (actionType)
            {
                case "openInPlex":
                    await HandleOpenInPlex(context, intent);
                    break;

                case "dismiss":
                    await HandleDismiss(context, intent);
                    break;
            }
        }

        private async Task HandleOpenInPlex(Context context, Intent intent)
        {
            string videoTitle = intent.GetStringExtra("videoTitle")!;
            string videoUrl = intent.GetStringExtra("videoUrl")!;
            string videoId = YouTubeUtils.GetVideoIdFromUrl(videoUrl)!;
            int notificationId = intent.GetIntExtra("notificationId", -1);

            string? ratingKey = await ServerApiClient.Instance.GetPlexRatingKey(videoTitle, videoId);
            if (!string.IsNullOrEmpty(ratingKey))
            {
                string plexUri = $"plex://server://8316eb530162c189b29f3250d4734700515fc5f8/com.plexapp.plugins.library/library/metadata/{ratingKey}";
                await Launcher.Default.OpenAsync(new Uri(plexUri));
            }

            AndroidX.Core.App.NotificationManagerCompat.From(context).Cancel(notificationId);
        }

        private Task HandleDismiss(Context context, Intent intent)
        {
            int notificationId = intent.GetIntExtra("notificationId", -1);

            AndroidX.Core.App.NotificationManagerCompat.From(context).Cancel(notificationId);

            return Task.CompletedTask;
        }
    }

}
