using Jot;
using Jot.Storage;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.IO;
using System.Windows;
using ServerStatusBot.Definitions;
using ServerStatusBot.Definitions.Database.Models;

namespace YouTubeHelper.Models
{
    /// <summary>
    /// Defines application-wide settings which will be persisted across sessions
    /// </summary>
    public class ApplicationSettings : ObservableObject
    {
        /// <summary>
        /// Singleton instance
        /// </summary>
        public static ApplicationSettings Instance { get; } = new ApplicationSettings();

        private ApplicationSettings()
        {
            Tracker.Configure<Window>()
                .Id(w => w.Name, new Size(SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight))
                .Properties(w => new { w.Top, w.Width, w.Height, w.Left, w.WindowState })
                .PersistOn(nameof(Window.Closing))
                .StopTrackingOn(nameof(Window.Closing))
                .WhenPersistingProperty((w, p) => p.Cancel = p.Property == nameof(w.WindowState) && w.WindowState == WindowState.Minimized);

            PropertyChanged += (_, _) =>
            {
                Save();
            };
        }

        public void Load()
        {
            Tracker.Configure<ApplicationSettings>()
                .Property(a => a.SelectedTabIndex)
                .Property(a => a.SelectedSortMode)
                .Property(a => a.SelectedExclusionsMode)
                .Property(a => a.SelectedExclusionReason)
                .Property(a => a.ServerAddress)
                .Track(this);
        }

        private void Save()
        {
            Tracker.Persist(this);
        }

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }
        private int _selectedTabIndex;

        public SortMode SelectedSortMode
        {
            get => _selectedSortMode;
            set => SetProperty(ref _selectedSortMode, value);
        }
        private SortMode _selectedSortMode;

        public ExclusionsMode SelectedExclusionsMode
        {
            get => _selectedExclusionsMode;
            set => SetProperty(ref _selectedExclusionsMode, value);
        }
        private ExclusionsMode _selectedExclusionsMode;

        public ExclusionReason SelectedExclusionReason
        {
            get => _selectedExclusionReason;
            set => SetProperty(ref _selectedExclusionReason, value);
        }
        private ExclusionReason _selectedExclusionReason;

        public byte[]? ServerAddress
        {
            get => _serverAddress;
            set => SetProperty(ref _serverAddress, value);
        }
        private byte[]? _serverAddress;

        #region Tracker instance

        public Tracker Tracker { get; } = new(new JsonFileStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YTH")));

        #endregion
    }
}