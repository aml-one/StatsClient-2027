using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

public class RemoveNumbersFromTextConverter : IValueConverter
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
                return "";
            
            return trimmedResult.ToUpper()
                                .Replace("_", "")
                                .Replace(",", "")
                                .Replace("%25", "")
                                .Replace(" STX", "")
                                .Replace(" STT", "")
                                .Replace("STX ", "")
                                .Replace("STT ", "")
                                .Replace("(STX)", "")
                                .Replace("(STT)", "")
                                .Replace("(", "")
                                .Replace(")", "")
                                .Replace("%2B", "")
                                .Trim();
        }

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
