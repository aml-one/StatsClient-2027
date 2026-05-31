using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using StatsClient.MVVM.Core;

namespace StatsClient.MVVM.Converters;

public class ColorToPastelAccentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string colorText || string.IsNullOrWhiteSpace(colorText))
            return ColorSchemeResourceCatalog.GetBrush("ImmersiveAccentBlueLight");

        try
        {
            var source = (Color)ColorConverter.ConvertFromString(colorText)!;
            return new SolidColorBrush(ToPastelAccent(source));
        }
        catch
        {
            return ColorSchemeResourceCatalog.GetBrush("ImmersiveAccentBlueLight");
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value;

    private static Color ToPastelAccent(Color source)
    {
        RgbToHsl(source, out var h, out var s, out _);

        var pastelSaturation = s < 0.08
            ? 0.48
            : Math.Clamp(s * 0.75 + 0.38, 0.52, 0.88);

        const double pastelLightness = 0.78;

        HslToRgb(h, pastelSaturation, pastelLightness, out var r, out var g, out var b);
        return Color.FromRgb(
            (byte)Math.Round(r * 255),
            (byte)Math.Round(g * 255),
            (byte)Math.Round(b * 255));
    }

    private static void RgbToHsl(Color color, out double h, out double s, out double l)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        l = (max + min) / 2.0;

        if (Math.Abs(max - min) < 0.0001)
        {
            h = 0;
            s = 0;
            return;
        }

        var d = max - min;
        s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

        if (max == r)
            h = (g - b) / d + (g < b ? 6 : 0);
        else if (max == g)
            h = (b - r) / d + 2;
        else
            h = (r - g) / d + 4;

        h /= 6.0;
    }

    private static void HslToRgb(double h, double s, double l, out double r, out double g, out double b)
    {
        if (s <= 0.0001)
        {
            r = g = b = l;
            return;
        }

        var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        var p = 2 * l - q;

        r = HueToRgb(p, q, h + 1.0 / 3.0);
        g = HueToRgb(p, q, h);
        b = HueToRgb(p, q, h - 1.0 / 3.0);
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
        return p;
    }
}
