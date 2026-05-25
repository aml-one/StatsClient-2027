using System.Globalization;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

public class EmailToWebsiteConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is string val)
            {
                if (val.Contains('@'))
                    return string.Concat(val[(val.IndexOf('@') + 1)..][0].ToString().ToUpper(), val[(val.IndexOf('@') + 1)..].AsSpan(1));
            }
        }
        catch (Exception)
        {
        }

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
