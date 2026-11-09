using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using TextBox = System.Windows.Controls.TextBox;

namespace MvcVisionSystem
{
    public sealed class PathTailPreviewConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 4 || values[0] is not string path || path.Length == 0 ||
                values[1] is not double width || width <= 0 || values[2] is not double size || values[3] is not TextBox editor)
                return string.Empty;

            var typeface = new Typeface(editor.FontFamily, editor.FontStyle, editor.FontWeight, editor.FontStretch);
            double dpi = VisualTreeHelper.GetDpi(editor).PixelsPerDip;
            double Measure(string text) => new FormattedText(text, culture, editor.FlowDirection, typeface, size, Brushes.Black, dpi).WidthIncludingTrailingWhitespace;
            if (Measure(path) <= width) return string.Empty;

            // The preview is display-only. Keep the leaf and its extension even when the folder prefix is long.
            int leafEnd = path.TrimEnd('\\', '/').Length;
            int separator = leafEnd > 0 ? path.LastIndexOfAny(new[] { '\\', '/' }, leafEnd - 1) : -1;
            string tail = separator >= 0 ? path.Substring(separator) : path;
            if (Measure("…" + tail) <= width) return "…" + tail;

            int[] elements = StringInfo.ParseCombiningCharacters(tail);
            int first = 0, end = elements.Length;
            while (first < end)
            {
                int mid = first + (end - first) / 2;
                if (Measure("…" + tail.Substring(elements[mid])) > width) first = mid + 1;
                else end = mid;
            }
            return first == elements.Length ? "…" : "…" + tail.Substring(elements[first]);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
