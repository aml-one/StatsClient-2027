using DCMViewer.Services;
using StatsClient.KnowledgeBase.Core;
using static StatsClient.MVVM.Core.DatabaseConnection;

namespace StatsClient.MVVM.Core.KnowledgeBase;

public sealed class KnowledgeBaseVisionSearchService
{
    private const int MaxCandidates = 20;
    private readonly KnowledgeBaseRepository _repository = new();
    private readonly NvidiaVisionService _vision = new();

    public async Task<IReadOnlyList<KnowledgeBaseVisionMatch>> SearchAsync(
        byte[] queryImageBytes,
        IReadOnlyList<int> cardIds,
        Func<string?>? readApiKey = null,
        CancellationToken cancellationToken = default)
    {
        readApiKey ??= () => ReadStatsSetting("Nvidia_API_KEY");
        var apiKey = readApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Nvidia_API_KEY is not set in Stats database Settings.");
        }

        var images = await _repository.GetThumbnailCandidatesAsync(cardIds, MaxCandidates, cancellationToken).ConfigureAwait(false);
        if (images.Count == 0)
        {
            return [];
        }

        var prompt = BuildPrompt(images);
        var result = await _vision.AnalyzeImageAsync(
            apiKey!,
            NvidiaVisionService.DefaultVisionEndpoint,
            queryImageBytes,
            prompt,
            maxTokens: 512,
            temperature: 0.1,
            topP: 0.85,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(result.ErrorSummary))
        {
            throw new InvalidOperationException(result.ErrorSummary);
        }

        return ParseMatches(result.Content, images);
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
            var parsed = System.Text.Json.JsonSerializer.Deserialize<List<VisionMatchDto>>(content[start..(end + 1)]);
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
        public int ImageId { get; set; }
        public int CardId { get; set; }
        public double Confidence { get; set; }
        public string? Reason { get; set; }
    }
}
