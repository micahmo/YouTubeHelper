using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using MongoDBHelpers;
using ServerStatusBot.Definitions;
using ServerStatusBot.Definitions.Database;
using ServerStatusBot.Definitions.Database.Models;
using Channel = ServerStatusBot.Definitions.Database.Models.Channel;
using Video = Google.Apis.YouTube.v3.Data.Video;
using ModelVideo = ServerStatusBot.Definitions.Database.Models.Video;

namespace YouTubeHelper.Shared.Utilities
{
    public class YouTubeApi
    {
        public static YouTubeApi Instance => _instance ??= new YouTubeApi();
        private static YouTubeApi? _instance;

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
            string? channelId = GetChannelIdFromUrl(channel.Identifier);

            if (channelId != null)
            {
                ChannelsResource.ListRequest? channelsListRequest = _youTubeService.Channels.List("snippet,contentDetails");
                channelsListRequest.Id = channelId;

                ChannelListResponse? channelsListResponse = await channelsListRequest.ExecuteAsync();

                if (channelsListResponse.Items.FirstOrDefault() is { } c)
                {
                    channel.VanityName = c.Snippet.Title;
                    channel.ChannelId = c.Id;
                    channel.ChannelPlaylist = c.ContentDetails.RelatedPlaylists.Uploads;
                    channel.Description = c.Snippet.Description;
                    return true;
                }
            }
            else
            {
                throw new Exception($"Unable to extract Channel ID from given identifier: {channel.Identifier}. Try providing the whole URL to the channel.");
            }

            return false;
        }

        public async Task<IEnumerable<ModelVideo>> FindVideos(
            Channel? channel, 
            List<ModelVideo> excludedVideos,
            bool showExclusions,
            SortMode sortMode,
            List<string>? searchTerms,
            Action<float, bool>? progressCallback = null,
            int count = 10, 
            DateTime? dateRangeLimit = null,
            TimeSpan? videoLengthMinimum = null)
        {
            PlaylistItemsResource.ListRequest playlistRequest = _youTubeService.PlaylistItems.List("contentDetails");
            playlistRequest.Fields = "items/contentDetails/videoId,nextPageToken,pageInfo/totalResults";
            playlistRequest.PlaylistId = channel?.ChannelPlaylist;
            playlistRequest.MaxResults = 50;

            List<string> videoIds = new List<string>();

            // Load known videos
            IEnumerable<string> knownVideos = (await Collections.KnownVideosCollection.FindByConditionAsync(v => v.ChannelPlaylist == channel!.ChannelPlaylist)).Select(v => v.Id);
            videoIds.AddRange(knownVideos);

#if DEBUG
            Stopwatch stopwatch = new();
            stopwatch.Start();
#endif

            string nextPageToken = string.Empty;
            for (float i = 1; nextPageToken != null; ++i)
            {
                playlistRequest.PageToken = nextPageToken;
                PlaylistItemListResponse response = await playlistRequest.ExecuteAsync();

                // If any ID from the API is already in the list, we can stop after this
                bool toBreak = response.Items.Any() && videoIds.Contains(response.Items.Last().ContentDetails.VideoId);

                videoIds.AddRange(response.Items.Select(i => i.ContentDetails.VideoId));
                nextPageToken = response.NextPageToken;

                float progress = Math.Min(i * 50 / response.PageInfo.TotalResults ?? 1, 1);
                progressCallback?.Invoke(progress, false);
                Debug.WriteLine($"Progress is {i*50} / {response.PageInfo.TotalResults} : {progress}");

                if (toBreak)
                {
                    break;
                }
            }

            progressCallback?.Invoke(0, true);

#if DEBUG
            stopwatch.Stop();
            Debug.WriteLine($"Took {stopwatch.Elapsed} for first retrieve.");
#endif

            // De-dup, in case we loaded from known and API
            videoIds = videoIds.Distinct().ToList();

            // Save the newly loaded known videos
            var knownVideosToAdd = videoIds.Except(knownVideos).Select(v => new ModelVideo
            {
                Id = v,
                ChannelPlaylist = channel?.ChannelPlaylist
            });
            if (knownVideosToAdd.Any())
            {
                await Collections.KnownVideosCollection.InsertManyAsync(knownVideosToAdd);
            }

            if (!showExclusions)
            {
                videoIds = videoIds.Except(excludedVideos?.Select(v => v.Id) ?? Enumerable.Empty<string>()).ToList();
            }

            return await FindVideoDetails(videoIds, excludedVideos, channel, sortMode, searchTerms, count, dateRangeLimit, videoLengthMinimum);
        }

        public async Task<IEnumerable<ModelVideo>> SearchVideos(Channel channel, List<ModelVideo> excludedVideos, bool showExclusions, SortMode sortMode, string lookup, int count = 10)
        {
            SearchResource.ListRequest videoSearchRequest = _youTubeService.Search.List("snippet");
            videoSearchRequest.Q = lookup;
            SearchListResponse channelSearchResult = await videoSearchRequest.ExecuteAsync();
            
            List<string> items = channelSearchResult.Items.Where(r => r.Id.Kind == "youtube#video").Select(r => r.Id.VideoId).ToList();

            if (!showExclusions)
            {
                items = items.Except(excludedVideos?.Select(v => v.Id) ?? Enumerable.Empty<string>()).ToList();
            }

            return await FindVideoDetails(items, excludedVideos, channel, sortMode, count: count);
        }

        public async Task<IEnumerable<ModelVideo>> FindVideoDetails(
            List<string> videoIds,
            List<ModelVideo>? excludedVideos = null, 
            Channel? channel = null,
            SortMode? sortMode = null,
            List<string>? searchTerms = null,
            int count = 10,
            DateTime? dateRangeLimit = null,
            TimeSpan? videoLengthMinimum = null,
            Func<List<Video>, List<Video>>? customSort = null)
        {
            List<ModelVideo> results = new();
            List<string>? excludedVideoIds = excludedVideos?.Select(v => v.Id).ToList();

            // Use the videoIds to look up real info
            ConcurrentBag<IList<Video>> videoResults = new();

#if DEBUG
            Stopwatch stopwatch = new();
            stopwatch.Start();
#endif

            await Parallel.ForEachAsync(videoIds.Chunk(50), async (chunkedVideoIds, _) =>
            {
                VideosResource.ListRequest videoRequest = _youTubeService.Videos.List(new List<string> { "snippet", "contentDetails" });
                videoRequest.Id = string.Join(",", chunkedVideoIds.Take(50));
                var videoResponse = await videoRequest.ExecuteAsync();
                videoResults.Add(videoResponse.Items);
            });

            List<Video> videos = videoResults.SelectMany(v => v).ToList();

#if DEBUG
            stopwatch.Stop();
            Debug.WriteLine($"Took {stopwatch.Elapsed} for second retrieve.");
#endif

            // Do optional filtering
            if (searchTerms?.Any() == true)
            {
                // Filter by search terms
                HashSet<Video> filteredVideos = new HashSet<Video>();
                foreach (Video video in videos)
                {
                    bool result = true;
                    foreach (string searchTerm in searchTerms)
                    {
                        if (!video.Snippet.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                        {
                            result = false;
                        }
                    }
                    if (result)
                    {
                        filteredVideos.Add(video);
                    }

                    result = true;
                    foreach (string searchTerm in searchTerms)
                    {
                        if (!video.Snippet.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                        {
                            result = false;
                        }
                    }
                    if (result)
                    {
                        filteredVideos.Add(video);
                    }
                }

                videos = filteredVideos.ToList();
            }

            // Filter by date range
            if (dateRangeLimit.HasValue)
            {
                videos = videos.Except(videos.Where(v => v.Snippet.PublishedAtDateTimeOffset < dateRangeLimit.Value)).ToList();
            }

            // Filter by video length
            if (videoLengthMinimum.HasValue)
            {
                videos = videos.Except(videos.Where(v => videoLengthMinimum.Value >= XmlConvert.ToTimeSpan(v.ContentDetails.Duration))).ToList();
            }

            // Remove videos that are not from this channel
            if (channel is not null)
            {
                videos = videos.Except(videos.Where(v => v.Snippet.ChannelId != channel.ChannelId)).ToList();
            }

            // First sort by duration
            var videosSortedByDuration = videos.OrderByDescending(v => XmlConvert.ToTimeSpan(v.ContentDetails.Duration)).ToList();

            // Then sort by age
            var videosSortedByAge = videos.OrderByDescending(v => v.Snippet.PublishedAtDateTimeOffset).ToList();

            List<Video> rankedVideos = videos;
            
            if (sortMode != null)
            {
                rankedVideos = videos.OrderBy(v => SortFunction(sortMode.Value, v, videosSortedByDuration, videosSortedByAge)).ToList();
            }

            if (customSort != null)
            {
                rankedVideos = customSort(videos);
            }    

            foreach (Video video in rankedVideos.Take(searchTerms?.Any() == true ? int.MaxValue : count))
            {
                results.Add(new ModelVideo
                {
                    Excluded = (excludedVideoIds ?? new List<string>()).Contains(video.Id),
                    ExclusionReason = excludedVideos?.FirstOrDefault(v => v.Id == video.Id)?.ExclusionReason ?? ExclusionReason.None,
                    Title = video.Snippet.Title,
                    Id = video.Id,
                    ChannelPlaylist = channel?.ChannelPlaylist ?? video.Snippet.ChannelId.Replace("UC", "UU"),
                    Description = video.Snippet.Description,
                    Duration = XmlConvert.ToTimeSpan(video.ContentDetails.Duration),
                    ReleaseDate = video.Snippet.PublishedAtDateTimeOffset ?? DateTimeOffset.MinValue,
                    ThumbnailUrl = /*v.Snippet.Thumbnails.Maxres?.Url ?? */video.Snippet.Thumbnails.Medium?.Url ?? video.Snippet.Thumbnails.Standard?.Url ?? video.Snippet.Thumbnails.High?.Url ?? video.Snippet.Thumbnails.Default__?.Url ?? string.Empty
                });
            }

            return results;
        }

        public async Task<string> FindChannelId(string channelHandle, string defaultValue = "")
        {
            SearchResource.ListRequest request = _youTubeService.Search.List("snippet");
            request.Q = channelHandle;
            SearchListResponse response = await request.ExecuteAsync();

            return response.Items.FirstOrDefault()?.Snippet.ChannelId ?? defaultValue;
        }

        public async Task<string> FindChannelName(string channelId, string defaultValue)
        {
            ChannelsResource.ListRequest request = _youTubeService.Channels.List("snippet");
            request.Id = channelId;
            ChannelListResponse response = await request.ExecuteAsync();

            return response.Items?.FirstOrDefault()?.Snippet.Title ?? defaultValue;
        }

        public string? ToChannelPlaylist(string? channelId) => channelId?.Replace("UC", "UU");

        public string? ToChannelId(string? channelPlaylist) => channelPlaylist?.Replace("UU", "UC");

        private double SortFunction(SortMode sortMode, Video video, List<Video> videosSortedByDuration, List<Video> videosSortedByAge)
        {
            switch (sortMode)
            {
                case SortMode.DurationDesc:
                    return -XmlConvert.ToTimeSpan(video.ContentDetails.Duration).TotalMilliseconds;
                case SortMode.DurationAsc:
                    return XmlConvert.ToTimeSpan(video.ContentDetails.Duration).TotalMilliseconds;
                case SortMode.AgeDesc:
                    return -(video.Snippet.PublishedAtDateTimeOffset ?? DateTimeOffset.MinValue).Ticks;
                case SortMode.AgeAsc:
                    return (video.Snippet.PublishedAtDateTimeOffset ?? DateTimeOffset.MinValue).Ticks;
                case SortMode.DurationDescPlusAgeDesc:
                    return videosSortedByDuration.IndexOf(video) + videosSortedByAge.IndexOf(video);
                case SortMode.DurationAscPlusAgeDesc:
                    List<Video> videosSortedByDurationReverse = videosSortedByDuration.ToList();
                    videosSortedByDurationReverse.Reverse();
                    return videosSortedByDurationReverse.IndexOf(video) + videosSortedByAge.IndexOf(video);
                default:
                    return 0;
            }
        }

        private static string? GetChannelIdFromUrl(string? url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                return uri.Segments.Select(s => s.Trim('/')).FirstOrDefault(s => s.StartsWith("UC"));
            }

            return null;
        }

        private readonly YouTubeService _youTubeService;
    }

    public enum SortMode
    {
        [Description("Duration Descending + Age Descending (Longest + Newest)")]
        DurationDescPlusAgeDesc,

        [Description("Duration Ascending + Age Descending (Shortest + Newest)")]
        DurationAscPlusAgeDesc,

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
