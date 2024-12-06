using Android.App;
using Android.OS;
using Flurl;
using Color = Android.Graphics.Color;

namespace YouTubeHelper.Mobile.Platforms.Android
{
    [Activity(Name = "com.micahmo.YouTubeHelper.ShareActivity", Theme = "@style/Maui.SplashTheme")]
    public class ShareActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                // Set the nav bar color
                Window?.SetNavigationBarColor(Color.Firebrick);
            }

            string? rawUrl = Intent?.Extras?.GetString("android.intent.extra.TEXT");

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
        }
    }
}
