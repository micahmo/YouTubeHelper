using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows;

namespace YouTubeHelper.Utilities
{
    /// <summary>
    /// Class to help with binding a property to Inlines
    /// </summary>
    public static class InlineHelper
    {
        public static readonly DependencyProperty InlinesProperty =
            DependencyProperty.RegisterAttached(
                "Inlines",
                typeof(IEnumerable<Inline>),
                typeof(InlineHelper),
                new PropertyMetadata(null, OnInlinesChanged));

        public static void SetInlines(DependencyObject element, IEnumerable<Inline> value)
        {
            element.SetValue(InlinesProperty, value);
        }

        public static IEnumerable<Inline> GetInlines(DependencyObject element)
        {
            return (IEnumerable<Inline>)element.GetValue(InlinesProperty);
        }

        private static void OnInlinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBlock textBlock)
            {
                textBlock.Inlines.Clear();
                if (e.NewValue is IEnumerable<Inline> inlines)
                {
                    foreach (Inline inline in inlines)
                    {
                        textBlock.Inlines.Add(inline);
                    }
                }
            }
        }
    }
}
