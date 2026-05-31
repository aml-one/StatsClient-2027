using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using StatsClient.MVVM.Core;

namespace StatsClient.MVVM.Converters;

public class DarkColorBoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool val && val)
            return ColorSchemeResourceCatalog.GetBrush("BlackColor");

        return ColorSchemeResourceCatalog.GetBrush("BlackColor");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
