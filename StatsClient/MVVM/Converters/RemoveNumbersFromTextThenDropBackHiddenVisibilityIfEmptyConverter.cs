using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

public class RemoveNumbersFromTextThenDropBackHiddenVisibilityIfEmptyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            StringBuilder result = new();
            foreach (char c in str)
            {
                if (!char.IsDigit(c))
                {
                    result.Append(c);
                }
            }


            string trimmedResult = result.ToString().Trim();

            trimmedResult = trimmedResult.Replace(",", "");

            if (trimmedResult == "-")
                return Visibility.Collapsed;


            if (string.IsNullOrEmpty(trimmedResult))
                return Visibility.Collapsed;
        }

        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
