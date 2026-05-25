using System.Globalization;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

public class ServiceStatusToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status switch
            {
                "Healthy" => "\\Images\\HealthIcons\\healthy.png",
                "Sleeping" => "\\Images\\HealthIcons\\sleeping.png",
                "Late to report" => "\\Images\\HealthIcons\\late.png",
                "Struggling" => "\\Images\\HealthIcons\\struggle.png",
                "Dead / Stopped" => "\\Images\\HealthIcons\\dead.png",
                _ => "\\Images\\HealthIcons\\dead.png"
            };
        }

        return "\\Images\\HealthIcons\\dead.png";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
