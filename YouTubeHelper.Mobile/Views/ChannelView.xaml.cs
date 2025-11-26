using ServerStatusBot.Definitions.Api;
using System.Collections;
using System.Text;
using Android.Webkit;
using CommunityToolkit.Maui.Alerts;
using ServerStatusBot.Definitions.Database.Models;
using YouTubeHelper.Mobile.ViewModels;
using CommunityToolkit.Maui.Views;
using WebView = Microsoft.Maui.Controls.WebView;
using CommunityToolkit.Maui.Core;

namespace YouTubeHelper.Mobile.Views;

public partial class ChannelView : ContentPage
{
    public ChannelView()
    {
        InitializeComponent();

        BindingContextChanged += OnBindingContextChanged;
    }

    /// <inheritdoc/>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateFooter();
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

            channelViewModel.ChannelView = this;
        }
    }

    private async void OnMoveLeftTapped(object? sender, TappedEventArgs e)
    {
        await MoveChannelRelativeAsync(-1);
    }

    private async void OnMoveRightTapped(object? sender, TappedEventArgs e)
    {
        await MoveChannelRelativeAsync(1);
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
            await ServerApiClient.Instance.PopulateChannel(channel, AppShell.ClientId);
        }
        catch (Exception ex)
        {
            await DisplayAlert(Mobile.Resources.Resources.Error, string.Format(Mobile.Resources.Resources.UnexpectedError, ex), Mobile.Resources.Resources.OK);
        }

        // If we get here, the new channel was created and populated successfully. Create a ViewModel for it.
        ChannelViewModel channelViewModel = new(AppShell.Instance)
        {
            Channel = channel,
            SelectedSortModeIndex = Preferences.Default.Get(nameof(ChannelViewModel.SelectedSortModeIndex), 4),
            SelectedExclusionsModeIndex = Preferences.Default.Get(nameof(ChannelViewModel.SelectedExclusionsModeIndex), 1),
            SelectedExclusionFilterIndex = Preferences.Default.Get(nameof(ChannelViewModel.SelectedExclusionFilterIndex), 0),
            Loading = false
        };

        AppShell.Instance.AppShellViewModel.ChannelViewModels.Add(channelViewModel);

        // Now create a view for it and add it to each section
        foreach (Tab tab in new List<Tab> { AppShell.Instance.ChannelTab })
        {
            ChannelView channelView = new ChannelView { BindingContext = channelViewModel };
            tab.Items.Add(new ShellContent { Title = channel.VanityName, Content = channelView });
        }

        // And finally, listen for any changes and persist them
        channel.Changed += async (_, _) =>
        {
            await ServerApiClient.Instance.UpdateChannel(channel, AppShell.ClientId);
        };
    }

    private async void OnDeleteChannelTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is ChannelViewModel channelViewModel)
        {
            channelViewModel.IsFabOpen = false;

            foreach (Tab tab in new List<Tab> { AppShell.Instance!.ChannelTab })
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
            await ServerApiClient.Instance.UpdateChannel(channelViewModel.Channel, AppShell.ClientId);
        }
    }

    private async void OnUploadCookiesTapped(object? _, TappedEventArgs __)
    {
        if (BindingContext is ChannelViewModel channelViewModel)
        {
            channelViewModel.IsFabOpen = false;

            // Show a WebView (not system browser; we want control) to let the user log into YouTube
            WebView webView = new WebView { Source = "https://accounts.google.com/ServiceLogin?service=youtube" };

            // Handle the navigated event so we can get the cookies once we're redirected to YouTube (meaning a successful login)
            webView.Navigated += HandleWebViewNavigation;

            // Create a page to hold the web view
            ContentPage loginPage = new ContentPage
            {
                Title = "YouTube Login",
                Content = new Grid { webView }
            };

            // Add a back button
            ToolbarItem backButton = new ToolbarItem
            {
                IconImageSource = new FontImageSource
                {
                    FontFamily = "ionicons.ttf#",
                    Glyph = "\uF406",
                    Size = 30,
                    Color = Colors.White
                },
                Command = new Command(async () => await Shell.Current.Navigation.PopModalAsync())
            };
            loginPage.ToolbarItems.Add(backButton);

            // Navigate to it
            NavigationPage navigationPage = new NavigationPage(loginPage)
            {
                BarTextColor = Colors.White
            };
            await Shell.Current.Navigation.PushModalAsync(navigationPage);
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

    private static readonly SemaphoreSlim FabLock = new(1, 1);

    public async Task ToggleFabExpander(bool isExpanded)
    {
        await FabLock.WaitAsync();

        try
        {
            // --- Update the icon --- //
            if (FabIconLabel == null)
            {
                return;
            }

            FabIconLabel.Text = isExpanded ? "\uF406" : "•••"; // \uF406 is the Ionicons "close" or "X" glyph
            FabIconLabel.FontSize = isExpanded ? 35 : 23;

            // --- Dim the background --- //
            Task dimTask = Task.Run(async () =>
            {
                if (BoxViewDim == null)
                {
                    return;
                }

                if (isExpanded)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        BoxViewDim.Opacity = 0;
                        BoxViewDim.IsVisible = true;
                    });

                    await BoxViewDim.FadeTo(0.3, 250, Easing.SinOut);
                }
                else
                {
                    await BoxViewDim.FadeTo(0, 200, Easing.SinIn);

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        BoxViewDim.IsVisible = false;
                    });
                }
            });

            // --- Animate the Items --- //
            if (FabExpander is { Content: Layout layout })
            {
                if (isExpanded)
                {
                    // Opening animation
                    layout.IsVisible = true;
                    layout.TranslationY = 50;
                    layout.Opacity = 0;

                    await Task.WhenAll(
                        layout.TranslateTo(0, 0, 250, Easing.SinOut),
                        layout.FadeTo(1)
                    );
                }
                else
                {
                    // Closing animation
                    await Task.WhenAll(
                        layout.TranslateTo(0, 50, 200, Easing.SinIn),
                        layout.FadeTo(0, 200)
                    );

                    layout.IsVisible = false;
                }
            }

            await dimTask;
        }
        finally
        {
            FabLock.Release();
        }
    }

    /// <summary>
    /// Converts Android-style cookies from WebView to Netscape format
    /// </summary>
    private Stream ConvertAndroidCookiesToNetscape(string rawCookies, string domain = ".youtube.com", string path = "/")
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("# Netscape HTTP Cookie File");
        sb.AppendLine("# This file was generated by CookieConverter");
        sb.AppendLine();

        long expires = DateTimeOffset.UtcNow.AddYears(5).ToUnixTimeSeconds();

        string[] cookies = rawCookies.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (string cookie in cookies)
        {
            string trimmed = cookie.Trim();
            string[] parts = trimmed.Split('=', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            string name = parts[0];
            string value = parts[1];

            sb.AppendLine($"{domain}\tTRUE\t{path}\tFALSE\t{expires}\t{name}\t{value}");
        }

        return new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    /// <summary>
    /// Handles navigation to youtube.com so we can grab authenticated cookies
    /// </summary>
    private async void HandleWebViewNavigation(object? sender, WebNavigatedEventArgs e)
    {
        if (e.Url.Contains("youtube.com")) // We've successfully logged in and been redirected
        {
            // Don't handle any more
            if (sender is WebView webView)
            {
                webView.Navigated -= HandleWebViewNavigation;
            }

            string? rawCookies = CookieManager.Instance?.GetCookie("https://youtube.com");

            if (string.IsNullOrEmpty(rawCookies))
            {
                return;
            }

            // Convert the cookies
            Stream stream = ConvertAndroidCookiesToNetscape(rawCookies);

            // Write to a temp file
            string fileName = Path.Combine(FileSystem.CacheDirectory, "cookies.txt");
            await using (FileStream fileStream = File.Create(fileName))
            {
                await stream.CopyToAsync(fileStream);
            }

            // Upload it
            string result = await ServerApiClient.Instance.UploadCookiesFile(fileName);

            // Clean up temp file
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }

            // Show the result
            await Toast.Make(result, ToastDuration.Long).Show();

            // Close the browser
            await Shell.Current.Navigation.PopAsync();
        }
    }

    private void UpdateFooter()
    {
        // If the footer appears where there is no FAB, some funky stuff happens
        VideosCollectionView.Footer = AppShell.Instance!.AppShellViewModel.QueueTabSelected ? null : new Grid { HeightRequest = 90 };
    }

    private async Task MoveChannelRelativeAsync(int delta)
    {
        Tab tab = AppShell.Instance!.ChannelTab;

        foreach (ShellContent? content in tab.Items.ToList())
        {
            if (content.Content is ChannelView { BindingContext: ChannelViewModel channelViewModel } && content.Content == this)
            {
                // Determine current and target positions
                int currentIndex = tab.Items.IndexOf(content);
                int targetIndex = currentIndex + delta;

                if (targetIndex < 0 || targetIndex >= tab.Items.Count)
                {
                    // Can't move, do nothing
                    return;
                }

                // Find the target page that we want to swap with (either on the left or right of us)
                ShellContent targetShellContent = tab.Items[targetIndex];
                ChannelView? targetChannelView = targetShellContent?.Content as ChannelView;
                ChannelViewModel? targetChannelViewModel = targetChannelView?.BindingContext as ChannelViewModel;

                if (!channelViewModel.Channel!.Persistent || !targetChannelViewModel!.Channel!.Persistent)
                {
                    // Don't move temporary channels
                    return;
                }

                // Swap the indices and persist
                channelViewModel.Channel!.Index = targetIndex;
                targetChannelViewModel.Channel!.Index = currentIndex;

                // Use an empty GUID so that this message comes back to us and is processed
                await ServerApiClient.Instance.UpdateChannel(channelViewModel.Channel, Guid.Empty.ToString());
                await ServerApiClient.Instance.UpdateChannel(targetChannelViewModel.Channel, Guid.Empty.ToString());
            }
        }
    }
}