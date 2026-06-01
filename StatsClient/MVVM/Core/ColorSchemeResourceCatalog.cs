using System.Windows;
using System.Windows.Media;

namespace StatsClient.MVVM.Core;

/// <summary>
/// Documents and validates color-scheme resource types.
/// WPF crashes at runtime when a resource type does not match the target property
/// (e.g. SolidColorBrush on GradientStop.Color, or Color on Background), or when
/// DynamicResource is used inside Trigger/DataTrigger Setter.Value (use StaticResource).
/// </summary>
internal static class ColorSchemeResourceCatalog
{
    /// <summary>
    /// Keys that must resolve to <see cref="Color"/>.
    /// Use on GradientStop.Color, DropShadowEffect.Color, SolidColorBrush.Color, animations, etc.
    /// </summary>
    public static readonly string[] ColorOnlyKeys =
    [
        "WindowBackgroundColor",
        "LightGrayBorderAndShadowColor_Cl",
        "BlackColor_Cl",
        "PrimaryDarkColor_Cl",
        "TodayButtonGradient_Top_Cl",
        "TodayButtonGradient_Middle_Cl",
        "TodayButtonGradient_Bottom_Cl",
        "ControlLightColor",
        "ControlMediumColor",
        "ControlDarkColor",
        "BorderLightColor",
        "BorderMediumColor",
        "BorderDarkColor",
        "DisabledBorderLightColor",
        "DisabledBorderDarkColor",
        "ClassicWindowBackgroundColor",
        "MessageBoxHeaderGradientStart",
        "MessageBoxHeaderGradientEnd",
        "PopupShadowColor",
        "SplashAccentBlue",
        "SplashAccentBlueLight",
        "ImmersiveCardShadow_Cl",
        "ImmersiveFocusRing_Cl",
        "ViewerWatermarkGradientStart",
        "ViewerWatermarkGradientMid1",
        "ViewerWatermarkGradientMid2",
        "ViewerWatermarkGradientEnd",
        "ViewerSectionPlaneColor",
        "ViewerMeasurementColor",
        "ViewerHighlightColor",
        "ViewerShadowColor",
    ];

    /// <summary>
    /// Keys that must resolve to <see cref="SolidColorBrush"/>.
    /// Use on Background, Foreground, BorderBrush, Fill, Stroke, and other Brush properties.
    /// </summary>
    public static readonly string[] BrushOnlyKeys =
    [
        "WindowBackgroundBrush",
        "ClassicWindowBackgroundBrush",
        "ImmersiveCardShadow",
        "ImmersiveFocusRing",
    ];

    public static bool TryGetColor(string key, out Color color)
    {
        color = default;
        if (Application.Current?.TryFindResource(key) is Color resolved)
        {
            color = resolved;
            return true;
        }

        return false;
    }

    public static bool TryGetBrush(string key, out SolidColorBrush brush)
    {
        brush = null!;
        if (Application.Current?.TryFindResource(key) is SolidColorBrush resolved)
        {
            brush = resolved;
            return true;
        }

        return false;
    }

    public static SolidColorBrush GetBrush(string key)
    {
        if (TryGetBrush(key, out var brush))
        {
            return brush;
        }

        throw new InvalidOperationException($"Color scheme brush '{key}' was not found.");
    }

    public static SolidColorBrush GetBrushOrDefault(string key, SolidColorBrush fallback)
    {
        return TryGetBrush(key, out var brush) ? brush : fallback;
    }

    public static Color GetColor(string key)
    {
        if (TryGetColor(key, out var color))
        {
            return color;
        }

        if (TryGetBrush(key, out var brush))
        {
            return brush.Color;
        }

        throw new InvalidOperationException($"Color scheme color '{key}' was not found.");
    }

    public static string GetHex(string key) => ToRgbHex(GetColor(key));

    public static string GetNamedColorString(string key)
    {
        if (Application.Current?.TryFindResource(key) is string named)
        {
            return named;
        }

        return key switch
        {
            "NamedColorString_Black" => "Black",
            "NamedColorString_White" => "White",
            "NamedColorString_Red" => "Red",
            "NamedColorString_Yellow" => "Yellow",
            "NamedColorString_Green" => "Green",
            "NamedColorString_Gray" => "Gray",
            "NamedColorString_Blue" => "Blue",
            "NamedColorString_Transparent" => "Transparent",
            "NamedColorString_LightGreen" => "LightGreen",
            "NamedColorString_LightBlue" => "LightBlue",
            "BlinkColor_Yellow" => "yellow",
            "BlinkColor_Green" => "green",
            "BlinkColor_Red" => "red",
            _ => throw new InvalidOperationException($"Color scheme named string '{key}' was not found."),
        };
    }

    public static string GetNamedColorStringOrHex(string key) =>
        Application.Current?.TryFindResource(key) is string named
            ? named
            : GetHex(key);

    private static string ToRgbHex(Color color) =>
        $"#{color.R:X2}{color.G:X2}{color.B:X2}";

#if DEBUG
    public static void ValidateActiveScheme(Application application)
    {
        var errors = new List<string>();

        foreach (var key in ColorOnlyKeys)
        {
            var value = application.TryFindResource(key);
            if (value is null)
            {
                errors.Add($"Missing color resource '{key}'.");
            }
            else if (value is not Color)
            {
                errors.Add(
                    $"Resource '{key}' must be Color but is {value.GetType().Name}. " +
                    "Use *_Cl keys or WindowBackgroundColor for Color properties.");
            }
        }

        foreach (var key in BrushOnlyKeys)
        {
            var value = application.TryFindResource(key);
            if (value is null)
            {
                errors.Add($"Missing brush resource '{key}'.");
            }
            else if (value is not SolidColorBrush)
            {
                errors.Add(
                    $"Resource '{key}' must be SolidColorBrush but is {value.GetType().Name}.");
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Color scheme resource type validation failed:\n" + string.Join("\n", errors));
        }
    }
#endif
}
