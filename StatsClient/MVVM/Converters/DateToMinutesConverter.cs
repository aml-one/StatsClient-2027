using System.Globalization;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

public class DateToMinutesConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string date)
        {
            if (DateTime.TryParse(date, out DateTime parsedDate))
            {
                double secs = (DateTime.Now - parsedDate).TotalSeconds;

                if (secs < 60)
                {
                    double scs = Math.Floor(secs);
                    if (scs == 1)
                        return "1 sec ago";
                    else
                        return $"{scs} secs ago";

                }
                else if (secs >= 60 && secs < 3600)
                {
                    double mns = Math.Floor(secs / 60);
                    if (mns == 1)
                        return $"1 min ago";
                    else
                        return $"{mns} mins ago";
                }
                else if (secs >= 60 && secs < 3600)
                {
                    double hrs = Math.Floor(secs / 3600);
                    if (hrs == 1)
                        return $"1 hr ago";
                    else
                        return $"{hrs} hrs ago";
                }

                if (DateTime.Now.ToString("yyyy-MM-dd") == parsedDate.ToString("yyyy-MM-dd"))
                    return $"Today at {parsedDate: h:mm tt}";

                return $"at {parsedDate:MMMM d. h:mm tt}";
            }
        }

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
