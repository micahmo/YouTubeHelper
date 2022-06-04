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

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
        private string _name;
    }
}
