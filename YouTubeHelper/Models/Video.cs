using System;
using System.ComponentModel;
using LiteDB;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using YouTubeHelper.Properties;
using YouTubeHelper.Utilities;

namespace YouTubeHelper.Models
{
    public class Video : ObservableObject
    {
        [BsonIgnore]
        public string ThumbnailUrl { get; set; }

        [BsonIgnore]
        public string Title { get; set; }

        [BsonId]
        public string Id { get; set; }

        [BsonIgnore]
        public string Description { get; set; }

        [BsonIgnore]
        public DateTimeOffset ReleaseDate { get; set; }

        [BsonIgnore]
        public string TimeString => string.Format(Resources.TimeStringFormat, ReleaseDate, (DateTimeOffset.Now - ReleaseDate).Days, Duration);

        [BsonIgnore]
        public TimeSpan Duration { get; set; }

        [BsonIgnore]
        public string RawUrl
        {
            get => _rawUrl;
            set => SetProperty(ref _rawUrl, value);
        }
        private string _rawUrl;

        public ExclusionReason ExclusionReason
        {
            get => _exclusionReason;
            set => SetProperty(ref _exclusionReason, value);
        }
        private ExclusionReason _exclusionReason;

        [BsonIgnore]
        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }
        private string _status;

        public string ChannelPlaylist { get; set; }

        [BsonIgnore]
        public bool Excluded
        {
            get => _excluded;
            set => SetProperty(ref _excluded, value);
        }
        private bool _excluded;
    }

    [Flags]
    public enum ExclusionReason
    {
        [Description("No exclusion reason filter")]
        None = 0,

        [Description("Won't Watch")]
        WontWatch = 1,

        [Description("Might Watch")]
        MightWatch = 2,

        [Description("Watched")]
        Watched = 4,

        [Description("Not Watched")]
        NotWatched = WontWatch | MightWatch
    }

    public class ExclusionReasonExtended : EnumExtended<ExclusionReason>
    {
        public ExclusionReasonExtended(ExclusionReason exclusionReason) : base(exclusionReason) { }
    }
}
