using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using CliWrap;
using CliWrap.Buffered;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using YouTubeHelper.Models;
using Channel = YouTubeHelper.Models.Channel;
using Video = Google.Apis.YouTube.v3.Data.Video;

namespace YouTubeHelper.Utilities
{
    public class YouTubeApi
    {
        public static YouTubeApi Instance => _instance ??= new YouTubeApi();
        private static YouTubeApi _instance;

        private YouTubeApi()
        {
            _youTubeService = new YouTubeService(new BaseClientService.Initializer
            {
                ApiKey = Settings.Instance.YouTubeApiKey,
                ApplicationName = GetType().ToString()
            });
        }

        public async Task<bool> PopulateChannel(Channel channel)
        {
            SearchResource.ListRequest channelSearchRequest = _youTubeService.Search.List("snippet");
            channelSearchRequest.Q = channel.Identifier;
            SearchListResponse channelSearchResult = await channelSearchRequest.ExecuteAsync();
            List<SearchResult> items = channelSearchResult.Items.Where(r => r.Id.Kind == "youtube#channel").ToList();

            if (channel.ResultIndex >= items.Count)
            {
                channel.ResultIndex = -1;
            }

            if (channelSearchResult.Items.Skip(channel.ResultIndex++).FirstOrDefault() is { } r)
            {
                channel.VanityName = r.Snippet.Title;
                channel.ChannelPlaylist = r.Snippet.ChannelId.Replace("UC", "UU");
                channel.Description = r.Snippet.Description;
                return true;
            }

            return false;
        }

        public async Task<IEnumerable<Models.Video>> FindVideos(Channel channel, List<Models.Video> excludedVideos, bool showExclusions = false, SortMode sortMode = SortMode.DurationPlusRecency)
        {
            PlaylistItemsResource.ListRequest playlistRequest = _youTubeService.PlaylistItems.List("contentDetails");
            playlistRequest.Fields = "items/contentDetails/videoId,nextPageToken";
            playlistRequest.PlaylistId = channel.ChannelPlaylist;
            playlistRequest.MaxResults = 50;

            List<string> videoIds = new List<string>();

            string nextPageToken = string.Empty;
            while (nextPageToken != null)
            {
                playlistRequest.PageToken = nextPageToken;
                PlaylistItemListResponse response = await playlistRequest.ExecuteAsync();
                videoIds.AddRange(response.Items.Select(i => i.ContentDetails.VideoId));
                nextPageToken = response.NextPageToken;
            }

            if (!showExclusions)
            {
                videoIds = videoIds.Except(excludedVideos?.Select(v => v.Id) ?? Enumerable.Empty<string>()).ToList();
            }

            return await FindVideoDetails(videoIds, excludedVideos, channel, sortMode);
        }

        public async Task<IEnumerable<Models.Video>> FindVideoDetails(List<string> videoIds, List<Models.Video> excludedVideos, Channel channel, SortMode sortMode = SortMode.AgeDesc)
        {
            List<Models.Video> results = new();
            List<string> excludedVideoIds = excludedVideos?.Select(v => v.Id).ToList();

            // Use the videoIds to look up real info
            List<Video> videos = new List<Video>();

            while (videoIds.Any())
            {
                VideosResource.ListRequest videoRequest = _youTubeService.Videos.List(new List<string> { "snippet", "contentDetails" });
                videoRequest.Id = string.Join(",", videoIds.Take(50));
                var videoResponse = await videoRequest.ExecuteAsync();
                videos.AddRange(videoResponse.Items);

                videoIds = videoIds.Skip(50).ToList();
            }

            // First sort by duration
            var videosSortedByDuration = videos.OrderByDescending(v => XmlConvert.ToTimeSpan(v.ContentDetails.Duration)).ToList();

            // Then sort by age
            var videosSortedByAge = videos.OrderByDescending(v => v.Snippet.PublishedAt).ToList();

            var rankedVideos = videos.OrderBy(v => SortFunction(sortMode, v, videosSortedByDuration, videosSortedByAge)).ToList();

            foreach (Video video in rankedVideos.Take(10))
            {
                results.Add(new Models.Video
                {
                    Excluded = (excludedVideoIds ?? new List<string>()).Contains(video.Id),
                    ExclusionReason = excludedVideos?.FirstOrDefault(v => v.Id == video.Id)?.ExclusionReason ?? ExclusionReason.None,
                    Title = video.Snippet.Title,
                    Id = video.Id,
                    ChannelPlaylist = channel.ChannelPlaylist,
                    Description = video.Snippet.Description,
                    Duration = XmlConvert.ToTimeSpan(video.ContentDetails.Duration),
                    ReleaseDate = new DateTimeOffset(video.Snippet.PublishedAt ?? DateTime.MinValue),
                    ThumbnailUrl = /*v.Snippet.Thumbnails.Maxres?.Url ?? */video.Snippet.Thumbnails.Medium?.Url ?? video.Snippet.Thumbnails.Standard?.Url ?? video.Snippet.Thumbnails.High?.Url ?? video.Snippet.Thumbnails.Default__?.Url ?? string.Empty
                });
            }

            return results;
        }

        private double SortFunction(SortMode sortMode, Video video, List<Video> videosSortedByDuration, List<Video> videosSortedByAge)
        {
            switch (sortMode)
            {
                case SortMode.DurationDesc:
                    return -XmlConvert.ToTimeSpan(video.ContentDetails.Duration).TotalMilliseconds;
                case SortMode.DurationAsc:
                    return XmlConvert.ToTimeSpan(video.ContentDetails.Duration).TotalMilliseconds;
                case SortMode.AgeDesc:
                    return -new DateTimeOffset(video.Snippet.PublishedAt ?? DateTime.MinValue).Ticks;
                case SortMode.AgeAsc:
                    return new DateTimeOffset(video.Snippet.PublishedAt ?? DateTime.MinValue).Ticks;
                case SortMode.DurationPlusRecency:
                default:
                    return videosSortedByDuration.IndexOf(video) + videosSortedByAge.IndexOf(video);
            }
        }

        public async Task<string> GetRawUrl(string videoId)
        {
            // Get URL with yt-dlp
            return (await Cli.Wrap(Settings.Instance.YtDlpPath)
                .WithArguments($"-f b --get-url {videoId}")
                .ExecuteBufferedAsync()).StandardOutput;
        }

        private readonly YouTubeService _youTubeService;
    }

    public enum SortMode
    {
        [Description("Duration + Recency")]
        DurationPlusRecency,

        [Description("Duration Descending (Longest First)")]
        DurationDesc,

        [Description("Duration Ascending (Shortest First)")]
        DurationAsc,

        [Description("Age Descending (Newest First)")]
        AgeDesc,

        [Description("Age Ascending (Oldest First)")]
        AgeAsc
    }

    public class SortModeExtended : EnumExtended<SortMode>
    {
        public SortModeExtended(SortMode sortMode) : base(sortMode) { }
    }
}
