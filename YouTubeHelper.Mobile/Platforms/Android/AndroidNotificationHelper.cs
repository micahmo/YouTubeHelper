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

        public static void Show(string title, string body, string? videoUrl, string? thumbnailPath, string notificationChannelId, int notificationId, bool isDone, bool isNewVideo, bool hasProgress, double progress, string? plexRatingKey, string? disabledAction = null)
        {
            bool isDismissable = isDone || isNewVideo;

            Context context = global::Android.App.Application.Context;

            PendingIntent? dismissPendingIntent = null;
            PendingIntent? navigateToVideoPendingIntent = null;
            PendingIntent? navigateToQueuePendingIntent = null;
            PendingIntent? openInPlexPendingIntent = null;
            PendingIntent? launchAppPendingIntent = null;
            PendingIntent? downloadVideoPendingIntent = null;
            PendingIntent? markVideoAsWontWatchPendingIntent = null;
            PendingIntent? watchVideoPendingIntent = null;
            PendingIntent? markVideoAsMightWatchPendingIntent = null;

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
                Intent dismissIntent = new Intent(ActionNotification);
                dismissIntent.SetPackage(context.PackageName);
                dismissIntent.PutExtra("actionType", "dismiss");
                dismissIntent.PutExtra("notificationId", notificationId);
                dismissIntent.PutExtra("isDone", isDone);
                dismissPendingIntent = PendingIntent.GetBroadcast(
                    context,
                    dismissIntentId,
                    dismissIntent,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent
                );

                // Navigate to Video Action
                Intent navigateToVideoIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName)!;
                navigateToVideoIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.ReorderToFront);
                navigateToVideoIntent.PutExtra(Intent.ExtraText, videoUrl);
                navigateToVideoIntent.PutExtra("notificationId", notificationId);
                navigateToVideoIntent.PutExtra("isDone", isDone);
                navigateToVideoIntent.PutExtra("isNewVideo", isNewVideo);
                navigateToVideoPendingIntent = PendingIntent.GetActivity(
                    context,
                    requestCode: navigateToVideoIntentId,
                    navigateToVideoIntent,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent
                );

                // Navigate to Queue Action
                Intent navigateToQueueIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName)!;
                navigateToQueueIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.ReorderToFront);
                navigateToQueueIntent.PutExtra("navigateTo", "queue");
                navigateToQueueIntent.PutExtra("notificationId", notificationId);
                navigateToQueueIntent.PutExtra("isDone", isDone);
                navigateToQueuePendingIntent = PendingIntent.GetActivity(
                    context,
                    requestCode: navigateToQueueIntentId,
                    navigateToQueueIntent,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent
                );

                // Open In Plex Action
                Intent openInPlexIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName)!;
                openInPlexIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.ReorderToFront);
                openInPlexIntent.PutExtra("plexRatingKey", plexRatingKey);
                openInPlexIntent.PutExtra("notificationId", notificationId);
                openInPlexIntent.PutExtra("isDone", isDone);
                openInPlexPendingIntent = PendingIntent.GetActivity(
                    context,
                    openInPlexIntentId,
                    openInPlexIntent,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent
                );

                // Just launch app
                Intent launchAppIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName)!;
                launchAppIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.ReorderToFront);
                launchAppPendingIntent = PendingIntent.GetActivity(
                    context,
                    launchAppIntentId,
                    launchAppIntent,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent
                );

                // Download Video Action
                Intent downloadVideoIntent = new Intent(ActionNotification);
                downloadVideoIntent.SetPackage(context.PackageName);
                downloadVideoIntent.PutExtra(Intent.ExtraText, videoUrl);
                downloadVideoIntent.PutExtra("downloadVideo", true);
                downloadVideoIntent.PutExtra("notificationId", notificationId);
                downloadVideoIntent.PutExtra("isNewVideo", isNewVideo);
                // Add extra data needed to rebuild notification
                downloadVideoIntent.PutExtra("title", title);
                downloadVideoIntent.PutExtra("body", body);
                downloadVideoIntent.PutExtra("thumbnailPath", thumbnailPath);
                downloadVideoIntent.PutExtra("channelId", notificationChannelId);
                downloadVideoIntent.PutExtra("hasProgress", hasProgress);
                downloadVideoIntent.PutExtra("progress", progress);
                downloadVideoIntent.PutExtra("plexRatingKey", plexRatingKey);
                downloadVideoPendingIntent = PendingIntent.GetBroadcast(
                    context,
                    downloadVideoIntentId,
                    downloadVideoIntent,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent
                );

                // Mark Video as Won't Watch Action
                Intent markVideoAsWontWatchIntent = new Intent(ActionNotification);
                markVideoAsWontWatchIntent.SetPackage(context.PackageName);
                markVideoAsWontWatchIntent.PutExtra(Intent.ExtraText, videoUrl);
                markVideoAsWontWatchIntent.PutExtra("markVideo", ExclusionReason.WontWatch.ToString());
                markVideoAsWontWatchIntent.PutExtra("notificationId", notificationId);
                markVideoAsWontWatchIntent.PutExtra("isNewVideo", isNewVideo);
                // Add extra data needed to rebuild notification
                markVideoAsWontWatchIntent.PutExtra("title", title);
                markVideoAsWontWatchIntent.PutExtra("body", body);
                markVideoAsWontWatchIntent.PutExtra("thumbnailPath", thumbnailPath);
                markVideoAsWontWatchIntent.PutExtra("channelId", notificationChannelId);
                markVideoAsWontWatchIntent.PutExtra("hasProgress", hasProgress);
                markVideoAsWontWatchIntent.PutExtra("progress", progress);
                markVideoAsWontWatchIntent.PutExtra("plexRatingKey", plexRatingKey);
                markVideoAsWontWatchPendingIntent = PendingIntent.GetBroadcast(
                    context,
                    markVideoAsWontWatchIntentId,
                    markVideoAsWontWatchIntent,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent
                );

                // Watch Video Action
                Intent watchVideoIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName)!;
                watchVideoIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.ReorderToFront);
                watchVideoIntent.PutExtra(Intent.ExtraText, videoUrl);
                watchVideoIntent.PutExtra("watchVideo", true);
                watchVideoIntent.PutExtra("notificationId", notificationId);
                watchVideoIntent.PutExtra("isNewVideo", isNewVideo);
                watchVideoPendingIntent = PendingIntent.GetActivity(
                    context,
                    watchVideoIntentId,
                    watchVideoIntent,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent
                );

                // Mark Video as Might Watch Action
                Intent markVideoAsMightWatchIntent = new Intent(ActionNotification);
                markVideoAsMightWatchIntent.SetPackage(context.PackageName);
                markVideoAsMightWatchIntent.PutExtra(Intent.ExtraText, videoUrl);
                markVideoAsMightWatchIntent.PutExtra("markVideo", ExclusionReason.MightWatch.ToString());
                markVideoAsMightWatchIntent.PutExtra("notificationId", notificationId);
                markVideoAsMightWatchIntent.PutExtra("isNewVideo", isNewVideo);
                // Add extra data needed to rebuild notification
                markVideoAsMightWatchIntent.PutExtra("title", title);
                markVideoAsMightWatchIntent.PutExtra("body", body);
                markVideoAsMightWatchIntent.PutExtra("thumbnailPath", thumbnailPath);
                markVideoAsMightWatchIntent.PutExtra("channelId", notificationChannelId);
                markVideoAsMightWatchIntent.PutExtra("hasProgress", hasProgress);
                markVideoAsMightWatchIntent.PutExtra("progress", progress);
                markVideoAsMightWatchIntent.PutExtra("plexRatingKey", plexRatingKey);
                markVideoAsMightWatchPendingIntent = PendingIntent.GetBroadcast(
                    context,
                    markVideoAsMightWatchIntentId,
                    markVideoAsMightWatchIntent,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent
                );
            }

            Bitmap? bitmap = BitmapFactory.DecodeFile(thumbnailPath);

            NotificationCompat.Builder builder = new NotificationCompat.Builder(context, channelId: notificationChannelId)
                .SetContentTitle(title)
                .SetContentText(body)
                .SetSmallIcon(ResourceConstant.Drawable.notification_icon)
                .SetLargeIcon(bitmap)
                .SetOngoing(!isDismissable)
                .SetAutoCancel(isDismissable);

            // Set notification tap action
            if (isNewVideo)
            {
                builder.SetContentIntent(navigateToVideoPendingIntent!);
            }
            else if (string.IsNullOrEmpty(plexRatingKey))
            {
                builder.SetContentIntent(launchAppPendingIntent!);
            }
            else
            {
                builder.SetContentIntent(openInPlexPendingIntent);
            }

            // Set additional actions
            if (isNewVideo)
            {
                // If an action is disabled, set its PendingIntent to null
                builder.AddAction(ResourceConstant.Drawable.abc_ab_share_pack_mtrl_alpha, "Won't Watch", disabledAction == "WontWatch" ? null : markVideoAsWontWatchPendingIntent);
                builder.AddAction(ResourceConstant.Drawable.abc_ab_share_pack_mtrl_alpha, "Might Watch", disabledAction == "MightWatch" ? null : markVideoAsMightWatchPendingIntent);
                builder.AddAction(ResourceConstant.Drawable.abc_ab_share_pack_mtrl_alpha, "Download", disabledAction == "Download" ? null : downloadVideoPendingIntent);
            }
            else
            {
                builder.AddAction(ResourceConstant.Drawable.abc_ab_share_pack_mtrl_alpha, "Video", navigateToVideoPendingIntent);
                builder.AddAction(ResourceConstant.Drawable.abc_ab_share_pack_mtrl_alpha, "Queue", navigateToQueuePendingIntent);
            }

            if (isDone)
            {
                builder.AddAction(ResourceConstant.Drawable.abc_ab_share_pack_mtrl_alpha, "Dismiss", dismissPendingIntent);
            }

            if (hasProgress)
            {
                builder.SetProgress(max: 100, progress: (int)progress, false);
            }

            NotificationManagerCompat.From(context).Notify(notificationId, builder.Build());
        }
    }
}