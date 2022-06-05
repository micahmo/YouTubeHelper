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
        public string RawUrl { get; set; }

        public ExclusionReason ExclusionReason
        {
            get => _exclusionReason;
            set => SetProperty(ref _exclusionReason, value);
        }
        private ExclusionReason _exclusionReason;

        public string ChannelPlaylist { get; set; }

        [BsonIgnore]
        public bool Excluded
        {
            get => _excluded;
            set => SetProperty(ref _excluded, value);
        }
        private bool _excluded;
    }

    public enum ExclusionReason
    {
        [Description("No exclusion reason filter")]
        None,

        [Description("Won't Watch")]
        WontWatch,

        [Description("Might Watch")]
        MightWatch,

        [Description("Watched")]
        Watched
    }

    public class ExclusionReasonExtended : EnumExtended<ExclusionReason>
    {
        public ExclusionReasonExtended(ExclusionReason exclusionReason) : base(exclusionReason) { }
    }
}
