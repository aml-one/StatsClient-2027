using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using StatsClient.KnowledgeBase.Core;

namespace StatsClient.KnowledgeBaseApi.Services;

public sealed class KnowledgeBaseVisionSearchService
{
    private const int MaxCandidates = 20;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(300) };

    private readonly KnowledgeBaseRepository _repository;

    public KnowledgeBaseVisionSearchService(KnowledgeBaseRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<KnowledgeBaseVisionMatch>> SearchAsync(
        byte[] queryImageBytes,
        IReadOnlyList<int> cardIds,
        Func<string?>? readApiKey = null,
        CancellationToken cancellationToken = default)
    {
        readApiKey ??= () => null;
        var apiKey = readApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("NVIDIA API key is required for vision search.");
        }

        var images = await _repository.GetThumbnailCandidatesAsync(cardIds, MaxCandidates, cancellationToken).ConfigureAwait(false);
        if (images.Count == 0)
        {
            return [];
        }

        var prompt = BuildPrompt(images);
        var content = await CallVisionAsync(apiKey!, queryImageBytes, prompt, cancellationToken).ConfigureAwait(false);
        return ParseMatches(content, images);
    }

    private static string BuildPrompt(IReadOnlyList<KnowledgeBaseCardImage> images)
    {
        var lines = images.Select(i => $"- imageId={i.ImageId}, cardId={i.CardId}, file={i.FileName}");
        return """
            You are comparing a query photo to knowledge base reference thumbnails.
            Reference entries:
            """ + string.Join(Environment.NewLine, lines) + """

            Identify the best matching reference imageId for the query photo.
            Return ONLY JSON array like:
            [{"imageId":123,"cardId":45,"confidence":0.87,"reason":"same scan body shape"}]
            Include up to 3 matches sorted by confidence descending.
            """;
    }

    private static async Task<string> CallVisionAsync(
        string apiKey,
        byte[] imagePng,
        string prompt,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            model = "google/gemma-3n-e4b-it",
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = prompt },
                        new
                        {
                            type = "image_url",
                            image_url = new
                            {
                                url = $"data:image/png;base64,{Convert.ToBase64String(imagePng)}"
                            }
                        }
                    }
                }
            },
            max_tokens = 512,
            temperature = 0.1,
            stream = false
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://integrate.api.nvidia.com/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }

    private static IReadOnlyList<KnowledgeBaseVisionMatch> ParseMatches(string content, IReadOnlyList<KnowledgeBaseCardImage> images)
    {
        content = content.Trim();
        int start = content.IndexOf('[');
        int end = content.LastIndexOf(']');
        if (start < 0 || end <= start)
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<VisionMatchDto>>(content[start..(end + 1)]);
            if (parsed is null)
            {
                return [];
            }

            return parsed
                .Select(m =>
                {
                    var image = images.FirstOrDefault(i => i.ImageId == m.ImageId);
                    return new KnowledgeBaseVisionMatch
                    {
                        CardId = m.CardId > 0 ? m.CardId : image?.CardId ?? 0,
                        Title = image?.FileName ?? $"Image {m.ImageId}",
                        Confidence = m.Confidence,
                        Reason = m.Reason ?? string.Empty
                    };
                })
                .Where(m => m.CardId > 0)
                .OrderByDescending(m => m.Confidence)
                .Take(3)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private sealed class VisionMatchDto
    {
        [JsonPropertyName("imageId")]
        public int ImageId { get; set; }

        [JsonPropertyName("cardId")]
        public int CardId { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }
}
