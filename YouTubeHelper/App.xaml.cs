﻿using HarmonyLib;
using Notification.Wpf;
using System.Windows;
using System.Windows.Threading;
using YouTubeHelper.Utilities;

namespace YouTubeHelper
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Harmony harmony = new Harmony("com.micahmo.youtubehelper");
            harmony.PatchAll();

            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Dispatcher.Invoke(async () => await MessageBoxHelper.ShowCopyableText(YouTubeHelper.Properties.Resources.UnexpectedError, YouTubeHelper.Properties.Resources.Error, e.Exception.ToString()));

            e.Handled = true;
        }

        public static NotificationManager NotificationManager = new();
    }
}
