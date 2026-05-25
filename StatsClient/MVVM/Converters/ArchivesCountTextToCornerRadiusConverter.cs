using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

public class ArchivesCountTextToCornerRadiusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string valStr)
        {
            if (valStr == "0" || string.IsNullOrEmpty(valStr))
                return new CornerRadius(23, 23, 0, 0);

        }
        return new CornerRadius(23, 3, 0, 0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
