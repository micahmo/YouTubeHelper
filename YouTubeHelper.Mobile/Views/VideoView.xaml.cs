using YouTubeHelper.Mobile.ViewModels;

namespace YouTubeHelper.Mobile.Views;

public partial class VideoView : ContentView
{
    public VideoView()
    {
        InitializeComponent();
        Loaded += Changed;
        LayoutChanged += Changed;
        SizeChanged += Changed;
    }

    private void Changed(object sender, EventArgs e)
    {
        if (BindingContext is VideoViewModel videoViewModel)
        {
            videoViewModel.ThumbnailHeight = 9.0 / 16.0 * (VideoGrid.Width / 2);
        }
    }
}