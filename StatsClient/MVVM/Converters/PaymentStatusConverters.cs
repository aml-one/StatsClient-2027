using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using StatsClient.MVVM.Core;

namespace StatsClient.MVVM.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isPaid)
        {
            return isPaid
                ? ColorSchemeResourceCatalog.GetBrush("PaymentStatusPaidColor")
                : ColorSchemeResourceCatalog.GetBrush("PaymentStatusUnpaidColor");
        }

        return ColorSchemeResourceCatalog.GetBrush("PaymentStatusUnknownColor");
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
