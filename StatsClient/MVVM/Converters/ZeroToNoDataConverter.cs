using System.Globalization;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

public class ZeroToNoDataConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int val)
        {
            if (val == 0)
                return "No data";
            return val.ToString();
        }
                
        return "No data";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
