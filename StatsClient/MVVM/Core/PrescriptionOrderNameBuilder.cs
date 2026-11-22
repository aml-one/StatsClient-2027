using static StatsClient.MVVM.Core.DatabaseOperations;
using static StatsClient.MVVM.Core.Functions;

namespace StatsClient.MVVM.Core;

/// <summary>
/// Builds a final 3Shape-style order ID from prescription extract fields,
/// using the same naming rules as Smart Order Names (customer-last layout).
/// </summary>
public static class PrescriptionOrderNameBuilder
{
    public static IReadOnlyList<string> DigitalSystems { get; } =
    [
        "DROPBOX",
        "DSCORE",
        "EMAIL",
        "ASCONNECT",
        "CARESTREAM",
        "DEXIS",
        "MEDIT",
        "IS3D",
        "SIRONA",
        "ITERO",
        "TRIOS",
    ];

    public static async Task<string> BuildAsync(
        string panNumber,
        string? doctorRaw,
        string? selectedCustomerName,
        string? patientFirstName,
        string? patientLastName,
        string? teethNumbers,
        string? shade,
        string? scannerOrderId,
        string? digitalSystem,
        bool isScrewRetained = false,
        bool namingCustomerFirst = false)
    {
        if (string.IsNullOrWhiteSpace(panNumber))
        {
            return string.Empty;
        }

        string patientName = BuildPatientToken(patientLastName, patientFirstName);
        string customerRaw = !string.IsNullOrWhiteSpace(selectedCustomerName)
            ? selectedCustomerName
            : doctorRaw ?? string.Empty;

        List<string> customerSuggestions = await CustomerHasSuggestedName(customerRaw);
        if (customerSuggestions.Count > 0)
        {
            customerRaw = !string.IsNullOrWhiteSpace(selectedCustomerName)
                ? selectedCustomerName
                : customerSuggestions[0];
        }

        string customer = $"-{CleanUpCustomerName(customerRaw)}";
        string toothToken = FormatTeethToken(teethNumbers);
        string shadeToken = string.IsNullOrWhiteSpace(shade) ? "" : $"-{shade.Trim()}";
        string systemToken = string.IsNullOrWhiteSpace(digitalSystem)
            ? ""
            : $"-{digitalSystem.Trim()}";
        string externalIdToken = BuildExternalOrderToken(scannerOrderId);
        string screwToken = isScrewRetained ? "-SCR" : "";

        string finalName = namingCustomerFirst
            ? $"{panNumber.Trim()}{customer}{patientName}{toothToken}{shadeToken}{externalIdToken}{systemToken}{screwToken}"
            : $"{panNumber.Trim()}{toothToken}{shadeToken}{patientName}{customer}{externalIdToken}{systemToken}{screwToken}";

        return SanitizeOrderName(finalName);
    }

    public static string MapDigitalSystem(string? pageHeaderIsHigh) =>
        PrescriptionSystemCatalog.NormalizeCode(pageHeaderIsHigh) switch
        {
            "0" or "1" => "ITERO",
            "3" => "IS3D",
            "4" => "TRIOS",
            "5" => "MEDIT",
            "6" => "DSCORE",
            "7" => "ASCONNECT",
            "8" => "DEXIS",
            _ => "",
        };

    public static string ResolveDigitalSystem(string? pageHeaderIsHigh)
    {
        string mapped = MapDigitalSystem(pageHeaderIsHigh);
        if (!string.IsNullOrWhiteSpace(mapped)
            && DigitalSystems.Contains(mapped, StringComparer.OrdinalIgnoreCase))
        {
            return DigitalSystems.First(s => s.Equals(mapped, StringComparison.OrdinalIgnoreCase));
        }

        return DigitalSystems[0];
    }

    public static bool ShowScannerOrderId(string? pageHeaderIsHigh)
    {
        string code = PrescriptionSystemCatalog.NormalizeCode(pageHeaderIsHigh);
        return code is "0" or "1" or "6" or "7" or "8";
    }

    public static bool ShowComments(string? pageHeaderIsHigh)
    {
        string code = PrescriptionSystemCatalog.NormalizeCode(pageHeaderIsHigh);
        return code is not ("0" or "1" or "4");
    }

    /// <summary>
    /// One tooth: -8. Multiple teeth: lowest-highest only, e.g. -8-9 or -3-30.
    /// </summary>
    public static string FormatTeethToken(string? teethNumbers)
    {
        HashSet<int> teeth = TeethSelectorLayout.ParseSelection(teethNumbers);
        if (teeth.Count == 0)
        {
            return string.Empty;
        }

        int min = teeth.Min();
        int max = teeth.Max();
        return min == max
            ? $"-{min}"
            : $"-{min}-{max}";
    }

    private static string BuildPatientToken(string? lastName, string? firstName)
    {
        string patientName = $"-{lastName ?? string.Empty}";
        patientName = SanitizePatientToken(patientName);

        if (patientName is "-" or "--")
        {
            patientName = $"-{firstName ?? string.Empty}";
            patientName = SanitizePatientToken(patientName);
        }

        if (patientName is "-" or "--")
        {
            patientName = "-NONAME";
        }

        return patientName;
    }

    private static string SanitizePatientToken(string value) =>
        value.Replace(" ", "_")
            .Replace(",", "")
            .Replace("'", "_")
            .Replace("\"", "_")
            .Replace("+", "_")
            .Replace("\\", "_")
            .Replace("/", "_")
            .Replace(":", "_")
            .Replace("*", "_")
            .Replace("?", "_")
            .Replace("<", "_")
            .Replace(">", "_")
            .Replace("&", "-")
            .Replace("|", "_")
            .Trim();

    private static string BuildExternalOrderToken(string? scannerOrderId)
    {
        string id = (scannerOrderId ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(id) ? string.Empty : $"-{id}";
    }

    private static string SanitizeOrderName(string value) =>
        value.Replace(" ", "_")
            .Replace("'", "")
            .Replace("%", "")
            .Replace("*", "")
            .Replace(",", "")
            .Replace(".", "")
            .Replace("&", "")
            .Replace("@", "")
            .Replace("$", "")
            .Replace("+", "")
            .ToUpperInvariant();

}
