using ServerStatusBot.Definitions.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Flurl;
using Flurl.Http;
using ServerStatusBot.Definitions.Database.Models;

namespace YouTubeHelper.Shared.Utilities;

public class ServerStatusBotApi
{
    public static ServerStatusBotApi Instance => _instance ??= new ServerStatusBotApi();
    private static ServerStatusBotApi? _instance;

    public async Task<List<RequestData>> GetQueue()
    {
        // Get the queue from the server
        List<RequestData> queue = await(await Settings.Instance.ServerAddress
            .AppendPathSegment("queue")
            .SetQueryParam("apiKey", Settings.Instance.ServerApiKey)
            .GetAsync()).GetJsonAsync<List<RequestData>>();

        // De-duplicate by videoId, keeping the item with the highest dateAdded
        List<RequestData> distinctQueue = queue
            .GroupBy(item => item.VideoId!)
            .Select(group => group.OrderByDescending(item => item.DateAdded).First())
            .ToList();

        return distinctQueue;
    }
}