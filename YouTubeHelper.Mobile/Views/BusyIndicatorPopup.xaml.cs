 using CommunityToolkit.Maui.Views;

namespace YouTubeHelper.Mobile.Views
{
    public partial class BusyIndicatorPopup : Popup
    {
        public static readonly BindableProperty TextProperty =
            BindableProperty.Create(nameof(Text), typeof(string), typeof(BusyIndicatorPopup), Mobile.Resources.Resources.Loading);

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }


        public BusyIndicatorPopup()
        {
            InitializeComponent();
            CanBeDismissedByTappingOutsideOfPopup = false;
        }
    }

    public class BusyIndicator : IDisposable
    {
        private readonly BusyIndicatorPopup _busyIndicatorPopup;
        
        public string Text { set => _busyIndicatorPopup.Text = value; }

        public BusyIndicator(Page page, string? text)
        {
            text ??= Resources.Resources.Loading;

            _busyIndicatorPopup = new()
            {
                Text = text
            };
            page.ShowPopup(_busyIndicatorPopup);
        }
        
        public void Dispose()
        {
            _busyIndicatorPopup.Close();
        }
    }
}