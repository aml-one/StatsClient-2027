using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StatsClient.MVVM.Converters;

public class EventColorToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (SolidColorBrush)new BrushConverter().ConvertFrom(value as string)!;

        //return Brushes.White;
    }

    

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
