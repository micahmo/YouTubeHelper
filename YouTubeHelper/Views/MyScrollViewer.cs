using System.Windows.Controls;
using System.Windows.Input;

namespace YouTubeHelper.Views
{
    public class MyScrollViewer : ScrollViewer
    {
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key is Key.PageUp or Key.PageDown)
            {
                return;
            }

            base.OnKeyDown(e);
        }
    }
}
