using StatsClient.MVVM.Model;
using System;
using System.Globalization;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

/// <summary>
/// Checking for first letters on string value to see if there is any number plus dash combination, if there is, then remove and return it without it
/// Used for make custom order in ListView object as following:
/// 0-string
/// 1-secondString
/// 2-thirdString
/// 
/// During sorting it gets sorted by the numbers on the front, and this converter removes the sorting numbers from the beginning of the string
/// and returns a clean string instead for displaying the value..
/// </summary>
public class RemoveIDTagFromFrontConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
            return "";

        string valueInStr;

        try
        {

            valueInStr = (string)value;
            bool isDigit;

            try
            {
                isDigit = char.IsDigit(valueInStr[0]);
            }
            catch
            {
                isDigit = false;
            }

            if (isDigit)
            {
                string[] stringParts = valueInStr.Split('-');
                return valueInStr.Replace(stringParts[0] + "-", "");
            }
        }
        catch         
        {
            return "Unknown";
        }

        return valueInStr;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return "";
    }
}
