using System.Globalization;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

public class OrderByToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        DateTime currentTime = DateTime.Now;
        _ = DateTime.TryParse(value as string, out DateTime postedTime);
        
        double difference = (currentTime - postedTime).TotalSeconds;
       
        if (difference < 600)
            return "YellowGreen";

        if (difference < 1800)
            return "Yellow";

        if (difference < 3600)
            return "Orange";

        if (difference < 8400)
            return "Black";


        return "Black";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
