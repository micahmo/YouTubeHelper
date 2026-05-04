using ServerStatusBot.Definitions.Api;
using ServerStatusBot.Definitions.Models;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace YouTubeHelper.Shared.Utilities
{
    public static class QueueUtils
    {
        public static bool MatchesQueueFilter(IVideoViewModel vm, string term)
        {
            string trimmed = term.Trim();
            bool exact = trimmed.StartsWith('"') && trimmed.EndsWith('"') && trimmed.Length > 2;
            string haystack = $"{vm.Video.Title} {vm.Video.Description} {vm.Video.ChannelName}".ToLowerInvariant();
            return exact
                ? haystack.Contains(trimmed.Trim('"').ToLowerInvariant())
                : trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(word => haystack.Contains(word.ToLowerInvariant()));
        }

        /// <summary>
        /// Tries to join the download group (i.e., subscribe to SignalR updates) for the given video(s)
        /// </summary>
        public static Task TryJoinDownloadGroup(IVideoViewModel videoViewModel)
        {
            return TryJoinDownloadGroup(new List<IVideoViewModel> { videoViewModel });
        }

        /// <summary>
        /// Tries to join the download group (i.e., subscribe to SignalR updates) for the given video(s)
        /// </summary>
        public static async Task TryJoinDownloadGroup(IEnumerable<IVideoViewModel> videoViewModels)
        {
            try
            {
                List<RequestData> distinctQueue = await ServerApiClient.Instance.GetQueue();
                foreach (IVideoViewModel? videoViewModel in videoViewModels)
                {
                    Guid? requestId = distinctQueue.FirstOrDefault(v => v.VideoId! == videoViewModel.Video.Id)?.RequestGuid;
                    if (requestId != null)
                    {
                        await ServerApiClient.Instance.JoinDownloadGroup(requestId!.ToString()!, requestData => videoViewModel.UpdateCheck(requestId!.ToString()!, requestData, showInAppNotifications: false));
                    }
                }
            }
            catch
            {
                // Ignore this, because getting the queue isn't a big deal, and we don't want it to trip the outer retry.
            }
        }
    }
}
