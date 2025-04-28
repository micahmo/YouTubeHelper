using System.Collections.Generic;
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

            return await ServerApiClient.Instance.FindVideoDetails(new FindVideosRequest
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
