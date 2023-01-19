using Android.App;
using Android.OS;
using Flurl;

namespace YouTubeHelper.Mobile.Platforms.Android
{
    [Activity(Name = "com.micahmo.YouTubeHelper.ShareActivity", Theme = "@style/Maui.SplashTheme")]
    public class ShareActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            string rawUrl = Intent?.Extras.GetString("android.intent.extra.TEXT");

            if (!string.IsNullOrEmpty(rawUrl))
            {
                Url url = new Url(rawUrl);

                if (url.QueryParams.FirstOrDefault(q => q.Name == "v").Value is string videoId)
                {
                    _ = AppShell.Instance.HandleSharedLink(videoId, null);
                }
                else if (url.Authority == "youtu.be")
                {
                    _ = AppShell.Instance.HandleSharedLink(url.PathSegments[0], null);
                }

                if (url.PathSegments.FirstOrDefault(p => p.StartsWith('@')) is { } channelHandle)
                {
                    _ = AppShell.Instance.HandleSharedLink(null, channelHandle);
                }
            }
        }
    }
}
