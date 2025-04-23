using System.Collections;
using YouTubeHelper.Mobile.ViewModels;

namespace YouTubeHelper.Mobile.Views;

public partial class ChannelView : ContentPage
{
    public ChannelView()
    {
        InitializeComponent();

        BindingContextChanged += OnBindingContextChanged;
    }

    private void OnBindingContextChanged(object? sender, EventArgs e)
    {
        if (BindingContext is ChannelViewModel channelViewModel)
        {
            channelViewModel.ScrollToTopRequested = () =>
            {
                if (VideosCollectionView.ItemsSource is IList { Count: > 0 })
                {
                    VideosCollectionView.ScrollTo(0, position: ScrollToPosition.Start, animate: true);
                }
            };
        }
    }
}