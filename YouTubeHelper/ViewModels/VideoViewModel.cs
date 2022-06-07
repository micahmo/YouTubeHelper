using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CliWrap;
using CliWrap.Buffered;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using YouTubeHelper.Models;
using YouTubeHelper.Utilities;
using YouTubeHelper.Views;

namespace YouTubeHelper.ViewModels
{
    public class VideoViewModel : ObservableObject
    {
        public VideoViewModel(Video video, MainControlViewModel mainControlViewModel, ChannelViewModel channelViewModel)
        {
            Video = video;
            _mainControlViewModel = mainControlViewModel;
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
                _mainControlViewModel.ActiveVideo = Video.RawUrl ??= await YouTubeApi.Instance.GetRawUrl(Video.Id);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            _mainControlViewModel.IsPlayerExpanded = true;
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

        private void DownloadVideo()
        {
            // Initiate the download
            Clipboard.SetText($"https://www.youtube.com/watch?v={Video.Id}");
            Cli.Wrap(Settings.Instance.TelegramPath)
                .WithArguments($@"-- ""tg://resolve/?domain={Settings.Instance.TelegramBotId}""")
                .ExecuteBufferedAsync();

            // Mark as downloaded
            Video.Excluded = true;
            Video.ExclusionReason = ExclusionReason.Watched;
            DatabaseEngine.ExcludedVideosCollection.Upsert(Video);

            if (!_mainControlViewModel.ShowExcludedVideos)
            {
                _channelViewModel.Videos.Remove(this);
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
                if (button.Content?.ToString() == Properties.Resources.Watched)
                {
                    Video.ExclusionReason = ExclusionReason.Watched;
                }
                else if (button.Content?.ToString() == Properties.Resources.WontWatch)
                {
                    Video.ExclusionReason = ExclusionReason.WontWatch;
                }
                else if (button.Content?.ToString() == Properties.Resources.MightWatch)
                {
                    Video.ExclusionReason = ExclusionReason.MightWatch;
                }
            }

            Video.Excluded = true;
            DatabaseEngine.ExcludedVideosCollection.Upsert(Video);

            if (!_mainControlViewModel.ShowExcludedVideos)
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

        public string ExcludedString => $"{Properties.Resources.Excluded} - {new ExclusionReasonExtended(Video.ExclusionReason).Description}";

        public Video Video { get; }

        private readonly MainControlViewModel _mainControlViewModel;
        private readonly ChannelViewModel _channelViewModel;
    }
}
