using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

public class LessThan50ToHiddenConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int val && value is not null)
            if (val < 50)
                return Visibility.Hidden;
                
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
