using Microsoft.Toolkit.Mvvm.ComponentModel;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace YouTubeHelper.Models
{
    public class Channel : ObservableObject, IHasIdentifier<ObjectId>
    {
        public Channel() : this(false) { }

        public Channel(bool nonPersistent)
        {
            if (!nonPersistent)
            {
                PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName is not nameof(Id))
                    {
                        DatabaseEngine.ChannelCollection.Upsert<Channel, ObjectId>(this);
                    }
                };
            }
        }

        [BsonId]
        [BsonIgnoreIfDefault]
        public ObjectId Id
        {
            get => _objectId;
            set => SetProperty(ref _objectId, value);
        }
        private ObjectId _objectId;

        public int Index
        {
            get => _index;
            set => SetProperty(ref _index, value);
        }
        private int _index = int.MaxValue;

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
