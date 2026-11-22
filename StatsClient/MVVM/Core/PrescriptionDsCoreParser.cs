using System.Text.RegularExpressions;

namespace StatsClient.MVVM.Core;

/// <summary>
/// DS Core PDFs may contain multiple case sheets in one export. Parsing helpers
/// target the first order block and support comma-separated tooth lists (e.g. bridges).
/// </summary>
internal static class PrescriptionDsCoreParser
{
    public static string SliceFirstOrderBlock(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        int first = text.IndexOf("Order Number:", StringComparison.OrdinalIgnoreCase);
        if (first < 0)
        {
            return text;
        }

        int searchFrom = first + "Order Number:".Length;
        int second = text.IndexOf("Order Number:", searchFrom, StringComparison.OrdinalIgnoreCase);
        return second < 0 ? text : text[..second].TrimEnd();
    }

    public static string? ExtractDoctorName(string text)
    {
        string? raw = MatchFirst(text, @"\bDr\.?\s+([^\n\r]+?)(?:\s+wrote at\b|\s*$)", ignoreCase: true)
            ?? MatchFirst(text, @"\bDr\.?\s+([^\n\r]+)", ignoreCase: true);

        string? cleaned = CleanDoctor(raw);
        if (IsPlausiblePersonName(cleaned))
        {
            return cleaned;
        }

        return ExtractDoctorLastNameFromCommentAuthor(text);
    }

    public static string? ExtractShade(string caseText, string? comments)
    {
        string? raw = MatchFirst(caseText, @"Tooth shade:\s*(.+)", ignoreCase: true)
            ?? MatchFirst(comments ?? caseText, @"Shade-?\s*([\w/+-]+)", ignoreCase: true);

        return PrescriptionShadeCatalog.NormalizeExtractedShade(raw) ?? raw?.Trim();
    }

    private static string? ExtractDoctorLastNameFromCommentAuthor(string text)
    {
        string? author = MatchFirst(
            text,
            @"^([A-Za-z][^\n\r]+?)\s+wrote at\b",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        if (string.IsNullOrWhiteSpace(author))
        {
            return null;
        }

        string[] parts = author.Trim().Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        string lastName = parts[^1];
        return IsPlausiblePersonName(lastName) ? lastName : null;
    }

    public static string? ExtractTeethNumbers(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var teeth = new SortedSet<int>();

        foreach (Match match in Regex.Matches(text, @"Tooth \(UNS\):\s*([^\n\r]+)", RegexOptions.IgnoreCase))
        {
            CollectTeethFromSegment(teeth, match.Groups[1].Value);
        }

        foreach (Match match in Regex.Matches(text, @"#\s*(\d{1,2})\b"))
        {
            if (int.TryParse(match.Groups[1].Value, out int n) && n is >= 1 and <= 32)
            {
                teeth.Add(n);
            }
        }

        return teeth.Count == 0 ? null : string.Join(",", teeth);
    }

    public static bool IsPlausiblePersonName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        string value = name.Trim();
        if (value.Length < 4)
        {
            return false;
        }

        if (Regex.IsMatch(value, @"\bwrote at\b|\d|Service type|Category:", RegexOptions.IgnoreCase))
        {
            return false;
        }

        string[] words = value.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return false;
        }

        if (words.Any(w => w.Length >= 4 && char.IsUpper(w[0])))
        {
            return true;
        }

        return words.Length >= 2
            && words.Any(w => w.Length >= 5)
            && value.Count(char.IsLetter) >= (int)(value.Length * 0.85);
    }

    private static string? CleanDoctor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string cleaned = value.Trim();
        if (cleaned.StartsWith("Dr.", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[3..].Trim();
        }
        else if (cleaned.StartsWith("Dr ", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[2..].Trim();
        }

        int lineBreak = cleaned.IndexOfAny(['\r', '\n']);
        if (lineBreak >= 0)
        {
            cleaned = cleaned[..lineBreak].Trim();
        }

        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private static void CollectTeethFromSegment(SortedSet<int> teeth, string segment)
    {
        foreach (Match rangeMatch in Regex.Matches(segment, @"(\d{1,2})\s*-\s*(\d{1,2})"))
        {
            if (!int.TryParse(rangeMatch.Groups[1].Value, out int start)
                || !int.TryParse(rangeMatch.Groups[2].Value, out int end))
            {
                continue;
            }

            if (start > end)
            {
                (start, end) = (end, start);
            }

            for (int i = start; i <= end && i <= start + 15; i++)
            {
                TryAddTooth(teeth, i);
            }
        }

        foreach (Match num in Regex.Matches(segment, @"\b(\d{1,2})\b"))
        {
            if (int.TryParse(num.Groups[1].Value, out int n))
            {
                TryAddTooth(teeth, n);
            }
        }
    }

    private static void TryAddTooth(SortedSet<int> teeth, int n)
    {
        if (n is >= 1 and <= 32)
        {
            teeth.Add(n);
        }
    }

    private static string? MatchFirst(string text, string pattern, bool ignoreCase = false) =>
        MatchFirst(text, pattern, ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);

    private static string? MatchFirst(string text, string pattern, RegexOptions options)
    {
        Match match = Regex.Match(text, pattern, options);
        if (!match.Success || match.Groups.Count < 2)
        {
            return null;
        }

        string value = match.Groups[1].Value.Trim();
        int lineBreak = value.IndexOfAny(['\r', '\n']);
        if (lineBreak >= 0)
        {
            value = value[..lineBreak].Trim();
        }

        return string.IsNullOrWhiteSpace(value) || value is "--" or "—" ? null : value;
    }
}
