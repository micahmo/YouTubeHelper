using CommunityToolkit.Maui.Views;
using YouTubeHelper.Mobile.ViewModels;

namespace YouTubeHelper.Mobile.Views
{
    public partial class FilterOptionsPopup : Popup
    {
        public FilterOptionsPopup(ChannelViewModel vm)
        {
            // Set width as a percentage of the device width
            DisplayInfo displayInfo = DeviceDisplay.MainDisplayInfo;
            double percent = 0.85;
            Width = displayInfo.Width / displayInfo.Density * percent;

            InitializeComponent();

            BindingContext = vm;
        }

        public double Width { get; set; }
    }
}