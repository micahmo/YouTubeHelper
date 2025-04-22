using _Microsoft.Android.Resource.Designer;
using Android.Content;
using Android.Graphics;
using AndroidX.Core.App;

namespace YouTubeHelper.Mobile.Platforms.Android
{
    public static class AndroidNotificationHelper
    {
        public static void Show(string title, string body, string? thumbnailPath, string channelId, int notificationId, bool isDone, bool hasProgress, double progress)
        {
            Context context = global::Android.App.Application.Context;

            Bitmap? bitmap = BitmapFactory.DecodeFile(thumbnailPath);

            var builder = new NotificationCompat.Builder(context, channelId: isDone ? "completion" : "progress")
                .SetContentTitle(title)
                .SetContentText(body)
                .SetSmallIcon(ResourceConstant.Drawable.notification_icon)
                .SetLargeIcon(bitmap)
                .SetOngoing(false)
                .SetAutoCancel(false);

            if (hasProgress)
            {
                builder.SetProgress(max: 100, progress: (int)progress, false);
            }

            NotificationManagerCompat.From(context).Notify(notificationId, builder.Build());
        }
    }
}