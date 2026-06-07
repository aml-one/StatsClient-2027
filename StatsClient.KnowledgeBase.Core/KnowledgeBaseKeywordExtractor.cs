using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace StatsClient.KnowledgeBase.Core;

public sealed class KnowledgeBaseKeywordExtractor
{
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "for", "from", "has", "have", "in", "is", "it",
        "its", "of", "on", "or", "that", "the", "this", "to", "was", "were", "will", "with", "you", "your"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<IReadOnlyList<string>> ExtractTagsAsync(
        string title,
        string bodyText,
        IEnumerable<string> linkLabels,
        Func<string?>? readApiKey = null,
        CancellationToken cancellationToken = default)
    {
        var sourceText = BuildSourceText(title, bodyText, linkLabels);
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return [];
        }

        readApiKey ??= () => null;
        var apiKey = readApiKey();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var aiTags = await ExtractViaNvidiaAsync(apiKey!, sourceText, cancellationToken).ConfigureAwait(false);
                if (aiTags.Count > 0)
                {
                    return aiTags;
                }
            }
            catch
            {
                // fall back to local extraction
            }
        }

        return ExtractLocally(sourceText);
    }

    public static IReadOnlyList<string> ExtractLocally(string sourceText)
    {
        var tokens = Regex.Matches(sourceText.ToLowerInvariant(), @"[a-z0-9]{3,}")
            .Select(m => m.Value)
            .Where(t => !StopWords.Contains(t))
            .GroupBy(t => t)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Take(3)
            .Select(g => g.Key)
            .ToList();

        return tokens;
    }

    private static string BuildSourceText(string title, string bodyText, IEnumerable<string> linkLabels)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(title))
        {
            sb.AppendLine(title.Trim());
        }

        if (!string.IsNullOrWhiteSpace(bodyText))
        {
            sb.AppendLine(bodyText.Trim());
        }

        foreach (var label in linkLabels.Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            sb.AppendLine(label.Trim());
        }

        return sb.ToString();
    }

    private static async Task<IReadOnlyList<string>> ExtractViaNvidiaAsync(
        string apiKey,
        string sourceText,
        CancellationToken cancellationToken)
    {
        const string endpoint = "https://integrate.api.nvidia.com/v1/chat/completions";
        const string prompt = """
            Extract exactly 2 or 3 short searchable keyword tags from the knowledge base entry below.
            Return ONLY a JSON array of lowercase strings, no markdown, no explanation.
            Example: ["scan body","zirconia","trios"]
            """;

        var request = new NvidiaTextRequest
        {
            Model = "meta/llama-3.1-8b-instruct",
            Messages =
            [
                new NvidiaTextMessage { Role = "user", Content = prompt + "\n\n" + sourceText }
            ],
            MaxTokens = 128,
            Temperature = 0.2
        };

        byte[] requestBytes = JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new ByteArrayContent(requestBytes)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var response = await SharedHttpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var completion = await JsonSerializer.DeserializeAsync<NvidiaTextResponse>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        var content = completion?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
        return ParseTagArray(content);
    }

    private static IReadOnlyList<string> ParseTagArray(string content)
    {
        content = content.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        int start = content.IndexOf('[');
        int end = content.LastIndexOf(']');
        if (start >= 0 && end > start)
        {
            content = content[start..(end + 1)];
        }

        try
        {
            var tags = JsonSerializer.Deserialize<List<string>>(content);
            return tags?
                .Select(KnowledgeBaseTagNormalizer.Normalize)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList() ?? [];
        }
        catch
        {
            return ExtractLocally(content);
        }
    }

    private sealed class NvidiaTextRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<NvidiaTextMessage> Messages { get; set; } = [];
        public int MaxTokens { get; set; }
        public double Temperature { get; set; }
    }

    private sealed class NvidiaTextMessage
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
    }

    private sealed class NvidiaTextResponse
    {
        public List<NvidiaTextChoice>? Choices { get; set; }
    }

    private sealed class NvidiaTextChoice
    {
        public NvidiaTextMessage? Message { get; set; }
    }
}
