using System.Globalization;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

public class AppNameToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string val)
        {
            if (!string.IsNullOrEmpty(val))
                return "/Images/AccountInfoIcons/app.png";
        }

        return "/Images/AccountInfoIcons/web.png";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
