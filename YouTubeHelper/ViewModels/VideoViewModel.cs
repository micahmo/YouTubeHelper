using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using CliWrap;
using CliWrap.Buffered;
using Flurl;
using Flurl.Http;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using ModernWpf.Controls;
using MongoDBHelpers;
using Notification.Wpf;
using ServerStatusBot.Definitions.Database;
using ServerStatusBot.Definitions.Database.Models;
using ServerStatusBot.Definitions.Models;
using YouTubeHelper.Properties;
using YouTubeHelper.Utilities;
using YouTubeHelper.Views;

namespace YouTubeHelper.ViewModels
{
    public class VideoViewModel : ObservableObject
    {
        public VideoViewModel(Video video, MainControlViewModel mainControlViewModel, ChannelViewModel channelViewModel)
        {
            Video = video;
            MainControlViewModel = mainControlViewModel;
            _channelViewModel = channelViewModel;

            Video.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(Video.Excluded) || args.PropertyName == nameof(Video.ExclusionReason))
                {
                    OnPropertyChanged(nameof(ExcludedString));
                }
            };
        }

        public ICommand PlayVideoCommand => _playVideoCommand ??= new RelayCommand(PlayVideo);
        private ICommand? _playVideoCommand;

        private async void PlayVideo()
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                MainControlViewModel.ActiveVideo = Video.RawUrl ??= await GetRawUrl(Video.Id);
                MainControlViewModel.ActiveVideoTitle = Video.Title;
                _channelViewModel.Videos.ToList().ForEach(v => v.Video.IsPlaying = false);
                Video.IsPlaying = true;

                // In case the video didn't change, we want to start playing anyway, so always raise the property changed.
                MainControlViewModel.RaisePropertyChanged(nameof(MainControlViewModel.SignalPlayVideo));
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            MainControlViewModel.IsMainControlExpanded = false;
            MainControlViewModel.IsPlayerExpanded = true;
        }

        public ICommand PlayVideoInBrowserCommand => _playVideoInBrowserCommand ??= new RelayCommand(PlayVideoInBrowser);
        private ICommand? _playVideoInBrowserCommand;

        private async void PlayVideoInBrowser()
        {
            _channelViewModel.Videos.ToList().ForEach(v => v.Video.IsPlaying = false);
            Video.IsPlaying = true;

            await Cli.Wrap("freetube.exe")
                .WithArguments($"--url https://www.youtube.com/watch?v={Video.Id}")
                .ExecuteBufferedAsync();
        }

        public ICommand DownloadVideoCommand => _downloadVideoCommand ??= new RelayCommand(() => DownloadVideo(false));
        private ICommand? _downloadVideoCommand;

        public ICommand DownloadVideoCommandRightClick => _downloadVideoCommandRightClick ??= new RelayCommand(() => DownloadVideo(true));
        private ICommand? _downloadVideoCommandRightClick;

        private async void DownloadVideo(bool rightClick)
        {
            if (_progressCancellationToken is not null)
            {
                // There is a download in progress and we've been told to cancel it.
                _progressCancellationToken.Cancel();
                return;
            }

            _progressCancellationToken = new();

            // Use silent mode when shift key is down. Grab this value is as soon as possible.
            bool silent = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            // Initiate the download
            string url = $"https://www.youtube.com/watch?v={Video.Id}";

            // Generate request ID
            string requestId = Guid.NewGuid().ToString();

            if (rightClick)
            {
                (string Text, ContentDialogResult Result) res = await MessageBoxHelper.ShowInputBox(Resources.DownloadDirectoryMessage, Resources.DownloadDirectoryMessage, Settings.Instance.DownloadDirectory);

                if (res.Result != ContentDialogResult.Primary || string.IsNullOrWhiteSpace(res.Text))
                {
                    // Canceled or entered nothing
                    _progressCancellationToken = null;
                    return;
                }

                Settings.Instance.DownloadDirectory = res.Text;
                MainControlViewModel.RaisePropertyChanged(nameof(MainControlViewModel.CurrentDownloadDirectoryLabel));
            }

            UnexcludeVideo();

            try
            {
                await Settings.Instance.ServerAddress
                    .AppendPathSegment("youtube")
                    .AppendPathSegment(url, fullyEncode: true)
                    .SetQueryParam("apiKey", Settings.Instance.ServerApiKey)
                    .SetQueryParam("silent", silent)
                    .SetQueryParam("requestId", requestId)
                    .SetQueryParam("dataDirectorySubpath", Settings.Instance.DownloadDirectory)
                    .SetQueryParam("videoId", Video.Id)
                    .SetQueryParam("channelPlaylist", Video.ChannelPlaylist)
                    .SetQueryParam("idInChannelFolder", Settings.Instance.DownloadDirectory.Equals("jellyfin", StringComparison.OrdinalIgnoreCase) ? false : true)
                    .GetAsync();
            }
            catch (Exception ex)
            {
                App.NotificationManager.Show(string.Empty, string.Format(Resources.VideoDownloadFailed, Video.Title, ex.Message.Substring(0, ex.Message.IndexOf(':'))), NotificationType.Error, "NotificationArea");
                _progressCancellationToken = null;
                return;
            }

            StartUpdateCheck(requestId);

            App.NotificationManager.Show(string.Empty, string.Format(Resources.VideoDownloadRequested, Video.Title), NotificationType.Information, "NotificationArea", icon: null);

            // Mark as downloading
            Video.Excluded = false;
            Video.Status = Resources.Downloading;
            Video.ExclusionReason = ExclusionReason.None;
            Video.Progress = 1;
            MainControlViewModel.RaisePropertyChanged(nameof(MainControlViewModel.ActiveDownloadsCountLabel));
            MainControlViewModel.RaisePropertyChanged(nameof(MainControlViewModel.CumulativeDownloadProgress));
        }

        private CancellationTokenSource? _progressCancellationToken;

        internal void StartUpdateCheck(string requestId, bool showInAppNotifications = true)
        {
            _progressCancellationToken ??= new();
            
            // Spin up a task to check for progress
            _ = Task.Run(async () =>
            {
                bool statusWasEverNotDone = false;
                int errorCount = 0;

                while (_progressCancellationToken?.IsCancellationRequested == false)
                {
                    IFlurlResponse progressResponse;
                    try
                    {
                        progressResponse = await Settings.Instance.ServerAddress
                            .AppendPathSegment("v2")
                            .AppendPathSegment("progress")
                            .AppendPathSegment(requestId)
                            .AllowAnyHttpStatus() // This will return 400 before the request starts, so ignore it.
                            .GetAsync(HttpCompletionOption.ResponseContentRead, _progressCancellationToken.Token);

                        // If we make a successful call, error count resets.
                        errorCount = 0;
                    }
                    catch (Exception ex)
                    {
                        if (++errorCount == 5)
                        {
                            App.NotificationManager.Show(string.Empty, string.Format(Resources.VideoDownloadFailedException, Video.Title, ex.Message), NotificationType.Error, "NotificationArea");

                            return;
                        }

                        continue;
                    }

                    if (progressResponse.StatusCode == (int)HttpStatusCode.OK)
                    {
                        RequestData result = await progressResponse.GetJsonAsync<RequestData>();
                        Video.Status = string.Format(Resources.DownloadingProgress, $"{result.Progress}%");
                        Video.Progress = result.Progress == 0 ? 1 : result.Progress;
                        MainControlViewModel.RaisePropertyChanged(nameof(MainControlViewModel.ActiveDownloadsCountLabel));
                        MainControlViewModel.RaisePropertyChanged(nameof(MainControlViewModel.CumulativeDownloadProgress));

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
                                // NOTE: We no longer need to update the db here because the server does it,
                                // so the above is purely a UI update.
                            }

                            MainControlViewModel.RaisePropertyChanged(nameof(MainControlViewModel.ActiveDownloadsCountLabel));
                            MainControlViewModel.RaisePropertyChanged(nameof(MainControlViewModel.CumulativeDownloadProgress));

                            if (showInAppNotifications)
                            {
                                App.NotificationManager.Show(string.Empty, string.Format(Resources.VideoDownloadSucceeded, Video.Title), NotificationType.Success, "NotificationArea");
                            }

                            return;
                        }

                        if (result.Status == DownloadStatus.Failed)
                        {
                            Video.Status = Resources.FailedToDownload;

                            if (showInAppNotifications)
                            {
                                string status = result.Status.ToString();

                                if (!string.IsNullOrEmpty(result.Reason))
                                {
                                    status += $" - {result.Reason}";
                                }

                                App.NotificationManager.Show(string.Empty, string.Format(Resources.VideoDownloadFailed, Video.Title, status), NotificationType.Error, "NotificationArea");
                            }

                            return;
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }).ContinueWith(_ =>
            {
                _progressCancellationToken = null;
            });
        }

        public ICommand ExcludeVideoCommand => _excludeVideoCommand ??= new RelayCommand<object>(ExcludeVideo);
        private ICommand? _excludeVideoCommand;

        private async void ExcludeVideo(object? sender)
        {
            MainControl.OpenFlyouts.ForEach(f => f.Hide());
            MainControl.OpenFlyouts.Clear();

            if (sender is Button button)
            {
                if (button.Content?.ToString() == Resources.Watched)
                {
                    Video.ExclusionReason = ExclusionReason.Watched;
                }
                else if (button.Content?.ToString() == Resources.WontWatch)
                {
                    Video.ExclusionReason = ExclusionReason.WontWatch;
                }
                else if (button.Content?.ToString() == Resources.MightWatch)
                {
                    Video.ExclusionReason = ExclusionReason.MightWatch;
                }
            }

            Video.Excluded = true;
            await Collections.ExcludedVideosCollection.UpsertAsync<Video, string>(Video);

            if (MainControlViewModel is { WatchMode: true, ShowExcludedVideos: false })
            {
                _channelViewModel.Videos.Remove(this);
            }
        }

        public ICommand UnexcludeVideoCommand => _enexcludeVideoCommand ??= new RelayCommand(UnexcludeVideo);
        private ICommand? _enexcludeVideoCommand;

        private async void UnexcludeVideo()
        {
            Video.Excluded = false;
            Video.ExclusionReason = ExclusionReason.None;
            await Collections.ExcludedVideosCollection.DeleteAsync(Video.Id);
        }

        public string ExcludedString => $"{Resources.Excluded} - {new ExclusionReasonExtended(Video.ExclusionReason).Description}";

        public Video Video { get; }

        public bool IsDescriptionExpanded
        {
            get => _isDescriptionExpanded;
            set => SetProperty(ref _isDescriptionExpanded, value);
        }
        private bool _isDescriptionExpanded;

        public MainControlViewModel MainControlViewModel { get; }
        
        private readonly ChannelViewModel _channelViewModel;

        public static async Task<string> GetRawUrl(string videoId)
        {
            try
            {
                return await (await Settings.Instance.ServerAddress
                    .AppendPathSegment("youtubelink")
                    .AppendPathSegment(videoId)
                    .SetQueryParam("apiKey", Settings.Instance.ServerApiKey)
                    .GetAsync()).GetStringAsync();
            }
            catch
            {
                return "https://google.com";
            }
        }
    }
}
