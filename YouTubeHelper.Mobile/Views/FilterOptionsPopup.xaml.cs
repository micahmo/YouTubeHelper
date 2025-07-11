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

            //// Set width as a percentage of the device width
            //DisplayInfo displayInfo = DeviceDisplay.MainDisplayInfo;
            //double percent = 0.85;
            //double width = displayInfo.Width / displayInfo.Density * percent;
            //Size = new Size(width, -1);
        }
    }
}