using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StatsClient.MVVM.Converters;

public class IncostistencyToColorConverterConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (!string.IsNullOrEmpty(values[0] as string) && !string.IsNullOrEmpty(values[1] as string))
            return (SolidColorBrush)new BrushConverter().ConvertFrom("#00571C")!;
        
        if (values[2] as bool? == true)
            return (SolidColorBrush)new BrushConverter().ConvertFrom("#06A2B0")!;

        if (values[0] as string == "")
            return (SolidColorBrush)new BrushConverter().ConvertFrom("#E1005A")!;
        
        if (values[1] as string == "")
            return (SolidColorBrush)new BrushConverter().ConvertFrom("#B07F0B")!;
        

        return Brushes.White;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        return [];
    }
}
