using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Flurl;
using Color = Android.Graphics.Color;

namespace YouTubeHelper.Mobile
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                // Set the nav bar color
                Window?.SetNavigationBarColor(Color.Firebrick);
            }

            // In case we were started with an intent, trigger that now
            OnNewIntent(Intent);
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);

            // See if we got an intent to load a video
            string? rawUrl = intent?.GetStringExtra(Intent.ExtraText);

            if (!string.IsNullOrEmpty(rawUrl))
            {
                Url url = new Url(rawUrl);

                if (url.QueryParams.FirstOrDefault(q => q.Name == "v").Value is string videoId)
                {
                    _ = AppShell.Instance?.HandleSharedLink(videoId, null);
                }
                else if (url.Authority == "youtu.be")
                {
                    _ = AppShell.Instance?.HandleSharedLink(url.PathSegments[0], null);
                }
                else if (url.Authority == "www.youtube.com" && url.PathSegments.Count >= 2 && url.PathSegments[0] == "live")
                {
                    _ = AppShell.Instance?.HandleSharedLink(url.PathSegments[1], null);
                }

                if (url.PathSegments.FirstOrDefault(p => p.StartsWith('@')) is { } channelHandle)
                {
                    _ = AppShell.Instance?.HandleSharedLink(null, channelHandle);
                }
            }

            // See if we got an intent to navigate to the queue tab
            if (intent?.GetStringExtra("navigateTo") == "queue")
            {
                AppShell.Instance?.NavigateToQueueTab();
            } 
        }
    }
}
