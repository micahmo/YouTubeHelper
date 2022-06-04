using System.Windows.Input;
using Microsoft.Toolkit.Mvvm.Input;
using YouTubeHelper.Models;
using YouTubeHelper.Utilities;

namespace YouTubeHelper.ViewModels
{
    public class VideoViewModel
    {
        public VideoViewModel(Video video, MainControlViewModel mainControlViewModel)
        {
            Video = video;
            _mainControlViewModel = mainControlViewModel;
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

        public Video Video { get; }

        private readonly MainControlViewModel _mainControlViewModel;
    }
}
