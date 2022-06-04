using System;
using YouTubeHelper.Properties;

namespace YouTubeHelper.Models
{
    public class Video
    {
        public string ThumbnailUrl { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public DateTimeOffset ReleaseDate { get; set; }

        public string TimeString => string.Format(Resources.TimeStringFormat, ReleaseDate, (DateTimeOffset.Now - ReleaseDate).Days, Duration);

        public TimeSpan Duration { get; set; }
    }
}
