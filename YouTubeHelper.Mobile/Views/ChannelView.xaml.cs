using ServerStatusBot.Definitions.Api;
using System.Collections;
using ServerStatusBot.Definitions.Database.Models;
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

    private async void OnAddChannelTapped(object? sender, TappedEventArgs e)
    {
        (BindingContext as ChannelViewModel)!.IsFabOpen = false;

        var res = await DisplayPromptAsync(Mobile.Resources.Resources.AddChannel, Mobile.Resources.Resources.AddChannelMessage);

        if (string.IsNullOrEmpty(res))
        {
            return;
        }

        List<ChannelViewModel> existingChannels = AppShell.Instance!.AppShellViewModel.ChannelViewModels;

        Channel channel = new Channel
        {
            VanityName = Mobile.Resources.Resources.NewChannel,
            Index = existingChannels.MaxBy(c => c.Channel!.Index)?.Channel!.Index + 1 ?? 0,
            Identifier = res
        };

        // See if we can populate it
        try
        {
            await ServerApiClient.Instance.PopulateChannel(channel);
        }
        catch (Exception ex)
        {
            await DisplayAlert(Mobile.Resources.Resources.Error, string.Format(Mobile.Resources.Resources.UnexpectedError, ex), Mobile.Resources.Resources.OK);
        }

        // If we get here, the new channel was created and populated successfully. Create a ViewModel for it.
        ChannelViewModel channelViewModel = new(AppShell.Instance)
        {
            Channel = channel,
            SelectedSortModeIndex = Preferences.Default.Get(nameof(ChannelViewModel.SelectedSortModeIndex), 0),
            SelectedExclusionFilterIndex = Preferences.Default.Get(nameof(ChannelViewModel.SelectedExclusionFilterIndex), 0),
            Loading = false
        };

        AppShell.Instance.AppShellViewModel.ChannelViewModels.Add(channelViewModel);

        // Now create a view for it and add it to each section
        foreach (Tab tab in new List<Tab> { AppShell.Instance.WatchTab, AppShell.Instance.SearchTab, AppShell.Instance.ExclusionsTab })
        {
            ChannelView channelView = new ChannelView { BindingContext = channelViewModel };
            tab.Items.Add(new ShellContent { Title = channel.VanityName, Content = channelView });
        }

        // And finally, listen for any changes and persist them
        channel.Changed += async (_, _) => { await ServerApiClient.Instance.UpdateChannel(channel); };
    }

    private async void OnDeleteChannelTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is ChannelViewModel channelViewModel)
        {
            channelViewModel.IsFabOpen = false;

            foreach (Tab tab in new List<Tab> { AppShell.Instance!.WatchTab, AppShell.Instance.SearchTab, AppShell.Instance.ExclusionsTab })
            {
                foreach (ShellContent? content in tab.Items)
                {
                    if ((content.Content as ChannelView)?.BindingContext as ChannelViewModel is { } cvm
                        && cvm.Channel?.ChannelPlaylist == channelViewModel.Channel!.ChannelPlaylist)
                    {
                        try
                        {
                            tab.Items.Remove(content);
                        }
                        catch
                        {
                            // This throws an exception, but it gets far enough.
                        }

                        break;
                    }
                }
            }

            AppShell.Instance.AppShellViewModel.ChannelViewModels.Remove(channelViewModel);

            channelViewModel.Channel!.MarkForDeletion = true;
            channelViewModel.Channel!.Persistent = false; // Stop doing updates!
            await ServerApiClient.Instance.UpdateChannel(channelViewModel.Channel);
        }
    }

    private async void OnChangeServerAddressTapped(object? sender, TappedEventArgs e)
    {
        (BindingContext as ChannelViewModel)!.IsFabOpen = false;

        SecureStorage.Default.Remove("server_address");
        await AppShell.Instance!.ConnectToServer();

        // After reconnecting, re-hook up SignalR
        Exception? finalEx = null;

        for (int i = 0; i < 10; ++i)
        {
            if (!string.IsNullOrEmpty(ServerApiClient.BaseUrl))
            {
                try
                {
                    await ServerApiClient.Instance.ReconnectAllGroups();
                    break;
                }
                catch (Exception ex)
                {
                    finalEx = ex;
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            else
            {
                break;
            }
        }

        if (finalEx != null)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlert(Mobile.Resources.Resources.Error, string.Format(Mobile.Resources.Resources.UnexpectedError, finalEx), Mobile.Resources.Resources.OK);
            });
        }
    }
}