using System.Globalization;
using System.Windows;
using System.Windows.Data;
using static StatsClient.MVVM.Core.LocalSettingsDB;

namespace StatsClient.MVVM.Converters;

public class AgeSecondsToCollapsedVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double timeout = 20;

        if (double.TryParse(ReadLocalSetting("TimeoutForImportAncmnt"), out double tmout))
            timeout = tmout;

        if (value is double val)
        {
            if (val < timeout)
                return Visibility.Visible;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
