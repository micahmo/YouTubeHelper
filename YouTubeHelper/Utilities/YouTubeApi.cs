using System;
using System.Collections.Generic;
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
            if (channelSearchResult.Items.FirstOrDefault(r => r.Id.Kind == "youtube#channel") is { } r)
            {
                channel.VanityName = r.Snippet.Title;
                channel.ChannelPlaylist = r.Snippet.ChannelId.Replace("UC", "UU");
                return true;
            }

            return false;
        }

        public async Task<IEnumerable<Models.Video>> FindVideos(Channel channel, SortMode sortMode = SortMode.DurationPlusRecency)
        {
            List<Models.Video> results = new();

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

            //videoIds = videoIds.Except(excludedVideoIds).ToList();

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

            foreach (Video v in rankedVideos.Take(10))
            {
                results.Add(new Models.Video
                {
                    Title = v.Snippet.Title,
                    Id = v.Id,
                    Description = v.Snippet.Description,
                    Duration = XmlConvert.ToTimeSpan(v.ContentDetails.Duration),
                    ReleaseDate = new DateTimeOffset(v.Snippet.PublishedAt ?? DateTime.MinValue),
                    ThumbnailUrl = /*v.Snippet.Thumbnails.Maxres?.Url ?? */v.Snippet.Thumbnails.Medium?.Url ?? v.Snippet.Thumbnails.Standard?.Url ?? v.Snippet.Thumbnails.High?.Url ?? v.Snippet.Thumbnails.Default__?.Url ?? string.Empty
                });
            }

            return results;
        }

        private int SortFunction(SortMode sortMode, Video video, List<Video> videosSortedByDuration, List<Video> videosSortedByAge)
        {
            switch (sortMode)
            {
                case SortMode.DurationDesc:
                    return -(int)XmlConvert.ToTimeSpan(video.ContentDetails.Duration).TotalMilliseconds;
                case SortMode.DurationAsc:
                    return (int)XmlConvert.ToTimeSpan(video.ContentDetails.Duration).TotalMilliseconds;
                case SortMode.AgeDesc:
                    return -(int)new DateTimeOffset(video.Snippet.PublishedAt ?? DateTime.MinValue).Ticks;
                case SortMode.AgeAsc:
                    return (int)new DateTimeOffset(video.Snippet.PublishedAt ?? DateTime.MinValue).Ticks;
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
        DurationPlusRecency,
        DurationDesc,
        DurationAsc,
        AgeDesc,
        AgeAsc,
    }
}
