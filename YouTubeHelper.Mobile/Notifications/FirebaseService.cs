using Plugin.Firebase.CloudMessaging;
using ServerStatusBot.Definitions.Api;
using YouTubeHelper.Mobile.Platforms.Android;

namespace YouTubeHelper.Mobile.Notifications
{
    /// <summary>
    /// A class used to initialize FCM. Call sometime during startup
    /// </summary>
    public class FirebaseService
    {
        public static async Task InitializeAsync()
        {
            // Retrieve the token
            string? token = await CrossFirebaseCloudMessaging.Current.GetTokenAsync();

            if (!string.IsNullOrEmpty(token))
            {
                // Register this device with the server
                await ServerApiClient.Instance.RegisterDeviceId(token);

                // Register for notifications
                // Note that this is the "old" way of handling the Notification object in the foreground, while FCM would handle the background
                // Now I have an activity set up to handle the intent, and all the info is in the Data dictionary, in both foreground and background
                // So this callback really shouldn't be handled any more
                CrossFirebaseCloudMessaging.Current.NotificationReceived += async (_, args) =>
                {
                    await MyFirebaseMessagingService.HandleNotificationData(args.Notification.Data);
                };
            }
        }
    }
}
