using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace StatsClient.MVVM.Converters;

public class EmptyToVisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string strValue && value is not null && !string.IsNullOrWhiteSpace(strValue))
        {
            return Visibility.Hidden;
        }

        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Visibility.Visible;
    }
}
