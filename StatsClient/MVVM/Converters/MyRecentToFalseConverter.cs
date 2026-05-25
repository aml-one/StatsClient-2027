using System.Globalization;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

public class MyRecentToFalseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string val)
            return val != "MyRecent";

        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
