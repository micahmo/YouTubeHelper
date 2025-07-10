using CommunityToolkit.Maui.Views;
using YouTubeHelper.Mobile.ViewModels;

namespace YouTubeHelper.Mobile.Views
{
    public partial class FilterOptionsPopup : Popup
    {
        public FilterOptionsPopup(ChannelViewModel vm)
        {
            InitializeComponent();

            BindingContext = vm;
        }
    }
}