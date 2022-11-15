 using CommunityToolkit.Maui.Views;

namespace YouTubeHelper.Mobile.Views
{
    public partial class BusyIndicatorPopup : Popup
    {
        public BusyIndicatorPopup()
        {
            InitializeComponent();
            CanBeDismissedByTappingOutsideOfPopup = false;
        }
    }

    public class BusyIndicator : IDisposable
    {
        private readonly BusyIndicatorPopup _busyIndicatorPopup;
        
        public BusyIndicator(Page page)
        {
            _busyIndicatorPopup = new();
            page.ShowPopup(_busyIndicatorPopup);
        }
        
        public void Dispose()
        {
            _busyIndicatorPopup.Close();
        }
    }
}