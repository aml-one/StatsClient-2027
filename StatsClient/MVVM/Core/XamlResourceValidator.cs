using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

namespace StatsClient.MVVM.Core;

/// <summary>
/// Validates XAML resource markup conventions that cause runtime XamlParseException if violated.
/// </summary>
internal static partial class XamlResourceValidator
{
    private static readonly Regex TriggerBlockPattern = TriggerBlockRegex();
    private static readonly Regex StyleTriggersBlockPattern = StyleTriggersBlockRegex();

    /// <summary>
    /// DynamicResource is only valid on DependencyProperty setters of DependencyObjects.
    /// Trigger/DataTrigger Setter.Value is NOT a dependency property — use StaticResource there.
    /// StaticResource keys must exist before theme dictionaries load (see App.xaml merge order).
    /// </summary>
    public static void ValidateNoDynamicResourceInTriggers(string contentRoot)
    {
        var xamlFiles = Directory.GetFiles(contentRoot, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                           && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        var errors = new List<string>();

        foreach (var file in xamlFiles)
        {
            var content = File.ReadAllText(file);
            foreach (Match match in TriggerBlockPattern.Matches(content))
            {
                if (match.Value.Contains("{DynamicResource", StringComparison.Ordinal))
                {
                    var relative = Path.GetRelativePath(contentRoot, file);
                    errors.Add($"{relative}: DynamicResource inside Trigger/DataTrigger — use StaticResource for Setter.Value.");
                }
            }

            foreach (Match match in StyleTriggersBlockPattern.Matches(content))
            {
                if (match.Value.Contains("{DynamicResource", StringComparison.Ordinal))
                {
                    var relative = Path.GetRelativePath(contentRoot, file);
                    errors.Add($"{relative}: DynamicResource inside Style.Triggers — use StaticResource for Setter.Value.");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "XAML resource validation failed:\n" + string.Join("\n", errors.Take(20)) +
                (errors.Count > 20 ? $"\n... and {errors.Count - 20} more." : string.Empty));
        }
    }

    public static void ValidateProjectXaml()
    {
        var contentRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
        if (!Directory.Exists(Path.Combine(contentRoot, "Themes")))
        {
            contentRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory));
            while (contentRoot is not null && !Directory.Exists(Path.Combine(contentRoot, "Themes")))
            {
                contentRoot = Directory.GetParent(contentRoot)?.FullName;
            }
        }

        if (contentRoot is null || !Directory.Exists(Path.Combine(contentRoot, "Themes")))
        {
            return;
        }

        ValidateNoDynamicResourceInTriggers(contentRoot);
        ValidateTriggerStaticResourcesResolve(Application.Current);
    }

    /// <summary>
    /// Probe keys used via StaticResource in trigger setters; missing keys become UnsetValue and crash BorderBrush.
    /// </summary>
    public static void ValidateTriggerStaticResourcesResolve(Application? application)
    {
        if (application is null)
        {
            return;
        }

        string[] probeKeys =
        [
            "LvItemImportHistory_HoverBorderBrush",
            "LvItemAccountInfo_HoverBorderBrush",
            "LvListViewArchives_Background",
            "LvListViewFolderSubscription_Background",
            "SettingsSideTabSelectedBorder",
            "AccentColor",
            "ImmersiveGlassBorder",
            "SemanticLinkColor",
            "BlackColor",
            "TransparentColor_Cl",
            "SplashBackground",
            "BusyOverlayCancelBackground",
            "PopupShadowColor_Cl",
            "ViewerMeasurementColor_Cl",
        ];

        var errors = new List<string>();
        foreach (var key in probeKeys)
        {
            var value = application.TryFindResource(key);
            if (value is null)
            {
                errors.Add($"Resource '{key}' not found — ensure ColorSchemes loads before LvItemModern/TabControlStyles in App.xaml.");
            }
            else if (value == DependencyProperty.UnsetValue)
            {
                errors.Add($"Resource '{key}' resolved to UnsetValue — check App.xaml merged dictionary order.");
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "XAML StaticResource validation failed:\n" + string.Join("\n", errors));
        }
    }

    [GeneratedRegex(
        "<(?:MultiDataTrigger|MultiTrigger|DataTrigger|Trigger)\\b[^>]*>.*?</(?:MultiDataTrigger|MultiTrigger|DataTrigger|Trigger)>",
        RegexOptions.Singleline)]
    private static partial Regex TriggerBlockRegex();

    [GeneratedRegex("<Style\\.Triggers>.*?</Style\\.Triggers>", RegexOptions.Singleline)]
    private static partial Regex StyleTriggersBlockRegex();
}
