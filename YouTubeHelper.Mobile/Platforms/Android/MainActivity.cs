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
                _ = AppShell.Instance?.HandleSharedLink(rawUrl);
            }

            // See if we got an intent to navigate to the queue tab
            if (intent?.GetStringExtra("navigateTo") == "queue")
            {
                _ = AppShell.Instance?.NavigateToQueueTab();
            } 
        }
    }
}
