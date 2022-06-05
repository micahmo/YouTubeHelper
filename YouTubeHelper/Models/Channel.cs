using LiteDB;
using Microsoft.Toolkit.Mvvm.ComponentModel;

namespace YouTubeHelper.Models
{
    public class Channel : ObservableObject
    {
        public Channel() : this(false) { }

        public Channel(bool nonPersistent)
        {
            if (!nonPersistent)
            {
                PropertyChanged += (_, _) => DatabaseEngine.ChannelCollection.Upsert(this);
            }
        }

        [BsonId]
        public int ObjectId
        {
            get => _objectId;
            set => SetProperty(ref _objectId, value);
        }
        private int _objectId;

        public int Index
        {
            get => _index == -1 ? ObjectId : _index;
            set => SetProperty(ref _index, value);
        }
        private int _index = -1;

        public string Identifier
        {
            get => _identifier;
            set
            {
                SetProperty(ref _identifier, value);
                VanityName = value;
            }
        }
        private string _identifier;

        public string ChannelId
        {
            get => _channelId;
            set => SetProperty(ref _channelId, value);
        }
        private string _channelId;

        public string ChannelPlaylist
        {
            get => _channelPlaylist;
            set => SetProperty(ref _channelPlaylist, value);
        }
        private string _channelPlaylist;

        public string VanityName
        {
            get => _vanityName;
            set => SetProperty(ref _vanityName, value);
        }
        private string _vanityName;

        public string Description
        {
            get => _description;
            set
            {
                SetProperty(ref _description, value);
                OnPropertyChanged(nameof(ExtendedDescription));
            }
        }
        private string _description;

        [BsonIgnore]
        public string ExtendedDescription => $"{Description} ({ChannelPlaylist})";

        [BsonIgnore]
        public int ResultIndex { get; set; } = -1;
    }
}
