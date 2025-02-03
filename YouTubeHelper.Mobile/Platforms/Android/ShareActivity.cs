using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace YouTubeHelper.Mobile.Platforms.Android
{
    // Inherit from Android Activity instead of MAUI activity so we don't run into the issue where MAUI doesn't support >1 activity
    [Activity(Name = "com.micahmo.YouTubeHelper.ShareActivity", Theme = "@style/Maui.SplashTheme", LaunchMode = LaunchMode.SingleTop)]
    public class ShareActivity : Activity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Extract shared text from intent
            string? rawUrl = Intent?.GetStringExtra(Intent.ExtraText);

            if (!string.IsNullOrEmpty(rawUrl))
            {
                // Forward the data to MainActivity
                Intent mainIntent = new Intent(this, typeof(MainActivity));
                mainIntent.SetAction(Intent.ActionView);
                mainIntent.PutExtra(Intent.ExtraText, rawUrl);
                mainIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop);
                StartActivity(mainIntent);
            }

            // Close ShareActivity after forwarding the intent
            Finish();
        }
    }
}
