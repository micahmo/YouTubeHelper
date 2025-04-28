using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using ServerStatusBot.Definitions;
using ServerStatusBot.Definitions.Database.Models;
using Channel = ServerStatusBot.Definitions.Database.Models.Channel;
using ModelVideo = ServerStatusBot.Definitions.Database.Models.Video;
using ServerStatusBot.Definitions.Api;
using ServerStatusBot.Definitions.Models;

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

            return await ServerApiClient.Instance.FindVideoDetails(new FindVideoDetailsRequest
            {
                VideoIds = videoIds,
                ExcludedVideos = excludedVideos,
                Channel = channel,
                SortMode = sortMode,
                SearchTerms = searchTerms,
                Count = count,
                DateRangeLimit = dateRangeLimit,
                VideoLengthMinimum = videoLengthMinimum
            });
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

            return await ServerApiClient.Instance.FindVideoDetails(new FindVideoDetailsRequest
            {
                VideoIds = items,
                ExcludedVideos = excludedVideos,
                Channel = channel,
                SortMode = sortMode,
                Count = count
            });
        }

        private readonly YouTubeService _youTubeService;
    }
}
