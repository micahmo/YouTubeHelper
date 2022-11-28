using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace YouTubeHelper.Shared.Utilities
{
    public class MyObservableCollection<T> : ObservableCollection<T>
    {
        private bool _addingRange;
        
        // Not thread safe!
        public void AddRange(List<T> range)
        {
            _addingRange = true;

            try
            {
                range.ForEach(Add);
            }
            finally
            {
                _addingRange = false;
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (_addingRange)
            {
                return;
            }

            base.OnCollectionChanged(e);
        }
    }
}
