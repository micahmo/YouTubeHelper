using ServerStatusBot.Definitions.Database.Models;
using ServerStatusBot.Definitions.Models;

namespace YouTubeHelper.Shared
{
    /// <summary>
    /// An interface that describes a video view model, which can be shared across both implementations (desktop and mobile)
    /// </summary>
    public interface IVideoViewModel
    {
        Video Video { get; }

        void UpdateCheck(string requestId, RequestData result, bool showInAppNotifications = true);
    }
}
