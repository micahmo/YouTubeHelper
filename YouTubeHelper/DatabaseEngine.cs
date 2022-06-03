using System;
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
                    if (!Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolderName)))
                    {
                        Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolderName));
                    }

                    return new LiteDatabase(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolderName, DbName));
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

        private const string DbName = "YTH.db";
        private const string AppDataFolderName = "YTH";
    }
}
