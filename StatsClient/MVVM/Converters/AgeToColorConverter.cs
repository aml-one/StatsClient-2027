using System.Drawing;
using System.Globalization;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

public class AgeToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int i)
        {
            if (i == 0)
                return "SeaGreen";

            if (i == 1)
                return "LimeGreen";

            if (i == 2)
                return "Green";

            if (i > 2 && i < 7)
                return "SteelBlue";

            if (i > 7 && i < 14)
                return "Blue";

            if (i > 14 && i < 35)
                return "Maroon";

            return "Black";
        }   

        return "Black";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
