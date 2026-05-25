using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

public class CommentIconToVisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string val)
        {
            if (val == "7")
                return Visibility.Visible;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return "0";
    }
}
