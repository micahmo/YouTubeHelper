using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using System.Windows.Input;
using YouTubeHelper.Mobile.Views;
using ServerStatusBot.Definitions.Models;
using ServerStatusBot.Definitions.Database.Models;
using ServerStatusBot.Definitions.Api;
using YouTubeHelper.Shared;

namespace YouTubeHelper.Mobile.ViewModels
{
    public class VideoViewModel : ObservableObject, IVideoViewModel
    {
        private readonly AppShell _page;
        private readonly ChannelViewModel _channelViewModel;

        public VideoViewModel(Video video, AppShell page, ChannelViewModel channelViewModel)
        {
            Video = video;
            _page = page;
            _channelViewModel = channelViewModel;

            Video.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(Video.Excluded) || args.PropertyName == nameof(Video.ExclusionReason) || args.PropertyName == nameof(Video.Status))
                {
                    OnPropertyChanged(nameof(ExcludedString));
                    OnPropertyChanged(nameof(HasStatus));
                }
            };
        }

        public Video Video { get; }

        public bool IsDescriptionExpanded
        {
            get => _isDescriptionExpanded;
            set => SetProperty(ref _isDescriptionExpanded, value);
        }
        private bool _isDescriptionExpanded;

        public bool IsPlaying
        {
            get => _isPlaying;
            set => SetProperty(ref _isPlaying, value);
        }
        private bool _isPlaying;

        public ICommand ToggleDescriptionCommand => _toggleDescriptionCommand ??= new RelayCommand(ToggleDescription);
        private ICommand? _toggleDescriptionCommand;

        public void ToggleDescription()
        {
            IsDescriptionExpanded = !IsDescriptionExpanded;
        }

        public string? ExcludedString => new ExclusionReasonExtended(Video.ExclusionReason).Description;

        public bool HasStatus => Video.Status is not null && Video.Excluded is not true;

        public ICommand VideoTappedCommand => _videoTappedCommand ??= new RelayCommand(VideoTapped);
        private ICommand? _videoTappedCommand;

        public async void VideoTapped()
        {
            if (_isPopupOpen)
            {
                return;
            }

            _isPopupOpen = true;

            try
            {
                string? action = await _page.DisplayActionSheet(
                    Video.Title,
                    Resources.Resources.Cancel,
                    null,
                    GetActionSheetOptions(excluded: Video.Excluded, queueTabSelected: AppShell.Instance?.AppShellViewModel.QueueTabSelected == true));

                bool excluded = false;

                if (action == Resources.Resources.Watch)
                {
                    using (new BusyIndicator(_page))
                    {
                        //_channelViewModel.ShowPlayer = true;
                        //_channelViewModel.CurrentVideoUrl = await GetRawUrl(Video.Id);

                    _page.AppShellViewModel.ChannelViewModels.ToList().ForEach(c =>
                    {
                        c.Videos.ToList().ForEach(v =>
                        {
                            v.IsPlaying = false;
                        });
                    });

                        IsPlaying = true;

                        await Browser.Default.OpenAsync(await GetRawUrl(Video.Id), new BrowserLaunchOptions
                        {
                            LaunchMode = BrowserLaunchMode.SystemPreferred,
                            TitleMode = BrowserTitleMode.Hide,
                            PreferredToolbarColor = Color.FromArgb("b22222")
                        });
                    }
                }
                else if (action == Resources.Resources.WatchExternal)
                {
                    try
                    {
                        _page.AppShellViewModel.ChannelViewModels.ToList().ForEach(c =>
                        {
                            c.Videos.ToList().ForEach(v =>
                            {
                                v.IsPlaying = false;
                            });
                        });

                        IsPlaying = true;

                        // Open the URI in the system default app
                        Uri videoUri = new Uri($"https://www.youtube.com/watch?v={Video.Id}");
                        await Launcher.OpenAsync(videoUri);
                    }
                    catch
                    {
                        // Ignore
                    }
                }
                else if (action == Resources.Resources.ExcludeWatched)
                {
                    Video.ExclusionReason = ExclusionReason.Watched;
                    excluded = true;
                }
                else if (action == Resources.Resources.ExcludeWontWatch)
                {
                    Video.ExclusionReason = ExclusionReason.WontWatch;
                    excluded = true;
                }
                else if (action == Resources.Resources.ExcludeMightWatch)
                {
                    Video.ExclusionReason = ExclusionReason.MightWatch;
                    excluded = true;
                }
                else if (action == Resources.Resources.Unexclude)
                {
                    Video.Excluded = false;
                    Video.ExclusionReason = ExclusionReason.None;
                    Video.MarkForDeletion = true;
                    await ServerApiClient.Instance.UpdateVideo(Video, AppShell.ClientId);
                    Video.MarkForDeletion = false;
                }
                else if (action == Resources.Resources.DownloadCustom)
                {
                    string res = await _page.DisplayPromptAsync(Resources.Resources.DownloadDirectoryTitle, Resources.Resources.DownloadDirectoryMessage, initialValue: Settings.Instance!.DownloadDirectory);

                    if (string.IsNullOrWhiteSpace(res))
                    {
                        // Cancel or entered nothing
                        return;
                    }

                    await DownloadVideo(Settings.Instance.DownloadDirectory = res);
                }
                else if (action?.StartsWith(Resources.Resources.Download) == true)
                {
                    await DownloadVideo(Settings.Instance!.DownloadDirectory);
                }
                else if (action == Resources.Resources.GoToChannel)
                {
                    AppShell.Instance?.HandleSharedLink(Video.Id, null);
                }

                if (excluded)
                {
                    Video.Excluded = true;
                    await ServerApiClient.Instance.UpdateVideo(Video, AppShell.ClientId);

                    if (_page.AppShellViewModel.WatchTabSelected && !_channelViewModel.ShowExcludedVideos)
                    {
                        _channelViewModel.Videos.Remove(this);
                    }
                }
            }
            finally
            {
                _isPopupOpen = false;
            }
        }
        private static bool _isPopupOpen;

        private string[] GetActionSheetOptions(bool excluded, bool queueTabSelected)
        {
            List<string> options = new List<string>
            {
                Resources.Resources.Watch,
                Resources.Resources.WatchExternal
            };

            if (excluded)
            {
                options.Add(Resources.Resources.Unexclude);
            }
            else
            {
                options.Add(Resources.Resources.ExcludeWatched);
                options.Add(Resources.Resources.ExcludeWontWatch);
                options.Add(Resources.Resources.ExcludeMightWatch);
            }

            options.Add(Resources.Resources.DownloadCustom);
            options.Add(string.Format(Resources.Resources.DownloadPath, Settings.Instance!.DownloadDirectory));

            if (queueTabSelected)
            {
                options.Add(Resources.Resources.GoToChannel);
            }

            return options.ToArray();
        }


        public static Task<string> GetRawUrl(string videoId)
        {
            try
            {
                return ServerApiClient.Instance.YouTubeLink(videoId: videoId);
            }
            catch
            {
                return Task.FromResult("https://google.com");
            }
        }

        private async Task DownloadVideo(string dataDirectorySubpath)
        {
            if (_previousRequestId != null)
            {
                await ServerApiClient.Instance.LeaveDownloadGroup(_previousRequestId);
                _previousRequestId = null;
            }

            // Initiate the download
            string url = $"https://www.youtube.com/watch?v={Video.Id}";

            // Generate request ID
            string requestId = Guid.NewGuid().ToString();

            // Unexclude the video before downloading again
            Video.Excluded = false;
            Video.ExclusionReason = ExclusionReason.None;
            Video.MarkForDeletion = true;
            await ServerApiClient.Instance.UpdateVideo(Video, AppShell.ClientId);
            Video.MarkForDeletion = false;

            try
            {
                await ServerApiClient.Instance.DownloadVideo(
                    url: url,
                    silent: true,
                    requestId: requestId,
                    dataDirectorySubpath: dataDirectorySubpath,
                    videoId: Video.Id,
                    videoName: Video.Title ?? string.Empty,
                    thumbnailUrl: Video.ThumbnailUrl ?? string.Empty,
                    channelPlaylist: Video.ChannelPlaylist,
                    idInChannelFolder: dataDirectorySubpath.Equals("jellyfin", StringComparison.OrdinalIgnoreCase) ? false : true);
            }
            catch (Exception ex)
            {
                await Toast.Make(string.Format(Resources.Resources.VideoDownloadFailed, Video.Title, ex.Message.Substring(0, ex.Message.IndexOf(':'))), ToastDuration.Long).Show();
                return;
            }

            try
            {
                // Try to hook up for progress updates
                await ServerApiClient.Instance.JoinDownloadGroup(requestId, requestData => UpdateCheck(requestId, requestData));
            }
            catch (Exception ex)
            {
                await Toast.Make(string.Format(Resources.Resources.ProgressUpdateFailed, Video.Title, ex.Message), ToastDuration.Long).Show();
            }

            await Toast.Make(string.Format(Resources.Resources.VideoDownloadRequested, Video.Title), ToastDuration.Long).Show();

            // Mark as downloading
            Video.Excluded = false;
            Video.Status = string.Format(Resources.Resources.DownloadingProgress, "0%");
            Video.ExclusionReason = ExclusionReason.None;
            Video.Progress = 0;
        }

        private string? _previousRequestId;
        private bool _statusWasEverNotDone;

        public void UpdateCheck(string requestId, RequestData result, bool showInAppNotifications = true)
        {
            if (result.VideoId != Video.Id)
            {
                // We got an update for a different video
                return;
            }
            _previousRequestId = requestId;

            Video.Status = string.Format(Resources.Resources.DownloadingProgress, $"{result.Progress}%");
            Video.Progress = result.Progress;

            if (result.Status == DownloadStatus.InProgress)
            {
                _statusWasEverNotDone = true;
            }

            if (result.Status == DownloadStatus.Completed)
            {
                // Mark as downloaded (only if succeeded)
                Video.Status = null;
                Video.Progress = 100;

                if (_statusWasEverNotDone)
                {
                    Video.Excluded = true;
                    Video.ExclusionReason = ExclusionReason.Watched;
                    // NOTE: We no longer need to update the db here because the server does it,
                    // so the above is purely a UI update.
                }

                if (showInAppNotifications && _statusWasEverNotDone)
                {
                    MainThread.BeginInvokeOnMainThread(async () => { await Toast.Make(string.Format(Resources.Resources.VideoDownloadSucceeded, Video.Title), ToastDuration.Long).Show(); });
                }

                ServerApiClient.Instance.LeaveDownloadGroup(requestId);
                _statusWasEverNotDone = false;
            }

            if (result.Status == DownloadStatus.Failed)
            {
                Video.Status = Resources.Resources.FailedToDownload;

                if (showInAppNotifications && _statusWasEverNotDone && result.Status != null)
                {
                    string status = result.Status!.ToString()!;

                    if (!string.IsNullOrEmpty(result.Reason))
                    {
                        status += $" - {result.Reason}";
                    }

                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await Toast.Make(string.Format(Resources.Resources.VideoDownloadFailed, Video.Title, string.Empty), ToastDuration.Long).Show();
                        await Task.Delay(TimeSpan.FromSeconds(5));
                        await Toast.Make(status, ToastDuration.Long).Show();
                    });
                }

                ServerApiClient.Instance.LeaveDownloadGroup(requestId);
                _statusWasEverNotDone = false;
            }
        }
    }
}
