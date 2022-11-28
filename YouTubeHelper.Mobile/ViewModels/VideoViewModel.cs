using System.Net;
using Microsoft.Toolkit.Mvvm.Input;
using System.Windows.Input;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Flurl;
using Flurl.Http;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using YouTubeHelper.Shared.Models;
using YouTubeHelper.Shared;
using YouTubeHelper.Mobile.Views;

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
        private ICommand _toggleDescriptionCommand;

        public void ToggleDescription()
        {
            IsDescriptionExpanded = !IsDescriptionExpanded;
        }

        public string ExcludedString => new ExclusionReasonExtended(Video.ExclusionReason).Description;

        public bool HasStatus => Video.Status is not null && Video.Excluded is not true;

        public ICommand VideoTappedCommand => _videoTappedCommand ??= new RelayCommand(VideoTapped);
        private ICommand _videoTappedCommand;

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
                        Resources.Resources.Unexclude,
                        Resources.Resources.Download);
                }
                else
                {
                    action = await _page.DisplayActionSheet(Video.Title, Resources.Resources.Cancel, null,
                        Resources.Resources.Watch,
                        Resources.Resources.ExcludeWatched,
                        Resources.Resources.ExcludeWontWatch,
                        Resources.Resources.ExcludeMightWatch,
                        Resources.Resources.Download);
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

                    if (_page.AppShellViewModel.ExclusionsTabSelected)
                    {
                        _channelViewModel.Videos.Remove(this);
                    }
                }
                else if (action == Resources.Resources.Download)
                {
                    await DownloadVideo();
                }

                if (excluded)
                {
                    Video.Excluded = true;
                    await DatabaseEngine.ExcludedVideosCollection.UpsertAsync<Video, string>(Video);

                    if (!_channelViewModel.ShowExcludedVideos)
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

        private async Task DownloadVideo()
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

            try
            {
                await Settings.Instance.TelegramApiAddress
                    .AppendPathSegment("youtube")
                    .AppendPathSegment(url, fullyEncode: true)
                    .SetQueryParam("apiKey", Settings.Instance.TelegramApiKey)
                    .SetQueryParam("silent", false)
                    .SetQueryParam("requestId", requestId)
                    .GetAsync();
            }
            catch (Exception ex)
            {
                await Toast.Make(string.Format(Resources.Resources.VideoDownloadFailed, Video.Title, ex.Message.Substring(0, ex.Message.IndexOf(':'))), ToastDuration.Long).Show();
                _progressCancellationToken = null;
                return;
            }

            // Spin up a task to check for progress
            _ = Task.Run(async () =>
            {
                while (!_progressCancellationToken.IsCancellationRequested)
                {
                    IFlurlResponse progressResponse;
                    try
                    {
                        progressResponse = await Settings.Instance.TelegramApiAddress
                            .AppendPathSegment("v2")
                            .AppendPathSegment("progress")
                            .AppendPathSegment(requestId)
                            .AllowAnyHttpStatus() // This will return 400 before the request starts, so ignore it.
                            .GetAsync(_progressCancellationToken.Token);
                    }
                    catch (Exception ex)
                    {
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            await Toast.Make(string.Format(Resources.Resources.VideoDownloadFailed, Video.Title, ex.Message.Substring(0, ex.Message.IndexOf(':'))), ToastDuration.Long).Show();
                        });
                        return;
                    }

                    if (progressResponse.StatusCode == (int)HttpStatusCode.OK)
                    {
                        dynamic result = await progressResponse.GetJsonAsync();
                        Video.Status = string.Format(Resources.Resources.DownloadingProgress, $"{result.progress}%");

                        if (result.status == 1)
                        {
                            // Mark as downloaded (only if succeeded)
                            Video.Excluded = true;
                            Video.Status = null;
                            Video.ExclusionReason = ExclusionReason.Watched;
                            await DatabaseEngine.ExcludedVideosCollection.UpsertAsync<Video, string>(Video);

                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                await Toast.Make(string.Format(Resources.Resources.VideoDownloadSucceeded, Video.Title), ToastDuration.Long).Show();
                            });
                            return;
                        }

                        if (result.status == 2)
                        {
                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                await Toast.Make(string.Format(Resources.Resources.VideoDownloadFailed, Video.Title, result.status), ToastDuration.Long).Show();
                            });
                            return;
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(.5));
                }
            }).ContinueWith(_ =>
            {
                _progressCancellationToken = null;
            });

            await Toast.Make(string.Format(Resources.Resources.VideoDownloadRequested, Video.Title), ToastDuration.Long).Show();

            // Mark as downloading
            Video.Excluded = false;
            Video.Status = Resources.Resources.Downloading;
            Video.ExclusionReason = ExclusionReason.None;
        }

        private CancellationTokenSource _progressCancellationToken;
    }
}
