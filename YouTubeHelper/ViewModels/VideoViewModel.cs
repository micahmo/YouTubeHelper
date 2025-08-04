using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using CliWrap;
using CliWrap.Buffered;
using Flurl;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using ModernWpf.Controls;
using Notification.Wpf;
using ServerStatusBot.Definitions.Api;
using ServerStatusBot.Definitions.Database.Models;
using ServerStatusBot.Definitions.Models;
using YouTubeHelper.Properties;
using YouTubeHelper.Shared;
using YouTubeHelper.Utilities;
using YouTubeHelper.Views;

namespace YouTubeHelper.ViewModels
{
    public class VideoViewModel : ObservableObject, IVideoViewModel
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
            if (_previousRequestId != null)
            {
                await ServerApiClient.Instance.LeaveDownloadGroup(_previousRequestId);
                _previousRequestId = null;
            }

            // Use silent mode when shift key is down. Grab this value is as soon as possible.
            bool silent = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            // Initiate the download
            string url = $"https://www.youtube.com/watch?v={Video.Id}";

            // Generate request ID
            string requestId = Guid.NewGuid().ToString();

            if (rightClick)
            {
                (string Text, ContentDialogResult Result) res = await MessageBoxHelper.ShowInputBox(Resources.DownloadDirectoryMessage, Resources.DownloadDirectoryMessage, Settings.Instance!.DownloadDirectory);

                if (res.Result != ContentDialogResult.Primary || string.IsNullOrWhiteSpace(res.Text))
                {
                    // Canceled or entered nothing
                    if (_previousRequestId != null)
                    {
                        await ServerApiClient.Instance.LeaveDownloadGroup(_previousRequestId);
                        _previousRequestId = null;
                    }
                    return;
                }

                Settings.Instance!.DownloadDirectory = res.Text;
                MainControlViewModel.RaisePropertyChanged(nameof(MainControlViewModel.CurrentDownloadDirectoryLabel));
            }

            UnexcludeVideo();

            try
            {
                await ServerApiClient.Instance.DownloadVideo(
                    url: url,
                    silent: true,
                    requestId: requestId,
                    dataDirectorySubpath: Settings.Instance!.DownloadDirectory,
                    videoId: Video.Id,
                    videoName: Video.Title ?? string.Empty,
                    thumbnailUrl: Video.ThumbnailUrl ?? string.Empty,
                    channelPlaylist: Video.ChannelPlaylist,
                    idInChannelFolder: Settings.Instance.DownloadDirectory.Equals("jellyfin", StringComparison.OrdinalIgnoreCase) ? false : true);
            }
            catch (Exception ex)
            {
                App.NotificationManager.Show(string.Empty, string.Format(Resources.VideoDownloadFailed, Video.Title, ex.Message.Substring(0, ex.Message.IndexOf(':'))), NotificationType.Error, "NotificationArea");
                return;
            }

            try
            {
                // Try to hook up for progress updates
                await ServerApiClient.Instance.JoinDownloadGroup(requestId, requestData => UpdateCheck(requestId, requestData));
            }
            catch (Exception ex)
            {
                App.NotificationManager.Show(string.Empty, string.Format(Resources.ProgressUpdateFailed, Video.Title, ex.Message), NotificationType.Error, "NotificationArea");
            }

            App.NotificationManager.Show(string.Empty, string.Format(Resources.VideoDownloadRequested, Video.Title), NotificationType.Information, "NotificationArea", icon: null);

            // Mark as downloading
            Video.Excluded = false;
            Video.Status = string.Format(Resources.DownloadingProgress, "0.0%");
            Video.ExclusionReason = ExclusionReason.None;
            Video.Progress = 1;
            MainControlViewModel.RaisePropertyChanged(nameof(MainControlViewModel.ActiveDownloadsCountLabel));
            MainControlViewModel.RaisePropertyChanged(nameof(MainControlViewModel.CumulativeDownloadProgress));
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

            Video.Status = string.Format(Resources.DownloadingProgress, $"{result.Progress:F1}%");
            Video.Progress = result.Progress == 0 ? 1 : result.Progress;
            MainControlViewModel.RaisePropertyChanged(nameof(MainControlViewModel.ActiveDownloadsCountLabel));
            MainControlViewModel.RaisePropertyChanged(nameof(MainControlViewModel.CumulativeDownloadProgress));

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

                MainControlViewModel.RaisePropertyChanged(nameof(MainControlViewModel.ActiveDownloadsCountLabel));
                MainControlViewModel.RaisePropertyChanged(nameof(MainControlViewModel.CumulativeDownloadProgress));

                if (showInAppNotifications && _statusWasEverNotDone)
                {
                    App.NotificationManager.Show(string.Empty, string.Format(Resources.VideoDownloadSucceeded, Video.Title), NotificationType.Success, "NotificationArea");
                }

                ServerApiClient.Instance.LeaveDownloadGroup(requestId);
                _statusWasEverNotDone = false;
            }

            if (result.Status == DownloadStatus.Failed)
            {
                Video.Status = Resources.FailedToDownload;

                if (showInAppNotifications && _statusWasEverNotDone && result.Status != null)
                {
                    string status = result.Status!.ToString()!;

                    if (!string.IsNullOrEmpty(result.Reason))
                    {
                        status += $" - {result.Reason}";
                    }

                    App.NotificationManager.Show(string.Empty, string.Format(Resources.VideoDownloadFailed, Video.Title, status), NotificationType.Error, "NotificationArea");
                }

                ServerApiClient.Instance.LeaveDownloadGroup(requestId);
                _statusWasEverNotDone = false;
            }
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
            await ServerApiClient.Instance.UpdateVideo(Video, MainWindow.ClientId);

            if (MainControlViewModel is { ChannelMode: true, ShowExclusions: false })
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
            Video.MarkForDeletion = true;
            await ServerApiClient.Instance.UpdateVideo(Video, MainWindow.ClientId);
            Video.MarkForDeletion = false;
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

        /// <summary>
        /// Wraps the <see cref="Video.Description"/> and returns it in such a way that it can be displayed with Inlines in a TextBlock
        /// </summary>
        public Inline[] FormattedDescription
        {
            get
            {
                List<Inline> inlines = new List<Inline>();
                Regex urlRegex = new Regex(@"(https?:\/\/[^\s]+)", RegexOptions.Compiled);
                string[] parts = urlRegex.Split(Video.Description ?? "");

                foreach (string part in parts)
                {
                    if (urlRegex.IsMatch(part))
                    {
                        Hyperlink hyperlink = new Hyperlink(new Run(part))
                        {
                            NavigateUri = new Uri(part)
                        };

                        hyperlink.Click += async (_, e) =>
                        {
                            e.Handled = true;

                            Url? url = Url.Parse(part);
                            if (url.Host.EndsWith("youtube.com") || url.Host.EndsWith("youtu.be"))
                            {
                                ContentDialogResult res = await MessageBoxHelper.Show(string.Format(Resources.OpenYouTubeLinkMessage, part),
                                    Resources.OpenYouTubeLinkTitle,
                                    MessageBoxButton.OKCancel,
                                    primaryButtonText: Resources.OpenInYouTubeHelper,
                                    secondaryButtonText: Resources.OpenExternally);

                                if (res == ContentDialogResult.Primary)
                                {
                                    MainWindow.Instance?.HandleSharedLink(part);
                                }
                                else
                                {
                                    Process.Start(new ProcessStartInfo(part) { UseShellExecute = true });
                                }
                            }
                            else
                            {
                                Process.Start(new ProcessStartInfo(part) { UseShellExecute = true });
                            }
                        };

                        inlines.Add(hyperlink);
                    }
                    else
                    {
                        inlines.Add(new Run(part));
                    }
                }

                return inlines.ToArray();
            }
        }
    }
}
