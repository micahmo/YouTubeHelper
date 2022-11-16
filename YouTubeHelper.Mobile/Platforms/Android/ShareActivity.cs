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
                    _ = AppShell.Instance.HandleSharedVideoId(videoId);
                }
                else if (url.Authority == "youtu.be")
                {
                    _ = AppShell.Instance.HandleSharedVideoId(url.PathSegments[0]);
                }
            }
        }
    }
}
