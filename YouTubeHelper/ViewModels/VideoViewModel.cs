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
using YouTubeHelper.Models;
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
        private ICommand _playVideoCommand;

        private async void PlayVideo()
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                MainControlViewModel.ActiveVideo = Video.RawUrl ??= await YouTubeApi.Instance.GetRawUrl(Video.Id);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

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
            // Use silent mode when shift key is down. Grab this value is as soon as possible.
            bool silent = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            // Initiate the download
            string url = $"https://www.youtube.com/watch?v={Video.Id}";

            // Generate request ID
            string requestId = Guid.NewGuid().ToString();

            var resultTask = Settings.Instance.TelegramApiAddress
                .AppendPathSegment("youtube")
                .AppendPathSegment(url, fullyEncode: true)
                .SetQueryParam("apiKey", Settings.Instance.TelegramApiKey)
                .SetQueryParam("silent", silent)
                .SetQueryParam("requestId", requestId)
                .WithTimeout(TimeSpan.FromMinutes(10))
                .GetAsync();

            // Spin up a task to check for progress
            CancellationTokenSource progressCancellationToken = new();
            var _ = Task.Run(async () =>
            {
                while (!progressCancellationToken.IsCancellationRequested)
                {
                    var progressResponse = await Settings.Instance.TelegramApiAddress
                        .AppendPathSegment("progress")
                        .AppendPathSegment(requestId)
                        .AllowAnyHttpStatus() // This will return 400 before the request starts, so ignore it.
                        .GetAsync();

                    if (progressResponse.StatusCode == (int)HttpStatusCode.OK)
                    {
                        double progress = await progressResponse.GetJsonAsync<double>();
                        Video.Status = string.Format(Resources.DownloadingProgress, $"{progress}%");
                    }

                    await Task.Delay(TimeSpan.FromSeconds(.5));
                }
            }, progressCancellationToken.Token);

            App.NotificationManager.Show(string.Empty, string.Format(Resources.VideoDownloadRequested, Video.Title), NotificationType.Information, "NotificationArea", icon: null);

            // Mark as downloading
            Video.Excluded = false;
            Video.Status = Resources.Downloading;
            Video.ExclusionReason = ExclusionReason.None;

            try
            {
                await resultTask;
                App.NotificationManager.Show(string.Empty, string.Format(Resources.VideoDownloadSucceeded, Video.Title), NotificationType.Success, "NotificationArea");

                // Mark as downloaded (only if succeeded)
                Video.Excluded = true;
                Video.Status = null;
                Video.ExclusionReason = ExclusionReason.Watched;
                DatabaseEngine.ExcludedVideosCollection.Upsert(Video);
            }
            catch (Exception ex)
            {
                App.NotificationManager.Show(string.Empty, string.Format(Resources.VideoDownloadFailed, Video.Title, ex.Message.Substring(0, ex.Message.IndexOf(':'))), NotificationType.Error, "NotificationArea");
            }
            finally
            {
                progressCancellationToken.Cancel();
            }
        }

        public ICommand ExcludeVideoCommand => _excludeVideoCommand ??= new RelayCommand<object>(ExcludeVideo);
        private ICommand _excludeVideoCommand;

        private void ExcludeVideo(object sender)
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
            DatabaseEngine.ExcludedVideosCollection.Upsert(Video);

            if (!MainControlViewModel.ShowExcludedVideos)
            {
                _channelViewModel.Videos.Remove(this);
            }
        }

        public ICommand UnexcludeVideoCommand => _enexcludeVideoCommand ??= new RelayCommand(UnexcludeVideo);
        private ICommand _enexcludeVideoCommand;

        private void UnexcludeVideo()
        {
            Video.Excluded = false;
            Video.ExclusionReason = ExclusionReason.None;
            DatabaseEngine.ExcludedVideosCollection.Delete(Video.Id);

            if (_channelViewModel.ExclusionsMode)
            {
                _channelViewModel.Videos.Remove(this);
            }
        }

        public string ExcludedString => $"{Resources.Excluded} - {new ExclusionReasonExtended(Video.ExclusionReason).Description}";

        public Video Video { get; }

        public MainControlViewModel MainControlViewModel { get; }
        
        private readonly ChannelViewModel _channelViewModel;
    }
}
