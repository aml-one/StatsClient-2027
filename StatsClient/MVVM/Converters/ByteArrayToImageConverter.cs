using System.Globalization;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

public sealed class ByteArrayToImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is byte[] bytes && bytes.Length > 0)
        {
            return Core.KnowledgeBase.KnowledgeBaseImageHelper.ToBitmapImage(bytes);
        }

        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
