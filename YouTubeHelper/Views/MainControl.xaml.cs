using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ModernWpf.Controls;
using YouTubeHelper.ViewModels;

namespace YouTubeHelper.Views
{
    /// <summary>
    /// Interaction logic for MainControl.xaml
    /// </summary>
    public partial class MainControl
    {
        public MainControl()
        {
            InitializeComponent();

            new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Normal, (_, _) =>
            {
                if (DataContext is MainControlViewModel mainControlViewModel && MediaElement.NaturalDuration.HasTimeSpan)
                {
                    if (!_mediaScrubHandled)
                    {
                        MediaElement.Position = TimeSpan.FromMilliseconds(_manualScrubPosition * MediaElement.NaturalDuration.TimeSpan.TotalMilliseconds);
                        _mediaScrubHandled = true;
                        _mediaScrubBeingDragged = false;
                    }
                    else if (!_mediaScrubBeingDragged)
                    {
                        MediaScrub.Value = MediaElement.Position.TotalMilliseconds / MediaElement.NaturalDuration.TimeSpan.TotalMilliseconds;
                        mainControlViewModel.ActiveVideoElapsedTimeSpan = MediaElement.Position;
                        mainControlViewModel.ActiveVideoDuration = MediaElement.NaturalDuration.TimeSpan;
                    }
                }
            }, Dispatcher.CurrentDispatcher).Start();

            // Manually hook up the slider mouse events, and say we want them even when they're handled
            MediaScrub.AddHandler(PreviewMouseDownEvent, new MouseButtonEventHandler(MediaScrub_PreviewMouseDown), true);
            MediaScrub.AddHandler(PreviewMouseUpEvent, new MouseButtonEventHandler(MediaScrub_PreviewMouseUp), true);
        }

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is MainControlViewModel mainControlViewModel)
            {
                mainControlViewModel.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(MainControlViewModel.ActiveVideo) ||
                        args.PropertyName == nameof(MainControlViewModel.SignalPlayVideo))
                    {
                        MediaElement.Play();
                    }
                    else if (args.PropertyName == nameof(MainControlViewModel.SignalPauseVideo))
                    {
                        MediaElement.Pause();
                    }
                };
            }
        }

        private void MediaScrub_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _mediaScrubBeingDragged = true;
        }

        private void MediaScrub_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            _manualScrubPosition = MediaScrub.Value;
            _mediaScrubHandled = false;
        }

        private bool _mediaScrubBeingDragged;
        private bool _mediaScrubHandled = true;
        private double _manualScrubPosition;

        private void MediaElement_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(1/4f));
                if (_doubleClickCancellationToken?.IsCancellationRequested == false)
                {
                    await _doubleClickCancellationToken.CancelAsync();
                    Dispatcher.Invoke(TogglePlayState);
                }
            }, (_doubleClickCancellationToken = new CancellationTokenSource()).Token);

            if (e.ClickCount == 2 && !_doubleClickCancellationToken.IsCancellationRequested)
            {
                _doubleClickCancellationToken?.Cancel();

                if (DataContext is MainControlViewModel mainControlViewModel)
                {
                    mainControlViewModel.IsMainControlExpanded = !mainControlViewModel.IsMainControlExpanded;
                }
            }

            // MediaElement steals keyboard focus for future shortcuts
            Dispatcher.BeginInvoke(() => MoveFocus(new TraversalRequest(FocusNavigationDirection.First)));
        }

        private CancellationTokenSource? _doubleClickCancellationToken;

        private void TogglePlayState()
        {
            MediaState? state = (MediaState?)StateField.GetValue(HelperObject);

            if (state == MediaState.Play)
            {
                MediaElement.Pause();
            }
            else
            {
                MediaElement.Play();
            }
        }

        private object HelperObject => _helperObject ??= typeof(MediaElement).GetField("_helper", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(MediaElement)!;
        private object? _helperObject;
        private FieldInfo StateField => _stateField ??= HelperObject.GetType().GetField("_currentState", BindingFlags.NonPublic | BindingFlags.Instance)!;
        private FieldInfo? _stateField;

        private void Flyout_Opened(object sender, object e)
        {
            if (sender is Flyout flyout)
            {
                OpenFlyouts.Add(flyout);
            }
        }
        public static List<Flyout> OpenFlyouts = new();

        private void Search_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                (DataContext as MainControlViewModel)?.SelectedChannel?.FindVideosCommand.Execute(null);
            }
        }

        private void TextBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                Clipboard.SetText(textBlock.Text);
            }
        }
    }
}
