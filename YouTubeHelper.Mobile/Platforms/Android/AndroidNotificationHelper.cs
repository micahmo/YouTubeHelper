using _Microsoft.Android.Resource.Designer;
using Android.App;
using Android.Content;
using Android.Graphics;
using AndroidX.Core.App;

namespace YouTubeHelper.Mobile.Platforms.Android
{
    public static class AndroidNotificationHelper
    {
        public static void Show(string title, string body, string? videoUrl, string? thumbnailPath, string channelId, int notificationId, bool isDone, bool hasProgress, double progress)
        {
            Context context = global::Android.App.Application.Context;

            PendingIntent? pendingIntent = null;
            if (context.PackageName != null)
            {
                Intent intent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName)!;
                intent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.ReorderToFront);

                if (isDone)
                {
                    intent.PutExtra(Intent.ExtraText, videoUrl);
                }
                else
                {
                    intent.PutExtra("navigateTo", "queue");
                }

                pendingIntent = PendingIntent.GetActivity(
                    context,
                    requestCode: 0,
                    intent,
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
                .SetContentIntent(pendingIntent);

            if (hasProgress)
            {
                builder.SetProgress(max: 100, progress: (int)progress, false);
            }

            NotificationManagerCompat.From(context).Notify(notificationId, builder.Build());
        }
    }
}