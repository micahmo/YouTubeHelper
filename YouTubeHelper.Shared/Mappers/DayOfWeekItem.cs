using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.Windows.Input;
using Microsoft.Toolkit.Mvvm.Input;

namespace YouTubeHelper.Shared.Mappers
{
    /// <summary>
    /// Mapper class to help represent the day-of-week filter that's stored in the channel
    /// </summary>
    public class DayOfWeekItem : ObservableObject
    {
        private bool _isSelected;

        public DayOfWeekItem(DayOfWeek day, bool isSelected)
        {
            Day = day;
            _isSelected = isSelected;
        }

        public DayOfWeek Day { get; }

        public string DisplayName => Day.ToString();

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler? SelectionChanged;

        public ICommand ToggleCommand => new RelayCommand(() =>
        {
            IsSelected = !IsSelected;
        });
    }
}
