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
                PropertyChanged += (_, __) => DatabaseEngine.ChannelCollection.Upsert(this);
            }
        }

        [BsonId]
        public int ObjectId
        {
            get => _objectId;
            set => SetProperty(ref _objectId, value);
        }
        private int _objectId;

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
    }
}
