using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using StatsClient.MVVM.Core;

namespace StatsClient.MVVM.Converters;

public class IncostistencyToColorConverterConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (!string.IsNullOrEmpty(values[0] as string) && !string.IsNullOrEmpty(values[1] as string))
            return ColorSchemeResourceCatalog.GetBrush("InconsistencyBothFilledColor");

        if (values[2] as bool? == true)
            return ColorSchemeResourceCatalog.GetBrush("InconsistencyFlaggedColor");

        if (values[0] as string == "")
            return ColorSchemeResourceCatalog.GetBrush("InconsistencyEmptyFirstColor");

        if (values[1] as string == "")
            return ColorSchemeResourceCatalog.GetBrush("InconsistencyEmptySecondColor");

        return ColorSchemeResourceCatalog.GetBrush("InconsistencyEmptyBackground");
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        return [];
    }
}
