using System;
using System.ComponentModel;
using Humanizer;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace YouTubeHelper.Shared.Models
{
    public class Video : ObservableObject, IHasIdentifier<string>
    {
        [BsonIgnore]
        public string ThumbnailUrl { get; set; }

        [BsonIgnore]
        public string Title { get; set; }

        [BsonId]
        [BsonIgnoreIfDefault]
        public string Id { get; set; }

        [BsonIgnore]
        public string Description { get; set; }

        [BsonIgnore]
        public DateTimeOffset ReleaseDate { get; set; }

        private string GetAgeString()
        {
            DateTimeOffset now = DateTimeOffset.Now;
            TimeSpan ago = now - ReleaseDate;
            // https://stackoverflow.com/a/4127477/4206279
            int years = now.Year - ReleaseDate.Year - 1 + (now.Month > ReleaseDate.Month || (now.Month == ReleaseDate.Month && now.Day >= ReleaseDate.Day) ? 1 : 0);
            int positiveYearDay = now.DayOfYear - ReleaseDate.DayOfYear;
            int negativeYearDayDif = (new DateTimeOffset(now.Year - years, now.Month, now.Day, now.Hour, now.Minute, now.Second, TimeZoneInfo.Local.BaseUtcOffset) - ReleaseDate).Days;

            if (ago.Days < 28)
            {
                return ReleaseDate.Humanize();
            }

            if (ago.Days < 365)
            {
                return $"{"day".ToQuantity(ago.Days)} ago";
            }

            if (positiveYearDay > 0)
            {
                return $"{"year".ToQuantity(years)}, {"day".ToQuantity(positiveYearDay)} ago";
            }

            if (negativeYearDayDif > 0)
            {
                return $"{"year".ToQuantity(years)}, {"day".ToQuantity(negativeYearDayDif)} ago";
            }

            return $"{"year".ToQuantity(years)} ago";
        }

        [BsonIgnore]
        public string TimeString => $"{ReleaseDate:MMMM d, yyyy}  •  {GetAgeString()}  •  {Duration}";

        [BsonIgnore]
        public string TimeStringNewLine => $"{ReleaseDate:MMMM d, yyyy}{Environment.NewLine}{GetAgeString()}{Environment.NewLine}{Duration}";

        [BsonIgnore]
        public TimeSpan Duration { get; set; }

        [BsonIgnore]
        public string RawUrl
        {
            get => _rawUrl;
            set => SetProperty(ref _rawUrl, value);
        }
        private string _rawUrl;

        [BsonRepresentation(BsonType.String)]
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
