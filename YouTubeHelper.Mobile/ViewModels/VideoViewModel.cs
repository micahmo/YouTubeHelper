using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Flurl;
using Flurl.Http;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using System.Net;
using System.Windows.Input;
using YouTubeHelper.Mobile.Views;
using YouTubeHelper.Shared;
using YouTubeHelper.Shared.Models;
using ServerStatusBot.Definitions.Models;

namespace YouTubeHelper.Mobile.ViewModels
{
    public class VideoViewModel : ObservableObject
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
                string action;
                if (Video.Excluded)
                {
                    action = await _page.DisplayActionSheet(Video.Title, Resources.Resources.Cancel, null,
                        Resources.Resources.Watch,
                        Resources.Resources.WatchExternal,
                        Resources.Resources.Unexclude,
                        Resources.Resources.DownloadCustom,
                        string.Format(Resources.Resources.DownloadPath, Settings.Instance.DownloadDirectory));
                }
                else
                {
                    action = await _page.DisplayActionSheet(Video.Title, Resources.Resources.Cancel, null,
                        Resources.Resources.Watch,
                        Resources.Resources.WatchExternal,
                        Resources.Resources.ExcludeWatched,
                        Resources.Resources.ExcludeWontWatch,
                        Resources.Resources.ExcludeMightWatch,
                        Resources.Resources.DownloadCustom,
                        string.Format(Resources.Resources.DownloadPath, Settings.Instance.DownloadDirectory));
                }

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
                    await DatabaseEngine.ExcludedVideosCollection.DeleteAsync(Video.Id);
                }
                else if (action == Resources.Resources.DownloadCustom)
                {
                    string res = await _page.DisplayPromptAsync(Resources.Resources.DownloadDirectoryTitle, Resources.Resources.DownloadDirectoryMessage, initialValue: Settings.Instance.DownloadDirectory);

                    if (string.IsNullOrWhiteSpace(res))
                    {
                        // Cancel or entered nothing
                        return;
                    }

                    await DownloadVideo(Settings.Instance.DownloadDirectory = res);
                }
                else if (action?.StartsWith(Resources.Resources.Download) == true)
                {
                    await DownloadVideo(Settings.Instance.DownloadDirectory);
                }

                if (excluded)
                {
                    Video.Excluded = true;
                    await DatabaseEngine.ExcludedVideosCollection.UpsertAsync<Video, string>(Video);

                    if (!_channelViewModel.ShowExcludedVideos && !_page.AppShellViewModel.QueueTabSelected)
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

        public static async Task<string> GetRawUrl(string videoId)
        {
            try
            {
                return await (await Settings.Instance.TelegramApiAddress
                    .AppendPathSegment("youtubelink")
                    .AppendPathSegment(videoId)
                    .SetQueryParam("apiKey", Settings.Instance.TelegramApiKey)
                    .GetAsync()).GetStringAsync();
            }
            catch
            {
                return "https://google.com";
            }
        }

        private async Task DownloadVideo(string dataDirectorySubpath)
        {
            if (_progressCancellationToken is not null)
            {
                // There is a download in progress and we've been told to cancel it.
                _progressCancellationToken.Cancel();
                return;
            }

            _progressCancellationToken = new();

            // Initiate the download
            string url = $"https://www.youtube.com/watch?v={Video.Id}";

            // Generate request ID
            string requestId = Guid.NewGuid().ToString();

            // Unexclude the video before downloading again
            Video.Excluded = false;
            Video.ExclusionReason = ExclusionReason.None;
            await DatabaseEngine.ExcludedVideosCollection.DeleteAsync(Video.Id);

            try
            {
                await Settings.Instance.TelegramApiAddress
                    .AppendPathSegment("youtube")
                    .AppendPathSegment(url, fullyEncode: true)
                    .SetQueryParam("apiKey", Settings.Instance.TelegramApiKey)
                    .SetQueryParam("silent", false)
                    .SetQueryParam("requestId", requestId)
                    .SetQueryParam("dataDirectorySubpath", dataDirectorySubpath)
                    .SetQueryParam("idInChannelFolder", dataDirectorySubpath.Equals("jellyfin", StringComparison.OrdinalIgnoreCase) ? false : true)
                    .GetAsync();
            }
            catch (Exception ex)
            {
                await Toast.Make(string.Format(Resources.Resources.VideoDownloadFailed, Video.Title, ex.Message.Substring(0, ex.Message.IndexOf(':'))), ToastDuration.Long).Show();
                _progressCancellationToken = null;
                return;
            }

            StartUpdateCheck(requestId);

            await Toast.Make(string.Format(Resources.Resources.VideoDownloadRequested, Video.Title), ToastDuration.Long).Show();

            // Mark as downloading
            Video.Excluded = false;
            Video.Status = Resources.Resources.Downloading;
            Video.ExclusionReason = ExclusionReason.None;
            Video.Progress = 0;
        }

        private CancellationTokenSource? _progressCancellationToken;

        internal void StartUpdateCheck(string requestId, bool showInAppNotifications = true)
        {
            _progressCancellationToken ??= new();

            // Spin up a task to check for progress
            _ = Task.Run(async () =>
            {
                bool statusWasEverNotDone = false;

                while (_progressCancellationToken?.IsCancellationRequested == false)
                {
                    IFlurlResponse progressResponse;
                    try
                    {
                        progressResponse = await Settings.Instance.TelegramApiAddress
                            .AppendPathSegment("v2")
                            .AppendPathSegment("progress")
                            .AppendPathSegment(requestId)
                            .AllowAnyHttpStatus() // This will return 400 before the request starts, so ignore it.
                            .GetAsync(HttpCompletionOption.ResponseContentRead, _progressCancellationToken.Token);
                    }
                    catch (Exception ex)
                    {
                        if (showInAppNotifications)
                        {
                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                await Toast.Make(string.Format(Resources.Resources.VideoDownloadFailed, Video.Title, ex.Message.Substring(0, ex.Message.IndexOf(':'))), ToastDuration.Long).Show();
                            });
                        }

                        return;
                    }

                    if (progressResponse.StatusCode == (int)HttpStatusCode.OK)
                    {
                        RequestData result = await progressResponse.GetJsonAsync<RequestData>();
                        Video.Status = string.Format(Resources.Resources.DownloadingProgress, $"{result.Progress}%");
                        Video.Progress = result.Progress;

                        if (result.Status == DownloadStatus.InProgress)
                        {
                            statusWasEverNotDone = true;
                        }

                        if (result.Status == DownloadStatus.Completed)
                        {
                            // Mark as downloaded (only if succeeded)
                            Video.Status = null;
                            Video.Progress = 100;

                            if (statusWasEverNotDone)
                            {
                                Video.Excluded = true;
                                Video.ExclusionReason = ExclusionReason.Watched;
                                await DatabaseEngine.ExcludedVideosCollection.UpsertAsync<Video, string>(Video);
                            }

                            if (showInAppNotifications)
                            {
                                MainThread.BeginInvokeOnMainThread(async () =>
                                {
                                    await Toast.Make(string.Format(Resources.Resources.VideoDownloadSucceeded, Video.Title), ToastDuration.Long).Show();
                                });
                            }

                            return;
                        }

                        if (result.Status == DownloadStatus.Failed)
                        {
                            if (showInAppNotifications)
                            {
                                MainThread.BeginInvokeOnMainThread(async () =>
                                {
                                    await Toast.Make(string.Format(Resources.Resources.VideoDownloadFailed, Video.Title, result.Status), ToastDuration.Long).Show();
                                });
                            }

                            return;
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(.5));
                }
            }).ContinueWith(_ =>
            {
                _progressCancellationToken = null;
            });
        }
    }
}
