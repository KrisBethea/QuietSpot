using System.Globalization;

namespace QuietSpot.App.Converters;

public class FavoriteGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "\u2665" : "\u2661"; // filled vs outline heart

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
