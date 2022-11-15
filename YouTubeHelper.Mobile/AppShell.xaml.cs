using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using YouTubeHelper.Mobile.ViewModels;
using YouTubeHelper.Mobile.Views;
using YouTubeHelper.Shared;
using YouTubeHelper.Shared.Models;

namespace YouTubeHelper.Mobile
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            BindingContext = new AppShellViewModel(this);
        }

        private async void Shell_Loaded(object sender, System.EventArgs e)
        {
            await ConnectToDatabase();

            WatchTab.Items.Clear();
            SearchTab.Items.Clear();
            ExclusionsTab.Items.Clear();

            var channels = DatabaseEngine.ChannelCollection.FindAll().AsEnumerable().OrderByDescending(c => c.Index).ToList();
            foreach (Channel channel in channels)
            {
                ChannelViewModel channelViewModel = new(this)
                {
                    Channel = channel, 
                    SelectedSortModeIndex = Preferences.Default.Get(nameof(ChannelViewModel.SelectedSortModeIndex), 0)
                };
                AppShellViewModel.ChannelViewModels.Add(channelViewModel);

                var channelView = new ChannelView { BindingContext = channelViewModel };

                WatchTab.Items.Insert(0, new ShellContent { Title = channel.VanityName, Content = channelView });
                SearchTab.Items.Insert(0, new ShellContent { Title = channel.VanityName, Content = channelView });
                ExclusionsTab.Items.Insert(0, new ShellContent { Title = channel.VanityName, Content = channelView });
            }
        }

        private async Task ConnectToDatabase()
        {
            bool connected = false;

            using (new BusyIndicator(this))
            {
                try
                {
                    string connectionString = await SecureStorage.Default.GetAsync("connection_string");
                    DatabaseEngine.ConnectionString = connectionString;
                    if (string.IsNullOrEmpty(DatabaseEngine.TestConnection()))
                    {
                        connected = true;
                    }
                }
                catch
                {
                    // We'll fall into the next block which prompts the user to re-enter
                }
            }

            while (!connected)
            {
                var res = await DisplayPromptAsync(Mobile.Resources.Resources.Database, Mobile.Resources.Resources.EnterConnectionString);
                
                if (string.IsNullOrEmpty(res))
                {
                    Environment.Exit(1);
                }

                DatabaseEngine.ConnectionString = res;

                using (new BusyIndicator(this))
                {
                    if (DatabaseEngine.TestConnection() is { } err)
                    {
                        await DisplayAlert(Mobile.Resources.Resources.Error, string.Format(Mobile.Resources.Resources.ErrorConnectingToDatabase, err), Mobile.Resources.Resources.OK);
                    }
                    else
                    {
                        connected = true;
                    }
                }
            }

            // We made it here, so we must have connected successfully. Save the connection string.
            await SecureStorage.Default.SetAsync("connection_string", DatabaseEngine.ConnectionString);
        }

        public AppShellViewModel AppShellViewModel => BindingContext as AppShellViewModel;

        private void Shell_Navigated(object _, ShellNavigatedEventArgs __)
        {
            AppShellViewModel.RaisePropertyChanged(nameof(AppShellViewModel.WatchTabSelected));
            AppShellViewModel.RaisePropertyChanged(nameof(AppShellViewModel.SearchTabSelected));
            AppShellViewModel.RaisePropertyChanged(nameof(AppShellViewModel.ExclusionsTabSelected));

            AppShellViewModel.ChannelViewModels.ForEach(c => c.Videos.Clear());
        }
    }
}