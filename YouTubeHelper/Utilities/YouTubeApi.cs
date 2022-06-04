using System.Linq;
using System.Threading.Tasks;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using YouTubeHelper.Models;
using Channel = YouTubeHelper.Models.Channel;

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
                return true;
            }

            return false;
        }

        private readonly YouTubeService _youTubeService;
    }
}
