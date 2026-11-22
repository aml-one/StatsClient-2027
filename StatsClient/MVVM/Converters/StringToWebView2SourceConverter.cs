using System.Globalization;
using System.Windows.Data;

namespace StatsClient.MVVM.Converters;

/// <summary>
/// WebView2.Source rejects null; use about:blank until a real URL is available.
/// </summary>
public sealed class StringToWebView2SourceConverter : IValueConverter
{
    private static readonly Uri Blank = new("about:blank");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string url && !string.IsNullOrWhiteSpace(url))
        {
            return Uri.TryCreate(url, UriKind.Absolute, out Uri? absolute)
                ? absolute
                : Uri.TryCreate(url, UriKind.Relative, out Uri? relative)
                    ? relative
                    : Blank;
        }

        return Blank;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Uri uri ? uri.ToString() : string.Empty;
}
