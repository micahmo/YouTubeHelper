using System;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Threading;
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

            new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Normal, (_, __) =>
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
        }

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is MainControlViewModel mainControlViewModel)
            {
                mainControlViewModel.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(MainControlViewModel.ActiveVideo))
                    {
                        MediaElement.Play();
                    }
                };
            }
        }

        private void MediaScrub_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _mediaScrubBeingDragged = true;
        }

        private void MediaScrub_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _manualScrubPosition = MediaScrub.Value;
            _mediaScrubHandled = false;
        }

        private bool _mediaScrubBeingDragged;
        private bool _mediaScrubHandled = true;
        private double _manualScrubPosition;

        private void MediaElement_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MediaState state = (MediaState)StateField.GetValue(HelperObject);

            if (state == MediaState.Play)
            {
                MediaElement.Pause();
            }
            else
            {
                MediaElement.Play();
            }
        }

        private object HelperObject => _helperObject ??= typeof(MediaElement).GetField("_helper", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(MediaElement);
        private object _helperObject;
        private FieldInfo StateField => _stateField ??= HelperObject.GetType().GetField("_currentState", BindingFlags.NonPublic | BindingFlags.Instance);
        private FieldInfo _stateField;
    }
}
