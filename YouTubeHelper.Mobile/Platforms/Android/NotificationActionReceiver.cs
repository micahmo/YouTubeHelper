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

                        HandleDismiss(context, intent);

                    }
                    finally
                    {
                        pendingResult?.Finish();
                    }
                });
            }
        }

        private void HandleDismiss(Context context, Intent intent)
        {
            int notificationId = intent.GetIntExtra("notificationId", -1);
            AndroidX.Core.App.NotificationManagerCompat.From(context).Cancel(notificationId);
        }
    }
}
