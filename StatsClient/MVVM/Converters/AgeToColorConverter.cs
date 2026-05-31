using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using StatsClient.MVVM.Core;

namespace StatsClient.MVVM.Converters;

public class AgeToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int i)
        {
            if (i == 0)
                return ColorSchemeResourceCatalog.GetBrush("AgeColorFresh");

            if (i == 1)
                return ColorSchemeResourceCatalog.GetBrush("AgeColorRecent");

            if (i == 2)
                return ColorSchemeResourceCatalog.GetBrush("AgeColorGreen");

            if (i > 2 && i < 7)
                return ColorSchemeResourceCatalog.GetBrush("AgeColorSteel");

            if (i > 7 && i < 14)
                return ColorSchemeResourceCatalog.GetBrush("AgeColorBlue");

            if (i > 14 && i < 35)
                return ColorSchemeResourceCatalog.GetBrush("AgeColorMaroon");

            return ColorSchemeResourceCatalog.GetBrush("AgeColorDefault");
        }

        return ColorSchemeResourceCatalog.GetBrush("AgeColorDefault");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
