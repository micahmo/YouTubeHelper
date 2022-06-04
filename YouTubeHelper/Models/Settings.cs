using System;
using LiteDB;
using Microsoft.Toolkit.Mvvm.ComponentModel;

namespace YouTubeHelper.Models
{
    public class Settings : ObservableObject
    {
        public Settings()
        {
            PropertyChanged += (_, __) =>
            {
                DatabaseEngine.SettingsCollection.Upsert(this);
            };
        }

        [BsonId]
        public int ObjectId
        {
            get => InstanceObjectId;
            set { }
        }

        public string YouTubeApiKey
        {
            get => _youTubeApiKey;
            set => SetProperty(ref _youTubeApiKey, value);
        }
        private string _youTubeApiKey;

        public string YtDlpPath
        {
            get => _ytDlpPath;
            set => SetProperty(ref _ytDlpPath, value);
        }
        private string _ytDlpPath;

        public string TelegramBotId
        {
            get => _telegramBotId;
            set => SetProperty(ref _telegramBotId, value);
        }
        private string _telegramBotId;

        public string TelegramApiKey
        {
            get => _telegramApiKey;
            set => SetProperty(ref _telegramApiKey, value);
        }
        private string _telegramApiKey;

        public static Settings Instance => _instance ??= DatabaseEngine.SettingsCollection.FindById(InstanceObjectId) ?? new Func<Settings>(() =>
        {
            Settings settings = new Settings();
            DatabaseEngine.SettingsCollection.Insert(settings);
            return settings;
        })();
        private static Settings _instance;

        private static int InstanceObjectId => 1;
    }
}
