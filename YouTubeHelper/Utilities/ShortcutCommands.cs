﻿using System.Windows.Input;

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
            ChangeView.InputGestures.Add(new KeyGesture(Key.F5));
            ChangeView.InputGestures.Add(new KeyGesture(Key.F5, ModifierKeys.Shift));
            ChangeView.InputGestures.Add(new KeyGesture(Key.PageUp, ModifierKeys.Control));
            ChangeView.InputGestures.Add(new KeyGesture(Key.PageDown, ModifierKeys.Control));
            ChangeView.InputGestures.Add(new KeyGesture(Key.PageUp, ModifierKeys.Control | ModifierKeys.Shift));
            ChangeView.InputGestures.Add(new KeyGesture(Key.PageDown, ModifierKeys.Control | ModifierKeys.Shift));
            HandlePaste.InputGestures.Add(new KeyGesture(Key.V, ModifierKeys.Control));
            AboutBox.InputGestures.Add(new KeyGesture(Key.F1));
        }

        public static RoutedCommand AddWatchedIds { get; } = new();

        public static RoutedCommand AddWontWatchIds { get; } = new();

        public static RoutedCommand AddMightWatchIds { get; } = new();

        public static RoutedCommand ChangeView { get; } = new();

        public static RoutedCommand HandlePaste { get; } = new();

        public static RoutedCommand AboutBox { get; } = new();

        #endregion
    }
}