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
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (context is null || intent is null)
            {
                return;
            }

            string? rawUrl = intent.GetStringExtra(Intent.ExtraText);
            string? actionType = intent.GetStringExtra("actionType");
            string? markVideoStr = intent.GetStringExtra("markVideo");
            ExclusionReason? markVideoEnum = markVideoStr is { } s && Enum.TryParse(s, ignoreCase: true, out ExclusionReason reason) ? reason : null;
            bool downloadVideo = intent.GetBooleanExtra("downloadVideo", false);

            switch (actionType)
            {
                case "dismiss":
                    HandleDismiss(context, intent);
                    break;
            }

            if (!string.IsNullOrEmpty(rawUrl) && YouTubeUtils.GetVideoIdFromUrl(rawUrl) is { } videoId && markVideoEnum is not null)
            {
                // Immediately update notification to show the action as disabled
                string disabledAction = markVideoEnum.Value.ToString();
                UpdateNotificationWithDisabledAction(context, intent, disabledAction);

                PendingResult? pendingResult = GoAsync();

                _ = Task.Run(async () =>
                {
                    try
                    {
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
                                video.ExclusionReason = markVideoEnum.Value;
                                await ServerApiClient.Instance.UpdateVideo(video, AppShell.ClientId);
                            }
                        }

                        HandleDismiss(context, intent);

                    }
                    finally
                    {
                        pendingResult?.Finish();
                    }
                });
            }

            if (!string.IsNullOrEmpty(rawUrl) && YouTubeUtils.GetVideoIdFromUrl(rawUrl) is { } videoId2 && downloadVideo)
            {
                // Immediately update notification to show the Download action as disabled
                UpdateNotificationWithDisabledAction(context, intent, "Download");

                PendingResult? pendingResult = GoAsync();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (await AppShell.ConnectToServerSilent())
                        {
                            if ((await ServerApiClient.Instance.FindVideos(new FindVideosRequest
                            {
                                ExclusionsMode = ExclusionsMode.ShowAll,
                                VideoIds = new List<string> { videoId2 },
                                SortMode = SortMode.AgeDesc,
                                Count = int.MaxValue
                            })).FirstOrDefault() is { } video)
                            {
                                await ServerApiClient.Instance.DownloadVideo(
                                    url: rawUrl,
                                    silent: true,
                                    requestId: Guid.NewGuid().ToString(),
                                    dataDirectorySubpath: "plex",
                                    videoId: video.Id,
                                    videoName: video.Title ?? string.Empty,
                                    thumbnailUrl: video.ThumbnailUrl ?? string.Empty,
                                    channelPlaylist: video.ChannelPlaylist,
                                    channelName: video.ChannelName,
                                    idInChannelFolder: true);
                            }
                        }

                    }
                    finally
                    {
                        pendingResult?.Finish();
                    }
                });
            }
        }

        private void UpdateNotificationWithDisabledAction(Context context, Intent intent, string disabledAction)
        {
            // Extract all the data needed to rebuild the notification
            int notificationId = intent.GetIntExtra("notificationId", -1);
            string title = intent.GetStringExtra("title") ?? string.Empty;
            string body = intent.GetStringExtra("body") ?? string.Empty;
            string videoUrl = intent.GetStringExtra(Intent.ExtraText) ?? string.Empty;
            string thumbnailPath = intent.GetStringExtra("thumbnailPath") ?? string.Empty;
            string channelId = intent.GetStringExtra("channelId") ?? string.Empty;
            bool isNewVideo = intent.GetBooleanExtra("isNewVideo", false);
            bool hasProgress = intent.GetBooleanExtra("hasProgress", false);
            double progress = intent.GetDoubleExtra("progress", 0);
            string? plexRatingKey = intent.GetStringExtra("plexRatingKey");
            string? channelName = intent.GetStringExtra("channelName");

            // Call the Show method with the disabled action parameter
            AndroidNotificationHelper.Show(
                title: title,
                channelName: channelName,
                body: body,
                videoUrl: videoUrl,
                thumbnailPath: thumbnailPath,
                notificationChannelId: channelId,
                notificationId: notificationId,
                isDone: false,
                isNewVideo: isNewVideo,
                hasProgress: hasProgress,
                progress: progress,
                plexRatingKey: plexRatingKey,
                disabledAction: disabledAction
            );
        }

        private void HandleDismiss(Context context, Intent intent)
        {
            int notificationId = intent.GetIntExtra("notificationId", -1);
            AndroidX.Core.App.NotificationManagerCompat.From(context).Cancel(notificationId);
        }
    }
}
