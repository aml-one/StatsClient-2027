using System.Text.RegularExpressions;

namespace StatsClient.MVVM.Core;

public static class ArchiveImportNameHelper
{
    private static readonly Regex LeadingPanPrefixRegex = new(@"^\d{2,5}-", RegexOptions.CultureInvariant);

    public static bool HasLeadingPanPrefix(string? orderId) =>
        !string.IsNullOrWhiteSpace(orderId) && LeadingPanPrefixRegex.IsMatch(orderId);

    /// <summary>
    /// Replaces the leading pan segment (2–5 digits and dash) with <paramref name="newPan"/>.
    /// Caller must ensure <see cref="HasLeadingPanPrefix"/> is true.
    /// </summary>
    public static string ReplaceLeadingPan(string orderId, int newPan)
    {
        int dashIndex = orderId.IndexOf('-');
        if (dashIndex < 0)
            return orderId;

        return $"{newPan}-{orderId[(dashIndex + 1)..]}";
    }
}
