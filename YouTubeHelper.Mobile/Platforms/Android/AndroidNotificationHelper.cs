using _Microsoft.Android.Resource.Designer;
using Android.App;
using Android.Content;
using Android.Graphics;
using AndroidX.Core.App;

namespace YouTubeHelper.Mobile.Platforms.Android
{
    public static class AndroidNotificationHelper
    {
        private const string ActionNotification = "com.micahmo.youtubehelper.NOTIFICATION_ACTION";

        public static void Show(string title, string body, string? videoUrl, string? thumbnailPath, string channelId, int notificationId, bool isDone, bool hasProgress, double progress)
        {
            Context context = global::Android.App.Application.Context;

            PendingIntent? dismissPendingIntent = null;
            PendingIntent? navigateToVideoPendingIntent = null;
            PendingIntent? navigateToQueuePendingIntent = null;
            PendingIntent? openInPlexPendingIntent = null;
            PendingIntent? launchPendingIntent = null;

            int dismissIntentId = notificationId * 10 + 0;
            int navigateToVideoIntentId = notificationId * 10 + 1;
            int navigateToQueueIntentId = notificationId * 10 + 2;
            int openInPlexIntentId = notificationId * 10 + 3;
            int launchIntentId = notificationId * 10 + 4;

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
                Intent openInPlexIntent = new Intent(ActionNotification);
                openInPlexIntent.SetPackage(context.PackageName);
                openInPlexIntent.PutExtra("actionType", "openInPlex");
                openInPlexIntent.PutExtra("videoTitle", title);
                openInPlexIntent.PutExtra("videoUrl", videoUrl);
                openInPlexIntent.PutExtra("notificationId", notificationId);
                openInPlexIntent.PutExtra("isDone", isDone);
                openInPlexPendingIntent = PendingIntent.GetBroadcast(
                    context,
                    openInPlexIntentId,
                    openInPlexIntent,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent
                );

                // Just launch app
                Intent launchIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName)!;
                launchIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.ReorderToFront);
                launchPendingIntent = PendingIntent.GetActivity(
                    context,
                    launchIntentId,
                    launchIntent,
                    PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent
                );
            }

            Bitmap? bitmap = BitmapFactory.DecodeFile(thumbnailPath);

            NotificationCompat.Builder builder = new NotificationCompat.Builder(context, channelId: isDone ? "completion" : "progress")
                .SetContentTitle(title)
                .SetContentText(body)
                .SetSmallIcon(ResourceConstant.Drawable.notification_icon)
                .SetLargeIcon(bitmap)
                .SetOngoing(!isDone)
                .SetAutoCancel(isDone)
                .SetContentIntent(launchPendingIntent!)
                .AddAction(ResourceConstant.Drawable.abc_ab_share_pack_mtrl_alpha, "Video", navigateToVideoPendingIntent)
                .AddAction(ResourceConstant.Drawable.abc_ab_share_pack_mtrl_alpha, "Queue", navigateToQueuePendingIntent);

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