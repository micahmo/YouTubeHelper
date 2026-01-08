using _Microsoft.Android.Resource.Designer;
using Android.App;
using Android.Content;
using Android.Graphics;
using AndroidX.Core.App;
using ServerStatusBot.Definitions.Database.Models;

namespace YouTubeHelper.Mobile.Platforms.Android
{
    public static class AndroidNotificationHelper
    {
        private const string ActionNotification = "com.micahmo.youtubehelper.NOTIFICATION_ACTION";

        public static void Show(string title, string body, string? videoUrl, string? thumbnailPath, string? channelThumbnailPath, string notificationChannelId, int notificationId, bool isDone, bool isNewVideo, bool isFailed, bool hasProgress, double progress, string? plexRatingKey, string? channelName, string? disabledAction = null)
        {
            bool isDismissable = isDone || isNewVideo;

            Context context = global::Android.App.Application.Context;

#pragma warning disable IDE0059
            PendingIntent? dismissPendingIntent = null;
            PendingIntent? navigateToVideoPendingIntent = null;
            PendingIntent? navigateToQueuePendingIntent = null;
            PendingIntent? openInPlexPendingIntent = null;
            PendingIntent? launchAppPendingIntent = null;
            PendingIntent? downloadVideoPendingIntent = null;
            PendingIntent? markVideoAsWontWatchPendingIntent = null;
            PendingIntent? watchVideoPendingIntent = null;
            PendingIntent? markVideoAsMightWatchPendingIntent = null;
#pragma warning restore IDE0059

            int dismissIntentId = notificationId * 10 + 0;
            int navigateToVideoIntentId = notificationId * 10 + 1;
            int navigateToQueueIntentId = notificationId * 10 + 2;
            int openInPlexIntentId = notificationId * 10 + 3;
            int launchAppIntentId = notificationId * 10 + 4;
            int downloadVideoIntentId = notificationId * 10 + 5;
            int markVideoAsWontWatchIntentId = notificationId * 10 + 6;
            int watchVideoIntentId = notificationId * 10 + 7;
            int markVideoAsMightWatchIntentId = notificationId * 10 + 8;

            if (context.PackageName != null)
            {
                // Dismiss Action
                Intent dismissIntent = new(ActionNotification);
                _ = dismissIntent.SetPackage(context.PackageName);
                _ = dismissIntent.PutExtra("actionType", "dismiss");
                _ = dismissIntent.PutExtra("notificationId", notificationId);
                _ = dismissIntent.PutExtra("isDone", isDone);
                _ = dismissIntent.PutExtra("isFailed", isFailed);
                dismissPendingIntent = PendingIntent.GetBroadcast(
                    context,
                    dismissIntentId,
                    dismissIntent,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent
                );

                // Navigate to Video Action
                Intent navigateToVideoIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName)!;
                _ = navigateToVideoIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.ReorderToFront);
                _ = navigateToVideoIntent.PutExtra(Intent.ExtraText, videoUrl);
                _ = navigateToVideoIntent.PutExtra("notificationId", notificationId);
                _ = navigateToVideoIntent.PutExtra("isDone", isDone);
                _ = navigateToVideoIntent.PutExtra("isNewVideo", isNewVideo);
                _ = navigateToVideoIntent.PutExtra("isFailed", isFailed);
                navigateToVideoPendingIntent = PendingIntent.GetActivity(
                    context,
                    requestCode: navigateToVideoIntentId,
                    navigateToVideoIntent,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent
                );

                // Navigate to Queue Action
                Intent navigateToQueueIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName)!;
                _ = navigateToQueueIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.ReorderToFront);
                _ = navigateToQueueIntent.PutExtra("navigateTo", "queue");
                _ = navigateToQueueIntent.PutExtra("notificationId", notificationId);
                _ = navigateToQueueIntent.PutExtra("isDone", isDone);
                _ = navigateToQueueIntent.PutExtra("isFailed", isFailed);
                navigateToQueuePendingIntent = PendingIntent.GetActivity(
                    context,
                    requestCode: navigateToQueueIntentId,
                    navigateToQueueIntent,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent
                );

                // Open In Plex Action
                Intent openInPlexIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName)!;
                _ = openInPlexIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.ReorderToFront);
                _ = openInPlexIntent.PutExtra("plexRatingKey", plexRatingKey);
                _ = openInPlexIntent.PutExtra("notificationId", notificationId);
                _ = openInPlexIntent.PutExtra("isDone", isDone);
                _ = openInPlexIntent.PutExtra("isFailed", isFailed);
                openInPlexPendingIntent = PendingIntent.GetActivity(
                    context,
                    openInPlexIntentId,
                    openInPlexIntent,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent
                );

                // Just launch app
                Intent launchAppIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName)!;
                _ = launchAppIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.ReorderToFront);
#pragma warning disable IDE0059
                launchAppPendingIntent = PendingIntent.GetActivity(
                    context,
                    launchAppIntentId,
                    launchAppIntent,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent
                );
#pragma warning restore IDE0059

                // Download Video Action
                Intent downloadVideoIntent = new(ActionNotification);
                _ = downloadVideoIntent.SetPackage(context.PackageName);
                _ = downloadVideoIntent.PutExtra(Intent.ExtraText, videoUrl);
                _ = downloadVideoIntent.PutExtra("downloadVideo", true);
                _ = downloadVideoIntent.PutExtra("notificationId", notificationId);
                _ = downloadVideoIntent.PutExtra("isDone", isDone);
                _ = downloadVideoIntent.PutExtra("isNewVideo", isNewVideo);
                _ = downloadVideoIntent.PutExtra("isFailed", isFailed);
                // Add extra data needed to rebuild notification
                _ = downloadVideoIntent.PutExtra("title", title);
                _ = downloadVideoIntent.PutExtra("body", body);
                _ = downloadVideoIntent.PutExtra("thumbnailPath", thumbnailPath);
                _ = downloadVideoIntent.PutExtra("channelThumbnailPath", channelThumbnailPath);
                _ = downloadVideoIntent.PutExtra("channelId", notificationChannelId);
                _ = downloadVideoIntent.PutExtra("hasProgress", hasProgress);
                _ = downloadVideoIntent.PutExtra("progress", progress);
                _ = downloadVideoIntent.PutExtra("plexRatingKey", plexRatingKey);
                _ = downloadVideoIntent.PutExtra("channelName", channelName);
                downloadVideoPendingIntent = PendingIntent.GetBroadcast(
                    context,
                    downloadVideoIntentId,
                    downloadVideoIntent,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent
                );

                // Mark Video as Won't Watch Action
                Intent markVideoAsWontWatchIntent = new(ActionNotification);
                _ = markVideoAsWontWatchIntent.SetPackage(context.PackageName);
                _ = markVideoAsWontWatchIntent.PutExtra(Intent.ExtraText, videoUrl);
                _ = markVideoAsWontWatchIntent.PutExtra("markVideo", ExclusionReason.WontWatch.ToString());
                _ = markVideoAsWontWatchIntent.PutExtra("notificationId", notificationId);
                _ = markVideoAsWontWatchIntent.PutExtra("isDone", isDone);
                _ = markVideoAsWontWatchIntent.PutExtra("isNewVideo", isNewVideo);
                _ = markVideoAsWontWatchIntent.PutExtra("isFailed", isFailed);
                // Add extra data needed to rebuild notification
                _ = markVideoAsWontWatchIntent.PutExtra("title", title);
                _ = markVideoAsWontWatchIntent.PutExtra("body", body);
                _ = markVideoAsWontWatchIntent.PutExtra("thumbnailPath", thumbnailPath);
                _ = markVideoAsWontWatchIntent.PutExtra("channelThumbnailPath", channelThumbnailPath);
                _ = markVideoAsWontWatchIntent.PutExtra("channelId", notificationChannelId);
                _ = markVideoAsWontWatchIntent.PutExtra("hasProgress", hasProgress);
                _ = markVideoAsWontWatchIntent.PutExtra("progress", progress);
                _ = markVideoAsWontWatchIntent.PutExtra("plexRatingKey", plexRatingKey);
                _ = markVideoAsWontWatchIntent.PutExtra("channelName", channelName);
                markVideoAsWontWatchPendingIntent = PendingIntent.GetBroadcast(
                    context,
                    markVideoAsWontWatchIntentId,
                    markVideoAsWontWatchIntent,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent
                );

                // Watch Video Action
                Intent watchVideoIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName)!;
                _ = watchVideoIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.ReorderToFront);
                _ = watchVideoIntent.PutExtra(Intent.ExtraText, videoUrl);
                _ = watchVideoIntent.PutExtra("watchVideo", true);
                _ = watchVideoIntent.PutExtra("notificationId", notificationId);
                _ = watchVideoIntent.PutExtra("isNewVideo", isNewVideo);
#pragma warning disable IDE0059
                watchVideoPendingIntent = PendingIntent.GetActivity(
                    context,
                    watchVideoIntentId,
                    watchVideoIntent,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent
                );
#pragma warning restore IDE0059

                // Mark Video as Might Watch Action
                Intent markVideoAsMightWatchIntent = new(ActionNotification);
                _ = markVideoAsMightWatchIntent.SetPackage(context.PackageName);
                _ = markVideoAsMightWatchIntent.PutExtra(Intent.ExtraText, videoUrl);
                _ = markVideoAsMightWatchIntent.PutExtra("markVideo", ExclusionReason.MightWatch.ToString());
                _ = markVideoAsMightWatchIntent.PutExtra("notificationId", notificationId);
                _ = markVideoAsMightWatchIntent.PutExtra("isDone", isDone);
                _ = markVideoAsMightWatchIntent.PutExtra("isNewVideo", isNewVideo);
                _ = markVideoAsMightWatchIntent.PutExtra("isFailed", isFailed);
                // Add extra data needed to rebuild notification
                _ = markVideoAsMightWatchIntent.PutExtra("title", title);
                _ = markVideoAsMightWatchIntent.PutExtra("body", body);
                _ = markVideoAsMightWatchIntent.PutExtra("thumbnailPath", thumbnailPath);
                _ = markVideoAsMightWatchIntent.PutExtra("channelThumbnailPath", channelThumbnailPath);
                _ = markVideoAsMightWatchIntent.PutExtra("channelId", notificationChannelId);
                _ = markVideoAsMightWatchIntent.PutExtra("hasProgress", hasProgress);
                _ = markVideoAsMightWatchIntent.PutExtra("progress", progress);
                _ = markVideoAsMightWatchIntent.PutExtra("plexRatingKey", plexRatingKey);
                _ = markVideoAsMightWatchIntent.PutExtra("channelName", channelName);
                markVideoAsMightWatchPendingIntent = PendingIntent.GetBroadcast(
                    context,
                    markVideoAsMightWatchIntentId,
                    markVideoAsMightWatchIntent,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent
                );
            }

            Bitmap? videoBitmap = BitmapFactory.DecodeFile(thumbnailPath);
            Bitmap? channelBitmap = BitmapFactory.DecodeFile(channelThumbnailPath);

            NotificationCompat.BigPictureStyle bigPictureStyle = new NotificationCompat.BigPictureStyle()
                .BigPicture(videoBitmap)
                .BigLargeIcon((Bitmap?)null)
                .SetBigContentTitle(title)
                .SetSummaryText(body);

            NotificationCompat.Builder builder = new NotificationCompat.Builder(context, channelId: notificationChannelId)
                .SetContentTitle(title)
                .SetContentText(body)
                .SetSubText(channelName)
                .SetStyle(bigPictureStyle)
                .SetSmallIcon(ResourceConstant.Drawable.notification_icon)
                .SetLargeIcon(channelBitmap)
                .SetOngoing(!isDismissable)
                .SetAutoCancel(isDismissable);

            // Set notification tap action
            _ = builder.SetContentIntent(navigateToVideoPendingIntent!);

            // Set additional actions
            if (isNewVideo)
            {
                // If an action is disabled, set its PendingIntent to null
                _ = builder.AddAction(ResourceConstant.Drawable.abc_ab_share_pack_mtrl_alpha, "Won't Watch", disabledAction == "WontWatch" ? null : markVideoAsWontWatchPendingIntent);
                _ = builder.AddAction(ResourceConstant.Drawable.abc_ab_share_pack_mtrl_alpha, "Might Watch", disabledAction == "MightWatch" ? null : markVideoAsMightWatchPendingIntent);
                _ = builder.AddAction(ResourceConstant.Drawable.abc_ab_share_pack_mtrl_alpha, "Download", disabledAction == "Download" ? null : downloadVideoPendingIntent);
            }
            else
            {
                _ = builder.AddAction(ResourceConstant.Drawable.abc_ab_share_pack_mtrl_alpha, "Queue", navigateToQueuePendingIntent);
            }

            if (!string.IsNullOrEmpty(plexRatingKey))
            {
                _ = builder.AddAction(ResourceConstant.Drawable.abc_ab_share_pack_mtrl_alpha, "Open in Plex", openInPlexPendingIntent);
            }

            if (isFailed)
            {
                _ = builder.AddAction(ResourceConstant.Drawable.abc_ab_share_pack_mtrl_alpha, "Re-download", disabledAction == "Download" ? null : downloadVideoPendingIntent);
            }

            if (isDone)
            {
                _ = builder.AddAction(ResourceConstant.Drawable.abc_ab_share_pack_mtrl_alpha, "Dismiss", dismissPendingIntent);
            }

            if (hasProgress)
            {
                _ = builder.SetProgress(max: 100, progress: (int)progress, false);
            }

            NotificationManagerCompat.From(context).Notify(notificationId, builder.Build());
        }
    }
}
