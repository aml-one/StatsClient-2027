using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using StatsClient.MVVM.Core;

namespace StatsClient.MVVM.Converters;

public class BoolToFGColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool val)
        {
            return val
                ? ColorSchemeResourceCatalog.GetBrush("ValidationValidForeground")
                : ColorSchemeResourceCatalog.GetBrush("ValidationInvalidForeground");
        }

        return ColorSchemeResourceCatalog.GetBrush("ValidationValidForeground");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
