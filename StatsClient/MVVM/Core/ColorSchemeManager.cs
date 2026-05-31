using System.Windows;
using System.Windows.Media;

namespace StatsClient.MVVM.Core;

public static class ColorSchemeManager
{
    public const string LightScheme = "Light";
    public const string DarkScheme = "Dark";
    public const string DefaultScheme = LightScheme;
    public const string LocalSettingKey = "ColorScheme";

    private static ResourceDictionary? _activeSchemeDictionary;
    private static string _currentScheme = DefaultScheme;

    public static string CurrentScheme => _currentScheme;

    public static IReadOnlyList<string> AvailableSchemes { get; } = [LightScheme, DarkScheme];

    public static void Apply(string? schemeName)
    {
        var normalized = NormalizeScheme(schemeName);

#if DEBUG
        XamlResourceValidator.ValidateProjectXaml();
#endif

        if (string.Equals(_currentScheme, normalized, StringComparison.OrdinalIgnoreCase)
            && _activeSchemeDictionary is not null)
        {
            return;
        }

        var application = Application.Current
            ?? throw new InvalidOperationException("Application is not initialized.");

#if DEBUG
        ColorSchemeResourceCatalog.ValidateActiveScheme(application);
#endif

        var merged = application.Resources.MergedDictionaries;
        RemoveActiveSchemeDictionary(merged);

        var schemeUri = new Uri($"/Themes/ColorSchemes/{normalized}.xaml", UriKind.Relative);
        var schemeDictionary = new ResourceDictionary { Source = schemeUri };
        merged.Add(schemeDictionary);

        _activeSchemeDictionary = schemeDictionary;
        _currentScheme = normalized;

        RemoveLegacySchemeOverrides();
    }

    public static void RemoveLegacySchemeOverrides()
    {
        var application = Application.Current;
        if (application is null)
        {
            return;
        }

        RemoveClassicSchemeFromMergedDictionaries(application.Resources.MergedDictionaries);

        foreach (Window window in application.Windows)
        {
            if (window.Resources is ResourceDictionary windowResources)
            {
                RemoveClassicSchemeFromMergedDictionaries(windowResources.MergedDictionaries);
            }
        }
    }

    private static void RemoveClassicSchemeFromMergedDictionaries(IList<ResourceDictionary> merged)
    {
        for (var index = merged.Count - 1; index >= 0; index--)
        {
            var source = merged[index].Source?.OriginalString;
            if (source is not null
                && source.Contains("ClassicColorScheme.xaml", StringComparison.OrdinalIgnoreCase))
            {
                merged.RemoveAt(index);
            }
        }
    }

    public static void InitializeFromApplicationResources()
    {
        var application = Application.Current;
        if (application is null)
        {
            return;
        }

        foreach (var dictionary in application.Resources.MergedDictionaries)
        {
            if (!IsColorSchemeDictionary(dictionary))
            {
                continue;
            }

            _activeSchemeDictionary = dictionary;
            _currentScheme = ReadSchemeNameFromDictionary(dictionary) ?? DefaultScheme;
#if DEBUG
            XamlResourceValidator.ValidateProjectXaml();
#endif
            return;
        }
    }

    public static string GetWindowBackgroundHex()
    {
        if (ColorSchemeResourceCatalog.TryGetColor("WindowBackgroundColor", out var color))
        {
            return ToRgbHex(color);
        }

        if (ColorSchemeResourceCatalog.TryGetBrush("WindowBackgroundBrush", out var brush))
        {
            return ToRgbHex(brush.Color);
        }

        return ColorSchemeResourceCatalog.GetHex("WindowBackgroundColor");
    }

    private static string ToRgbHex(Color color) =>
        $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    public static string NormalizeScheme(string? schemeName) =>
        string.Equals(schemeName, DarkScheme, StringComparison.OrdinalIgnoreCase)
            ? DarkScheme
            : LightScheme;

    private static void RemoveActiveSchemeDictionary(IList<ResourceDictionary> merged)
    {
        for (var index = merged.Count - 1; index >= 0; index--)
        {
            if (!IsColorSchemeDictionary(merged[index]))
            {
                continue;
            }

            merged.RemoveAt(index);
        }

        _activeSchemeDictionary = null;
    }

    private static bool IsColorSchemeDictionary(ResourceDictionary dictionary)
    {
        var source = dictionary.Source?.OriginalString;
        return source is not null
               && source.Contains("ColorSchemes/", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadSchemeNameFromDictionary(ResourceDictionary dictionary)
    {
        if (dictionary["ColorSchemeName"] is string schemeName
            && !string.IsNullOrWhiteSpace(schemeName))
        {
            return NormalizeScheme(schemeName);
        }

        var source = dictionary.Source?.OriginalString;
        if (source is null)
        {
            return null;
        }

        if (source.Contains("Dark.xaml", StringComparison.OrdinalIgnoreCase))
        {
            return DarkScheme;
        }

        if (source.Contains("Light.xaml", StringComparison.OrdinalIgnoreCase))
        {
            return LightScheme;
        }

        return null;
    }
}
