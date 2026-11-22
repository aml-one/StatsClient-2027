using System.Text;

namespace StatsClient.MVVM.Core;

public static class PatientNameMatcher
{
    public static bool IsLikelySamePatient(string? statsFirst, string? statsLast, string? labnextPatientLine)
    {
        var statsTokens = Tokenize(statsFirst, statsLast);
        var labnextTokens = TokenizeLabnextLine(labnextPatientLine);

        if (statsTokens.Count == 0 || labnextTokens.Count == 0)
            return false;

        int matched = 0;
        foreach (string token in statsTokens)
        {
            if (labnextTokens.Any(l => TokensEqualFuzzy(token, l)))
                matched++;
        }

        int required = statsTokens.Count >= 2 ? 2 : 1;
        return matched >= required;
    }

    private static List<string> Tokenize(string? first, string? last)
    {
        var tokens = new List<string>();
        AddNameTokens(tokens, first);
        AddNameTokens(tokens, last);
        return tokens
            .Where(t => t.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> TokenizeLabnextLine(string? patientLine)
    {
        if (string.IsNullOrWhiteSpace(patientLine))
            return [];

        return Normalize(patientLine)
            .Split([' ', ',', '-', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddNameTokens(List<string> tokens, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        foreach (string part in value.Split(['-', ',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string normalized = Normalize(part);
            if (normalized.Length >= 2)
                tokens.Add(normalized);
        }
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char c in value.Trim().ToUpperInvariant())
        {
            if (char.IsLetter(c))
                builder.Append(c);
        }

        return builder.ToString();
    }

    private static bool TokensEqualFuzzy(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
            return true;

        int maxDistance = a.Length <= 4 || b.Length <= 4 ? 1 : 2;
        return LevenshteinDistance(a, b) <= maxDistance;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        if (a.Length == 0)
            return b.Length;
        if (b.Length == 0)
            return a.Length;

        int[,] matrix = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++)
            matrix[i, 0] = i;
        for (int j = 0; j <= b.Length; j++)
            matrix[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[a.Length, b.Length];
    }
}
