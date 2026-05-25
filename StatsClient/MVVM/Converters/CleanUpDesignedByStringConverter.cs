using System.Globalization;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

public class CleanUpDesignedByStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string val)
        {
            string[] parts = val.Split('-');
            if (parts.Length == 2)
            {
                val = parts[1].Trim();
                return val;
            }
        }

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
