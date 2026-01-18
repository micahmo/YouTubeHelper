using Android.Content;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Flurl.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace YouTubeHelper.Mobile
{
    internal static class Utilities
    {
        public static async Task<string?> GetCachedImagePath(string? url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return null;
            }

            string fileName = GetHashedFileName(url) + Path.GetExtension(url);
            string localPath = Path.Combine(FileSystem.CacheDirectory, fileName);

            if (File.Exists(localPath))
            {
                return localPath;
            }

            byte[] imageBytes = await url.GetBytesAsync();
            await File.WriteAllBytesAsync(localPath, imageBytes);

            return localPath;
        }

        private static string GetHashedFileName(string input)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
    }

    public class UpdateChecker
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/micahmo/youtubehelper/releases/latest";

        public async Task CheckForUpdatesAsync()
        {
            try
            {
                JsonDocument latestRelease = await GetLatestReleaseAsync();

                Version currentVersion = GetCurrentVersion();
                Version latestVersion = ParseVersionFromRelease(latestRelease);

                if (latestVersion > currentVersion)
                {
                    await ShowUpdateSnackbarAsync(latestRelease);
                }
            }
            catch
            {
                // Ignore
            }
        }

        private async Task<JsonDocument> GetLatestReleaseAsync()
        {
            string json = await GitHubApiUrl.GetStringAsync();
            return JsonDocument.Parse(json);
        }

        private Version GetCurrentVersion() => new(AppInfo.VersionString);

        private Version ParseVersionFromRelease(JsonDocument release)
        {
            JsonElement root = release.RootElement;
            string tagName = root.GetProperty("tag_name").GetString() ?? "0.0.0";

            // Remove 'v' prefix if present (e.g., "v1.2.3" -> "1.2.3")
            string versionString = tagName.TrimStart('v', 'V');
            return Version.Parse(versionString);
        }

        private async Task ShowUpdateSnackbarAsync(JsonDocument release)
        {
            Version newVersion = ParseVersionFromRelease(release);

            await MainThread.InvokeOnMainThreadAsync(async () => await Snackbar.Make(
                    $"Update available: {newVersion}",
                    OpenObtainiumAsync,
                    "Obtainium",
                    TimeSpan.FromSeconds(10),
                    visualOptions: new SnackbarOptions
                    {
                        BackgroundColor = Colors.Black,
                        TextColor = Colors.White,
                        ActionButtonTextColor = Color.FromArgb("#EF4444")
                    }).Show());
        }

        private void OpenObtainiumAsync()
        {
            try
            {
#if ANDROID
                string packageName = "dev.imranr.obtainium.fdroid";
                Intent? intent = Android.App.Application.Context.PackageManager?.GetLaunchIntentForPackage(packageName);

                if (intent != null)
                {
                    intent.SetFlags(ActivityFlags.NewTask);
                    Android.App.Application.Context.StartActivity(intent);
                }
#endif
            }
            catch
            {
                // Ignore
            }
        }
    }
}
