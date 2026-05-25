using System.Globalization;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

public class DateTimeStringToUSADateTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (DateTime.TryParse(value as string, out var date)) 
            return date.ToString("MMM d, yyyy h:mmtt");

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
