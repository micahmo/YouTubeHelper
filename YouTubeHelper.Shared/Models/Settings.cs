using System;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using MongoDB.Bson.Serialization.Attributes;

namespace YouTubeHelper.Shared.Models
{
    public class Settings : ObservableObject, IHasIdentifier<int>
    {
        public Settings()
        {
            PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is not nameof(Id))
                {
                    DatabaseEngine.SettingsCollection.Upsert<Settings, int>(this);
                }
            };
        }

        [BsonId]
        [BsonIgnoreIfDefault]
        public int Id
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

        public string ChromePath
        {
            get => _chromePath;
            set => SetProperty(ref _chromePath, value);
        }
        private string _chromePath;

        public string TelegramApiKey
        {
            get => _telegramApiKey;
            set => SetProperty(ref _telegramApiKey, value);
        }
        private string _telegramApiKey;

        public string TelegramApiAddress
        {
            get => _telegramApiAddress;
            set => SetProperty(ref _telegramApiAddress, value);
        }
        private string _telegramApiAddress;

        public string DownloadDirectory
        {
            get => string.IsNullOrWhiteSpace(_downloadDirectory) ? "plex" : _downloadDirectory;
            set => SetProperty(ref _downloadDirectory, value);
        }
        private string _downloadDirectory;

        public static Settings Instance => _instance ??= DatabaseEngine.SettingsCollection.FindById(InstanceObjectId) ?? new Func<Settings>(() =>
        {
            Settings settings = new Settings();
            DatabaseEngine.SettingsCollection.InsertOne(settings);
            return settings;
        })();
        private static Settings _instance;

        private static int InstanceObjectId => 1;
    }
}
