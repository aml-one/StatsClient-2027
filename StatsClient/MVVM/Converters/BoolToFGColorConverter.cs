using System.Globalization;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

public class BoolToFGColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool val)
        {
            if (val)
                return "Black";
            else
                return "Red";
        }

        return "Black";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
