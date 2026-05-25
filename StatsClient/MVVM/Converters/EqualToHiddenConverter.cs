using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows;
using System.Diagnostics;

namespace StatsClient.MVVM.Converters;

public class EqualToHiddenConverter : IMultiValueConverter
{       

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is not null && values[0] is not null && values[1] is not null)
        {
            if ((string)values[0] == (string)values[1])
            {
                return Visibility.Collapsed;
            }
        }

        return Visibility.Visible;
    }



    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
