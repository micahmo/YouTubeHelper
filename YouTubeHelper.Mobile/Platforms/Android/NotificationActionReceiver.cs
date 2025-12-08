using Android.App;
using Android.Content;
using ServerStatusBot.Definitions;
using ServerStatusBot.Definitions.Api;
using ServerStatusBot.Definitions.Database.Models;
using ServerStatusBot.Definitions.Models;

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
            string? rawUrl = intent.GetStringExtra(Intent.ExtraText);

            switch (actionType)
            {
                case "dismiss":
                    await HandleDismiss(context, intent);
                    break;
            }

            if (!string.IsNullOrEmpty(rawUrl) && YouTubeUtils.GetVideoIdFromUrl(rawUrl) is { } videoId)
            {
                PendingResult? pendingResult = GoAsync();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        string? markVideoStr = intent.GetStringExtra("markVideo");
                        ExclusionReason? markVideoEnum = markVideoStr is { } s && Enum.TryParse(s, ignoreCase: true, out ExclusionReason reason) ? reason : null;

                        switch (markVideoEnum)
                        {
                            case ExclusionReason.WontWatch:
                                if (await AppShell.ConnectToServerSilent())
                                {
                                    if ((await ServerApiClient.Instance.FindVideos(new FindVideosRequest
                                        {
                                            ExclusionsMode = ExclusionsMode.ShowAll,
                                            VideoIds = new List<string> { videoId },
                                            SortMode = SortMode.AgeDesc,
                                            Count = int.MaxValue
                                        })).FirstOrDefault() is { } video)
                                    {
                                        video.Excluded = true;
                                        video.ExclusionReason = ExclusionReason.WontWatch;
                                        await ServerApiClient.Instance.UpdateVideo(video, AppShell.ClientId);
                                    }
                                }

                                await HandleDismiss(context, intent);

                                break;
                            default:
                                break;
                        }
                    }
                    finally
                    {
                        pendingResult?.Finish();
                    }
                });
            }
        }

        private Task HandleDismiss(Context context, Intent intent)
        {
            int notificationId = intent.GetIntExtra("notificationId", -1);

            AndroidX.Core.App.NotificationManagerCompat.From(context).Cancel(notificationId);

            return Task.CompletedTask;
        }
    }

}
