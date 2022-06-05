using System;
using System.Configuration;
using System.IO;
using LiteDB;
using YouTubeHelper.Models;

namespace YouTubeHelper
{
    public class DatabaseEngine
    {
        public static LiteDatabase DatabaseInstance
        {
            get
            {
                return _databaseInstance ??= new Func<LiteDatabase>(() =>
                {
                    if (!Directory.Exists(DatabaseDirectory))
                    {
                        Directory.CreateDirectory(DatabaseDirectory);
                    }

                    return new LiteDatabase(new ConnectionString(Path.Combine(DatabaseDirectory, DbName))
                    {
                        Connection = ConnectionType.Shared
                    });
                })();
            }
        }
        private static LiteDatabase _databaseInstance;


        public static void Shutdown()
        {
            _channelCollection = null;
            DatabaseInstance?.Dispose();
            _databaseInstance = null;
        }

        public static ILiteCollection<Channel> ChannelCollection => _channelCollection ??= DatabaseInstance.GetCollection<Channel>("channel");
        private static ILiteCollection<Channel> _channelCollection;

        public static ILiteCollection<Settings> SettingsCollection => _settingsCollection ??= DatabaseInstance.GetCollection<Settings>("settings");
        private static ILiteCollection<Settings> _settingsCollection;

        public static ILiteCollection<Video> ExcludedVideosCollection => _excludedVideosCollection ??= DatabaseInstance.GetCollection<Video>("excludedVideos");
        private static ILiteCollection<Video> _excludedVideosCollection;

        private const string DbName = "YTH.db";
        private static string DatabaseDirectory
        {
            get
            {
                string overrideDirectory = ConfigurationManager.AppSettings["DatabaseDirectory"];
                return string.IsNullOrEmpty(overrideDirectory)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YTH")
                    : overrideDirectory;
            }
        }
    }
}
