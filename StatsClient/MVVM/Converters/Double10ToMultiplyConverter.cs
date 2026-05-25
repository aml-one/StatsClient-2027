using System.Globalization;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

public class Double10ToMultiplyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double val)
        {
            return val * 24;
        }

        return 24;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
