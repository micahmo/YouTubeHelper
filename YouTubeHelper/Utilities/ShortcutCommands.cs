using System.Windows.Input;

namespace YouTubeHelper.Utilities
{
    public static class ShortcutCommands
    {
        #region Commands

        static ShortcutCommands()
        {
            AddWatchedIds.InputGestures.Add(new KeyGesture(Key.W, ModifierKeys.Control));
            AddWontWatchIds.InputGestures.Add(new KeyGesture(Key.T, ModifierKeys.Control));
            AddMightWatchIds.InputGestures.Add(new KeyGesture(Key.M, ModifierKeys.Control));
            ChangeView.InputGestures.Add(new KeyGesture(Key.D1, ModifierKeys.Control));
            ChangeView.InputGestures.Add(new KeyGesture(Key.D2, ModifierKeys.Control));
            ChangeView.InputGestures.Add(new KeyGesture(Key.D3, ModifierKeys.Control));
            ChangeView.InputGestures.Add(new KeyGesture(Key.Escape));
        }

        public static RoutedCommand AddWatchedIds { get; } = new();

        public static RoutedCommand AddWontWatchIds { get; } = new();

        public static RoutedCommand AddMightWatchIds { get; } = new();

        public static RoutedCommand ChangeView { get; } = new();

        #endregion
    }
}