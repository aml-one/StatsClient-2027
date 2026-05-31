using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using StatsClient.MVVM.Core;

namespace StatsClient.MVVM.Converters;

public class OrderByToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        DateTime currentTime = DateTime.Now;
        _ = DateTime.TryParse(value as string, out DateTime postedTime);

        double difference = (currentTime - postedTime).TotalSeconds;

        if (difference < 600)
            return ColorSchemeResourceCatalog.GetBrush("OrderByColorFresh");

        if (difference < 1800)
            return ColorSchemeResourceCatalog.GetBrush("OrderByColorMedium");

        if (difference < 3600)
            return ColorSchemeResourceCatalog.GetBrush("OrderByColorStale");

        if (difference < 8400)
            return ColorSchemeResourceCatalog.GetBrush("OrderByColorOld");

        return ColorSchemeResourceCatalog.GetBrush("OrderByColorOld");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
