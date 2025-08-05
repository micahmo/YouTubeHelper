using Android.App;
using Android.Content;

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
                case "dismiss":
                    await HandleDismiss(context, intent);
                    break;
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
