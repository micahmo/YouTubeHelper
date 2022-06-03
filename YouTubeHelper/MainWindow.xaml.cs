using System.Linq;
using System.Windows;
using ModernWpf.Controls;
using YouTubeHelper.ViewModels;
using YouTubeHelper.Views;

namespace YouTubeHelper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void NavigationView_Loaded(object sender, RoutedEventArgs e)
        {
            NavigationView.SelectedItem = NavigationView.MenuItems.OfType<NavigationViewItem>().First();
            NavigationView.Content = MainControl;
        }

        private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItem?.ToString() == Properties.Resources.Watch)
            {
                MainControlViewModel.Mode = MainControlMode.Watch;
            }
            else if (args.InvokedItem?.ToString() == Properties.Resources.Search)
            {
                MainControlViewModel.Mode = MainControlMode.Search;
            }

            NavigationView.Header = args.IsSettingsInvoked ? Properties.Resources.Settings : null;
        }

        private static readonly MainControlViewModel MainControlViewModel = new();
        private static readonly MainControl MainControl = new() {DataContext = MainControlViewModel};
    }
}
