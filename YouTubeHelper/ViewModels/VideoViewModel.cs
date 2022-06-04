using YouTubeHelper.Models;

namespace YouTubeHelper.ViewModels
{
    public class VideoViewModel
    {
        public VideoViewModel(Video video)
        {
            Video = video;
        }

        public Video Video { get; }
    }
}
