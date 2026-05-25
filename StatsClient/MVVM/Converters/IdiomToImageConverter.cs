using System.Globalization;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

public class IdiomToImageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string val)
        {
            return @$"\Images\Idioms\{val}.png";
        }

        return @$"\Images\Idioms\Unknown.png";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
