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
using ServerStatusBot.Definitions;
using ServerStatusBot.Definitions.Database.Models;
using Channel = ServerStatusBot.Definitions.Database.Models.Channel;
using Video = Google.Apis.YouTube.v3.Data.Video;
using ModelVideo = ServerStatusBot.Definitions.Database.Models.Video;
using ServerStatusBot.Definitions.Api;

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
            IEnumerable<string> knownVideos = new List<string>();
            if (channel?.ChannelPlaylist != null)
            {
                knownVideos = await ServerApiClient.Instance.GetKnownVideos(channel.ChannelPlaylist!);
                videoIds.AddRange(knownVideos);
            }

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
            List<ModelVideo> knownVideosToAdd = videoIds.Except(knownVideos).Select(v => new ModelVideo
            {
                Id = v,
                ChannelPlaylist = channel?.ChannelPlaylist
            }).ToList();
            if (knownVideosToAdd.Any())
            {
                await ServerApiClient.Instance.AddKnownVideos(knownVideosToAdd);
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
                rankedVideos = videos.OrderBy(v => YouTubeUtils.SortFunction(sortMode.Value, v, videosSortedByDuration, videosSortedByAge)).ToList();
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

        private readonly YouTubeService _youTubeService;
    }
}
