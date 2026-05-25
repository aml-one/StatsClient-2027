using System.Drawing;
using System.Globalization;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

public class DarkColorBoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool val)
            if (val)
                return "Black";
        
        return "Black";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
