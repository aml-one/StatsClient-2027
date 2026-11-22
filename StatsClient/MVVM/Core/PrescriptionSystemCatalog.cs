namespace StatsClient.MVVM.Core;

/// <summary>
/// PageHeaderIsHigh codes and display names for prescription scanner systems.
/// Code "9" = unknown / future systems until a dedicated profile is added.
/// </summary>
public static class PrescriptionSystemCatalog
{
    public const string UnknownCode = "9";

    public static string DisplayName(string? pageHeaderIsHigh) =>
        pageHeaderIsHigh switch
        {
            "0" => "iTero (low header)",
            "1" => "iTero (high header)",
            "3" => "IS3D / Intelliscan",
            "4" => "TRIOS",
            "5" => "Medit",
            "6" => "DS Core",
            "7" => "ASConnect",
            "8" => "Dexis",
            UnknownCode => "Unknown / Unclassified",
            null or "" => "Unknown / Unclassified",
            _ => $"System {pageHeaderIsHigh}",
        };

    public static string NormalizeCode(string? pageHeaderIsHigh) =>
        string.IsNullOrWhiteSpace(pageHeaderIsHigh) ? UnknownCode : pageHeaderIsHigh;
}
