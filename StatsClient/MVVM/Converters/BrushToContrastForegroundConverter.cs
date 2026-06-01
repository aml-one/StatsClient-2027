using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StatsClient.MVVM.Converters;

/// <summary>
/// Picks black or white foreground for icons/text on top of a colored background brush.
/// </summary>
public sealed class BrushToContrastForegroundConverter : IValueConverter
{
    private static readonly SolidColorBrush LightIconBrush = new(Color.FromRgb(0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush DarkIconBrush = new(Color.FromRgb(0x11, 0x18, 0x27));

    static BrushToContrastForegroundConverter()
    {
        LightIconBrush.Freeze();
        DarkIconBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var color = ResolveColor(value);
        var luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
        return luminance > 0.58 ? DarkIconBrush : LightIconBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    private static Color ResolveColor(object? value)
    {
        switch (value)
        {
            case SolidColorBrush solid:
                return solid.Color;
            case Color color:
                return color;
            case string text when !string.IsNullOrWhiteSpace(text):
                try
                {
                    return (Color)ColorConverter.ConvertFromString(text);
                }
                catch
                {
                    return Colors.SlateGray;
                }
            case Brush brush when brush is SolidColorBrush solidBrush:
                return solidBrush.Color;
            default:
                return Colors.SlateGray;
        }
    }
}
