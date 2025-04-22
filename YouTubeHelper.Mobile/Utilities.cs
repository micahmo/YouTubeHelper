using System.Security.Cryptography;
using System.Text;
using Flurl.Http;

namespace YouTubeHelper.Mobile
{
    internal static class Utilities
    {
        public static async Task<string?> GetCachedImagePath(string? url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            
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
}
