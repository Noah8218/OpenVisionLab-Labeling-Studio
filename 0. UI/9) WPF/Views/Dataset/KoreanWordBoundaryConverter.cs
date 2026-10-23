using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace MvcVisionSystem
{
    public sealed class KoreanWordBoundaryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is string text ? Regex.Replace(text, @"(?<=[가-힣])(?=[가-힣])", "\u2060") : value;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
