using DCMViewer.Services;

using StatsClient.MVVM.Model;

using static StatsClient.MVVM.Core.DatabaseConnection;



namespace StatsClient.MVVM.Core.ScanBodyGallery;



public sealed class ScanBodyGalleryVisionSearchService

{

    private const int MaxCandidates = 30;



    public async Task<IReadOnlyList<ScanBodyGalleryVisionMatch>> SearchAsync(

        byte[] queryImageBytes,

        IReadOnlyList<int> entryIds,

        int minConfidencePercent = 85,

        Func<string?>? readApiKey = null,

        CancellationToken cancellationToken = default)

    {

        readApiKey ??= () => ReadStatsSetting("Nvidia_API_KEY");

        var apiKey = readApiKey();

        if (string.IsNullOrWhiteSpace(apiKey))

            throw new InvalidOperationException("Nvidia_API_KEY is not set in Stats database Settings.");



        minConfidencePercent = Math.Clamp(minConfidencePercent, 50, 99);

        double minConfidence = minConfidencePercent / 100.0;



        var images = await ScanBodyGalleryRepository.GetThumbnailCandidatesAsync(entryIds, MaxCandidates)

            .ConfigureAwait(false);

        if (images.Count == 0)

            return [];



        var prompt = BuildPrompt(images, minConfidencePercent);

        var vision = new NvidiaVisionService();

        var result = await vision.AnalyzeImageAsync(

            apiKey!,

            NvidiaVisionService.DefaultVisionEndpoint,

            queryImageBytes,

            prompt,

            maxTokens: 512,

            temperature: 0.1,

            topP: 0.85,

            cancellationToken: cancellationToken).ConfigureAwait(false);



        if (!string.IsNullOrWhiteSpace(result.ErrorSummary))

            throw new InvalidOperationException(result.ErrorSummary);



        return ParseMatches(result.Content, images, minConfidence);

    }



    private static string BuildPrompt(IReadOnlyList<ScanBodyGalleryImageModel> images, int minConfidencePercent)

    {

        var lines = images.Select(i => $"- imageId={i.ImageId}, entryId={i.EntryId}, file={i.FileName}");

        return string.Join(Environment.NewLine, new[]

        {

            "You are comparing a dental scan body photo (query image) to scan body gallery reference thumbnails.",

            "Only report matches you are genuinely confident about — avoid false positives.",

            "Reference entries:",

            string.Join(Environment.NewLine, lines),

            "",

            "Identify reference images that match the query (same scan body brand, system, size, or morphology).",

            "Return ONLY a JSON array like:",

            "[{\"imageId\":123,\"entryId\":45,\"confidence\":0.87,\"reason\":\"same scan body shape\"}]",

            "Use confidence as a decimal from 0.0 to 1.0 (not 0–100).",

            "Include up to 5 matches sorted by confidence descending.",

            $"Omit any match below {minConfidencePercent}% confidence.",

            "If nothing matches well enough, return []."

        });

    }



    private static IReadOnlyList<ScanBodyGalleryVisionMatch> ParseMatches(

        string content,

        IReadOnlyList<ScanBodyGalleryImageModel> images,

        double minConfidence)

    {

        content = content.Trim();

        int start = content.IndexOf('[');

        int end = content.LastIndexOf(']');

        if (start < 0 || end <= start)

            return [];



        try

        {

            var parsed = System.Text.Json.JsonSerializer.Deserialize<List<VisionMatchDto>>(content[start..(end + 1)]);

            if (parsed is null)

                return [];



            return parsed

                .Select(m =>

                {

                    var image = images.FirstOrDefault(i => i.ImageId == m.ImageId);

                    double confidence = ScanBodyGalleryVisionMatch.NormalizeConfidence(m.Confidence);

                    return new ScanBodyGalleryVisionMatch

                    {

                        EntryId = m.EntryId > 0 ? m.EntryId : image?.EntryId ?? 0,

                        Title = image?.FileName ?? $"Image {m.ImageId}",

                        Confidence = confidence,

                        Reason = m.Reason ?? string.Empty

                    };

                })

                .Where(m => m.EntryId > 0 && m.Confidence >= minConfidence)

                .OrderByDescending(m => m.Confidence)

                .Take(5)

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

        public int EntryId { get; set; }

        public double Confidence { get; set; }

        public string? Reason { get; set; }

    }

}

