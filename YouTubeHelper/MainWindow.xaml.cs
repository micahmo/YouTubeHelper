using System.Linq;
using System.Windows;
using System.Windows.Input;
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
            MainControlViewModel.Load();
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
            else if (args.InvokedItem?.ToString() == Properties.Resources.Exclusions)
            {
                MainControlViewModel.Mode = MainControlMode.Exclusions;
            }

            NavigationView.Content = args.IsSettingsInvoked ? SettingsControl : MainControl;
            NavigationView.Header = args.IsSettingsInvoked ? Properties.Resources.Settings : null;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Deselects any control when the window is clicked
            Keyboard.ClearFocus();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            DatabaseEngine.Shutdown();
        }

        private static readonly MainControlViewModel MainControlViewModel = new();
        private static readonly MainControl MainControl = new() {DataContext = MainControlViewModel};

        private static readonly SettingsViewModel SettingsViewModel = new();
        private static readonly SettingsControl SettingsControl = new() { DataContext = SettingsViewModel };
    }
}
