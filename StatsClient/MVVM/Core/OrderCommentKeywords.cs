namespace StatsClient.MVVM.Core;

public static class OrderCommentKeywords
{
    /// <summary>
    /// True when <paramref name="comments"/> contains a rush keyword (same rules as user panel rush detection).
    /// </summary>
    public static bool ContainsRushKeyword(string? comments)
    {
        if (string.IsNullOrWhiteSpace(comments))
            return false;

        string text = comments;
        return text.Contains(" rush", StringComparison.OrdinalIgnoreCase)
            || text.Contains("rush ", StringComparison.OrdinalIgnoreCase)
            || text.Equals("rush", StringComparison.OrdinalIgnoreCase)
            || text.Contains("expedite ", StringComparison.OrdinalIgnoreCase)
            || text.Contains(" expedite", StringComparison.OrdinalIgnoreCase)
            || text.Equals("expedite", StringComparison.OrdinalIgnoreCase)
            || text.Contains("asap ", StringComparison.OrdinalIgnoreCase)
            || text.Contains(" asap", StringComparison.OrdinalIgnoreCase)
            || text.Equals("asap", StringComparison.OrdinalIgnoreCase);
    }
}
