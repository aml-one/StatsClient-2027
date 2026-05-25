using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

public class IncostistencyToVisibilityCollapsedConverterConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if ((!string.IsNullOrEmpty(values[0] as string) && !string.IsNullOrEmpty(values[1] as string)) || values[2] as bool? == true)
            return Visibility.Collapsed;
        return Visibility.Visible;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        return [];
    }
}
