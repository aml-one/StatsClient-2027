using System.Text.RegularExpressions;
using StatsClient.KnowledgeBase.Core;
using StatsClient.MVVM.Model;

namespace StatsClient.MVVM.Core.ScanBodyGallery;

public static class ScanBodyKnowledgeBaseCrossReference
{
    private const int MinimumTermLength = 2;

    /// <summary>
    /// Both brand and system must be populated on the scan body, and both must appear in the KB card text.
    /// A single matching term is never enough.
    /// </summary>
    public static bool CardMatchesScanBody(
        string title,
        string body,
        IEnumerable<string> tags,
        string brand,
        string systemName)
    {
        if (!HasRequiredScanBodyFields(brand, systemName))
            return false;

        string haystack = BuildSearchCorpus(title, body, tags);
        return ContainsDistinctTerm(haystack, brand)
            && ContainsDistinctTerm(haystack, systemName);
    }

    public static bool CardMatchesScanBody(KnowledgeBaseCardCrossReferenceInfo card, string brand, string systemName) =>
        CardMatchesScanBody(card.Title, card.BodyText, card.Tags, brand, systemName);

    public static bool CardMatchesScanBody(KnowledgeBaseCardSummary card, string brand, string systemName) =>
        CardMatchesScanBody(card.Title, card.BodyPreview, card.Tags, brand, systemName);

    public static bool CardMatchesScanBody(KnowledgeBaseCardDetail detail, ScanBodyGalleryEntryModel entry) =>
        CardMatchesScanBody(detail.Title, detail.BodyText, detail.Tags, entry.Brand, entry.SystemName);

    public static bool HasRequiredScanBodyFields(string brand, string systemName) =>
        !string.IsNullOrWhiteSpace(brand) && !string.IsNullOrWhiteSpace(systemName);

    private static string BuildSearchCorpus(string title, string body, IEnumerable<string> tags) =>
        $"{title} {body} {string.Join(' ', tags)}";

    private static bool ContainsDistinctTerm(string haystack, string term)
    {
        term = term.Trim();
        if (term.Length < MinimumTermLength)
            return false;

        string pattern = Regex.Escape(term).Replace("\\ ", "\\s+");
        return Regex.IsMatch(
            haystack,
            $@"(?<![\p{{L}}\p{{N}}]){pattern}(?![\p{{L}}\p{{N}}])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
