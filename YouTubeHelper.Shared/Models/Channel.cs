using Microsoft.Toolkit.Mvvm.ComponentModel;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using MongoDBHelpers;

namespace YouTubeHelper.Shared.Models
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
#pragma warning disable CS0618
                        DatabaseCollections.ChannelCollection.Upsert<Channel, ObjectId>(this);
#pragma warning restore CS0618
                    }
                };
            }
        }

        [BsonId]
        [BsonIgnoreIfDefault]
        public ObjectId Id
        {
            get => _objectId;
            init => SetProperty(ref _objectId, value);
        }
        private readonly ObjectId _objectId;

        public int Index
        {
            get => _index;
            set => SetProperty(ref _index, value);
        }
        private int _index = int.MaxValue;

        public string? Identifier
        {
            get => _identifier;
            set
            {
                SetProperty(ref _identifier, value);
                VanityName = value;
            }
        }
        private string? _identifier;

        public string? ChannelId
        {
            get => _channelId;
            set => SetProperty(ref _channelId, value);
        }
        private string? _channelId;

        public string? ChannelPlaylist
        {
            get => _channelPlaylist;
            set => SetProperty(ref _channelPlaylist, value);
        }
        private string? _channelPlaylist;

        public string? VanityName
        {
            get => _vanityName;
            set => SetProperty(ref _vanityName, value);
        }
        private string? _vanityName;

        public string? Description
        {
            get => _description;
            set
            {
                SetProperty(ref _description, value);
                OnPropertyChanged(nameof(ExtendedDescription));
            }
        }
        private string? _description;

        [BsonIgnore]
        public string ExtendedDescription => $"{Description} ({ChannelPlaylist})";

        public DateTime? DateRangeLimit
        {
            get => _dateRangeLimit;
            set => SetProperty(ref _dateRangeLimit, value);
        }
        private DateTime? _dateRangeLimit;

        public bool EnableDateRangeLimit
        {
            get => _enableDateRangeLimit;
            set => SetProperty(ref _enableDateRangeLimit, value);
        }
        private bool _enableDateRangeLimit;

        [BsonIgnore]
        public double? VideoLengthMinimumInSeconds
        {
            get => _videoLengthMinimum?.TotalSeconds;
            set => VideoLengthMinimum = value.HasValue ? TimeSpan.FromSeconds(value.Value) : null;
        }

        public TimeSpan? VideoLengthMinimum
        {
            get => _videoLengthMinimum;
            set => SetProperty(ref _videoLengthMinimum, value);
        }
        private TimeSpan? _videoLengthMinimum;

        public bool EnableVideoLengthMinimum
        {
            get => _enableVideoLengthMinimum;
            set => SetProperty(ref _enableVideoLengthMinimum, value);
        }
        private bool _enableVideoLengthMinimum;
    }
}
