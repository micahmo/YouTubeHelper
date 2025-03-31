using MongoDB.Driver;
using MongoDBHelpers;
using YouTubeHelper.Shared.Models;

namespace YouTubeHelper.Shared
{
    public class DatabaseCollections
    {
        public static IMongoCollection<Channel> ChannelCollection => _channelCollection ??= DatabaseEngine.DatabaseInstance.GetCollection<Channel>("channel");
        private static IMongoCollection<Channel>? _channelCollection;

        public static IMongoCollection<Settings> SettingsCollection => _settingsCollection ??= DatabaseEngine.DatabaseInstance.GetCollection<Settings>("settings");
        private static IMongoCollection<Settings>? _settingsCollection;

        public static IMongoCollection<Video> ExcludedVideosCollection => _excludedVideosCollection ??= DatabaseEngine.DatabaseInstance.GetCollection<Video>("excludedVideos");
        private static IMongoCollection<Video>? _excludedVideosCollection;

        public static IMongoCollection<Video> KnownVideosCollection => _knownVideosCollection ??= DatabaseEngine.DatabaseInstance.GetCollection<Video>("knownVideos");
        private static IMongoCollection<Video>? _knownVideosCollection;
    }
}
