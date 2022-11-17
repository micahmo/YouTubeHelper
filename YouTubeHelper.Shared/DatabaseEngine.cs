using System;
using MongoDB.Driver;
using YouTubeHelper.Shared.Models;

namespace YouTubeHelper.Shared
{
    public class DatabaseEngine
    {
        public static string ConnectionString { get; set; }

        private static IMongoDatabase DatabaseInstance
        {
            get
            {
                return _databaseInstance ??= new Func<IMongoDatabase>(() =>
                {
                    var clientSettings = MongoClientSettings.FromConnectionString(ConnectionString);
                    clientSettings.AllowInsecureTls = ConnectionString.Contains("tlsAllowInvalidCertificates=true");
                    MongoClient mongoClient = new MongoClient(clientSettings);
                    return mongoClient.GetDatabase("yth");
                })();
            }
        }
        private static IMongoDatabase _databaseInstance;

        public static string TestConnection()
        {
            try
            {
                DatabaseInstance.ListCollectionNames();
                return default;
            }
            catch (Exception ex)
            {
                _databaseInstance = null;
                return ex.Message;
            }
        }

        public static void Reset()
        {
            _databaseInstance = null;
        }

        public static IMongoCollection<Channel> ChannelCollection => _channelCollection ??= DatabaseInstance.GetCollection<Channel>("channel");
        private static IMongoCollection<Channel> _channelCollection;

        public static IMongoCollection<Settings> SettingsCollection => _settingsCollection ??= DatabaseInstance.GetCollection<Settings>("settings");
        private static IMongoCollection<Settings> _settingsCollection;

        public static IMongoCollection<Video> ExcludedVideosCollection => _excludedVideosCollection ??= DatabaseInstance.GetCollection<Video>("excludedVideos");
        private static IMongoCollection<Video> _excludedVideosCollection;

        public static IMongoCollection<Video> KnownVideosCollection => _knownVideosCollection ??= DatabaseInstance.GetCollection<Video>("knownVideos");
        private static IMongoCollection<Video> _knownVideosCollection;
    }
}
