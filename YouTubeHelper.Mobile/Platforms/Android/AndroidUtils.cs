using Android.Content;
using ServerStatusBot.Definitions.Api;

namespace YouTubeHelper.Mobile.Platforms.Android
{
    public static class AndroidUtils
    {
        public static void DismissNotification(Context context, int notificationId, bool broadcast = true)
        {
            AndroidX.Core.App.NotificationManagerCompat.From(context).Cancel(notificationId);

            if (broadcast)
            {
                _ = Task.Run(async () =>
                {
                    if (await AppShell.ConnectToServerSilent())
                    {
                        await ServerApiClient.Instance.DismissNotification(notificationId.ToString(), AppShell.ClientId);
                    }
                });
            }
        }
    }
}
