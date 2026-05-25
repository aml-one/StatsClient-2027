using System.Globalization;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

public class ProcessStatusIDToReadableStateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string val)
        {
            switch (val)
            {
                case "psCreated": return "Created";
                case "psScanned": return "Scanned";
                case "psScanning": return "Scanning";
                case "psModelled": return "Designed";
                case "psModelling": return "Designing";
                case "psClosed": return "Closed";
                case "psSent": return "Sent";
            }
        }

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
