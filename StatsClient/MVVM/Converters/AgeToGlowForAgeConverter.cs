using System.Globalization;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

class AgeToGlowForAgeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        DateTime currentTime = DateTime.Now;
        _ = DateTime.TryParse(value as string, out DateTime postedTime);

        double difference = (currentTime - postedTime).TotalSeconds;
        
        if (difference < 3600)
            return "LightCoral";


        return "White";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}