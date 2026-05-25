using System.Globalization;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

public class RemoveFirstCharFromStringIfItsZConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string val) 
        {
            if (val.StartsWith('z') || val.StartsWith('0') || val.StartsWith('1')
                || val.StartsWith('2') || val.StartsWith('3') || val.StartsWith('4')
                || val.StartsWith('5') || val.StartsWith('6') || val.StartsWith('7')
                || val.StartsWith('8') || val.StartsWith('9'))
                return val[1..];
        }

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
