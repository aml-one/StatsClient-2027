using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StatsClient.MVVM.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isPaid)
        {
            return isPaid 
                ? new SolidColorBrush(Color.FromRgb(39, 174, 96))  // Green for paid (#27AE60)
                : new SolidColorBrush(Color.FromRgb(231, 76, 60));  // Red for unpaid (#E74C3C)
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToPaidStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isPaid)
        {
            return isPaid ? "✓ PAID" : "✗ UNPAID";
        }
        return "UNKNOWN";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
