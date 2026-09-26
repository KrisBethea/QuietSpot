using System.Globalization;

namespace QuietSpot.App.Converters;

/// <summary>Maps a status label to pill background/text colors. Parameter: "bg" or "fg".</summary>
public class StatusColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var label = (value as string ?? "").ToLowerInvariant();
        var wantBg = (parameter as string) == "bg";

        if (label.Contains("packed"))
            return App.Current!.Resources[wantBg ? "PackedBg" : "PackedColor"];
        if (label.Contains("moderate"))
            return App.Current!.Resources[wantBg ? "ModerateBg" : "ModerateColor"];
        return App.Current!.Resources[wantBg ? "QuietBg" : "QuietColor"];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
