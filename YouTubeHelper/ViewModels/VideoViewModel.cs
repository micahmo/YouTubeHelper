using System;
using System.Net;
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
using Notification.Wpf;
using YouTubeHelper.Properties;
using YouTubeHelper.Shared;
using YouTubeHelper.Shared.Models;
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
        private ICommand _playVideoCommand;

        private async void PlayVideo()
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                MainControlViewModel.ActiveVideo = Video.RawUrl ??= await GetRawUrl(Video.Id);
                MainControlViewModel.ActiveVideoTitle = Video.Title;

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
        private ICommand _playVideoInBrowserCommand;

        private async void PlayVideoInBrowser()
        {
            await Cli.Wrap(Settings.Instance.ChromePath)
                .WithArguments($"-incognito https://www.youtube.com/watch?v={Video.Id}")
                .ExecuteBufferedAsync();
        }

        public ICommand DownloadVideoCommand => _downloadVideoCommand ??= new RelayCommand(DownloadVideo);
        private ICommand _downloadVideoCommand;

        private async void DownloadVideo()
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

            try
            {
                await Settings.Instance.TelegramApiAddress
                    .AppendPathSegment("youtube")
                    .AppendPathSegment(url, fullyEncode: true)
                    .SetQueryParam("apiKey", Settings.Instance.TelegramApiKey)
                    .SetQueryParam("silent", silent)
                    .SetQueryParam("requestId", requestId)
                    .GetAsync();
            }
            catch (Exception ex)
            {
                App.NotificationManager.Show(string.Empty, string.Format(Resources.VideoDownloadFailed, Video.Title, ex.Message.Substring(0, ex.Message.IndexOf(':'))), NotificationType.Error, "NotificationArea");
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
                        App.NotificationManager.Show(string.Empty, string.Format(Resources.VideoDownloadFailed, Video.Title, ex.Message.Substring(0, ex.Message.IndexOf(':'))), NotificationType.Error, "NotificationArea");
                        return;
                    }

                    if (progressResponse.StatusCode == (int)HttpStatusCode.OK)
                    {
                        dynamic result = await progressResponse.GetJsonAsync();
                        Video.Status = string.Format(Resources.DownloadingProgress, $"{result.progress}%");

                        if (result.status == 1)
                        {
                            // Mark as downloaded (only if succeeded)
                            Video.Excluded = true;
                            Video.Status = null;
                            Video.ExclusionReason = ExclusionReason.Watched;
                            await DatabaseEngine.ExcludedVideosCollection.UpsertAsync<Video, string>(Video);

                            App.NotificationManager.Show(string.Empty, string.Format(Resources.VideoDownloadSucceeded, Video.Title), NotificationType.Success, "NotificationArea");
                            return;
                        }

                        if (result.status == 2)
                        {
                            App.NotificationManager.Show(string.Empty, string.Format(Resources.VideoDownloadFailed, Video.Title, result.status), NotificationType.Error, "NotificationArea");
                            return;
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(.5));
                }
            }).ContinueWith(_ =>
            {
                _progressCancellationToken = null;
            });

            App.NotificationManager.Show(string.Empty, string.Format(Resources.VideoDownloadRequested, Video.Title), NotificationType.Information, "NotificationArea", icon: null);

            // Mark as downloading
            Video.Excluded = false;
            Video.Status = Resources.Downloading;
            Video.ExclusionReason = ExclusionReason.None;
        }

        private CancellationTokenSource _progressCancellationToken;

        public ICommand ExcludeVideoCommand => _excludeVideoCommand ??= new RelayCommand<object>(ExcludeVideo);
        private ICommand _excludeVideoCommand;

        private async void ExcludeVideo(object sender)
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
            await DatabaseEngine.ExcludedVideosCollection.UpsertAsync<Video, string>(Video);

            if (!MainControlViewModel.ShowExcludedVideos)
            {
                _channelViewModel.Videos.Remove(this);
            }
        }

        public ICommand UnexcludeVideoCommand => _enexcludeVideoCommand ??= new RelayCommand(UnexcludeVideo);
        private ICommand _enexcludeVideoCommand;

        private async void UnexcludeVideo()
        {
            Video.Excluded = false;
            Video.ExclusionReason = ExclusionReason.None;
            await DatabaseEngine.ExcludedVideosCollection.DeleteAsync(Video.Id);
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
    }
}
