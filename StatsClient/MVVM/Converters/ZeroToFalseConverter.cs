using System.Globalization;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

public class ZeroToFalseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int val)
        {
            if (val < 1)
                return false;
        }

        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
