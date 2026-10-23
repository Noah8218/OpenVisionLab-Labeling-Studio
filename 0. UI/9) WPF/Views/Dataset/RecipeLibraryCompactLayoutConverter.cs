using System;
using System.Globalization;
using System.Windows.Data;

namespace MvcVisionSystem
{
    public sealed class RecipeLibraryCompactLayoutConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool compact = value is double width && double.IsFinite(width) && width > 0 && width <= 1500;
            return (parameter as string) switch
            {
                "class-name" => value is double availableWidth && availableWidth > 0 ? Math.Max(0D, availableWidth - 60D) : double.PositiveInfinity,
                "identity" => new System.Windows.GridLength(compact ? 1.5D : 1.6D, System.Windows.GridUnitType.Star),
                "cover" => value is double coverWidth ? coverWidth / 1.5D : 0D,
                "thumbnail" => compact ? 56D : 64D,
                "task" => compact ? 96D : 110D,
                "progress" => compact ? 130D : 150D,
                "model" => compact ? 150D : 170D,
                _ => compact
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
