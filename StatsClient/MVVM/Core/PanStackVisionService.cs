using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using DCMViewer.Services;
using StatsClient.MVVM.Model;
using static StatsClient.MVVM.ViewModel.MainViewModel;

namespace StatsClient.MVVM.Core;

public sealed class PanStackVisionParseResult
{
    public bool IsSuccess { get; init; }
    public IReadOnlyList<PanStackVisionColumnData> Columns { get; init; } = [];
    public int SkippedLowConfidenceCount { get; init; }
    public string ErrorSummary { get; init; } = string.Empty;
    public string RawContent { get; init; } = string.Empty;
    public int SectionsProcessed { get; init; }

    public IReadOnlyList<string> Numbers =>
        Columns.SelectMany(column => column.Labels.Select(label => label.Number)).ToList();
}

/// <summary>
/// Reads pan numbers from photos of stacked model boxes (foreground stacks only).
/// Large stacks are analyzed as several vertical image sections (~5 vision calls).
/// </summary>
public sealed class PanStackVisionService
{
    /// <summary>Only labels at or above this confidence are returned (0.0–1.0).</summary>
    public const double MinimumLabelConfidence = 0.88;

    public const string SettingPanStackVisionMaxTokens = "PanStackVision_MaxTokens";
    public const string SettingPanStackVisionChunkCount = "PanStackVision_ChunkCount";

    private const int DefaultChunkCount = 5;
    private const int MaxChunkCount = 8;
    private const int PerSectionMaxTokens = 2048;
    private const int SingleShotMaxTokens = 8192;
    private const int MinimumMaxTokens = 1024;
    private const int MaximumMaxTokens = 8192;
    private const double DefaultTemperature = 0.05;
    private const double DefaultTopP = 0.85;

    private readonly NvidiaVisionService _vision = new();

    public async Task<PanStackVisionParseResult> ParsePanStackImageAsync(
        byte[] imagePng,
        CancellationToken cancellationToken = default,
        IProgress<PanStackVisionProgress>? progress = null)
    {
        if (imagePng.Length == 0)
        {
            return Fail("Image is empty.");
        }

        var chunkCount = ResolveChunkCount(imagePng);
        if (chunkCount <= 1)
        {
            return await ParseSingleImageVisionAsync(imagePng, cancellationToken, progress).ConfigureAwait(false);
        }

        var sections = BuildSections(imagePng);
        return await ParseInSectionsAsync(sections, cancellationToken, progress).ConfigureAwait(false);
    }

    /// <summary>Build vertical crops on the UI thread before calling <see cref="ParseSectionsAsync"/>.</summary>
    public IReadOnlyList<PanStackVisionSection> BuildSections(byte[] imagePng)
    {
        var chunkCount = ResolveChunkCount(imagePng);
        if (chunkCount <= 1)
        {
            return [new PanStackVisionSection { Index = 0, Count = 1, ImagePng = imagePng, X0 = 0, X1 = 1 }];
        }

        return PanStackVisionImageHelper.SplitIntoVerticalSections(imagePng, chunkCount);
    }

    public async Task<PanStackVisionParseResult> ParseSectionsAsync(
        IReadOnlyList<PanStackVisionSection> sections,
        CancellationToken cancellationToken = default,
        IProgress<PanStackVisionProgress>? progress = null)
    {
        if (sections.Count == 0)
        {
            return Fail("No image sections to analyze.");
        }

        if (sections.Count == 1 && sections[0].Count == 1)
        {
            return await ParsePanStackImageAsync(sections[0].ImagePng, cancellationToken, progress)
                .ConfigureAwait(false);
        }

        return await ParseInSectionsAsync(sections, cancellationToken, progress).ConfigureAwait(false);
    }

    private async Task<PanStackVisionParseResult> ParseInSectionsAsync(
        IReadOnlyList<PanStackVisionSection> sections,
        CancellationToken cancellationToken,
        IProgress<PanStackVisionProgress>? progress)
    {
        var visionSettings = LoadVisionSettings(singleShot: false);

        var allColumns = new List<PanStackVisionColumnData>();
        var skippedTotal = 0;
        var rawParts = new List<string>();
        var sectionErrors = new List<string>();
        var sectionsOk = 0;
        var mergeLock = new object();

        await Parallel.ForEachAsync(
            sections,
            new ParallelOptions { MaxDegreeOfParallelism = 3, CancellationToken = cancellationToken },
            async (section, _) =>
            {
                var sectionNumber = section.Index + 1;

                try
                {
                    int completedBefore;
                    lock (mergeLock)
                    {
                        completedBefore = sectionsOk;
                    }

                    progress?.Report(new PanStackVisionProgress
                    {
                        ChunkIndex = section.Index,
                        ChunkCount = sections.Count,
                        SectionsCompleted = completedBefore,
                        IsSectionStarting = true,
                        Message = $"Reading section {sectionNumber} of {sections.Count} (left → right)…"
                    });

                    byte[] visionImage;
                    string imageMimeType;
                    try
                    {
                        visionImage = PanStackVisionImageHelper.PrepareForVisionApi(section.ImagePng);
                        imageMimeType = visionImage.Length < section.ImagePng.Length ? "image/jpeg" : "image/png";
                    }
                    catch (Exception ex)
                    {
                        lock (mergeLock)
                        {
                            sectionErrors.Add($"Section {sectionNumber}: {ex.Message}");
                        }

                        return;
                    }

                    var vision = await CallVisionWithRetryAsync(
                        visionSettings,
                        visionImage,
                        BuildSectionPrompt(section.Index, section.Count),
                        imageMimeType,
                        cancellationToken).ConfigureAwait(false);

                    if (!vision.IsSuccess)
                    {
                        lock (mergeLock)
                        {
                            sectionErrors.Add($"Section {sectionNumber}: {vision.ErrorSummary}");
                        }

                        return;
                    }

                    LogVisionJson($"section {sectionNumber}/{sections.Count}", vision.Content);

                    if (!PanStackVisionJsonParser.TryParseMatrix(
                            vision.Content,
                            MinimumLabelConfidence,
                            out var sectionColumns,
                            out var skipped,
                            out var parseError))
                    {
                        lock (mergeLock)
                        {
                            sectionErrors.Add($"Section {sectionNumber}: {parseError}");
                        }

                        return;
                    }

                    PanStackVisionColumnMerger.RemapToFullImage(sectionColumns, section.X0, section.X1);
                    PanStackVisionColumnMerger.NormalizeSectionYAxis(sectionColumns);
                    PanStackVisionColumnMerger.CaptureOverlayCoordinates(sectionColumns);
                    LogParsedLabels($"section {sectionNumber}/{sections.Count} after remap", sectionColumns);

                    List<PanStackVisionColumnData> snapshot;
                    int labelsSoFar;
                    lock (mergeLock)
                    {
                        allColumns.AddRange(sectionColumns);
                        skippedTotal += skipped;
                        sectionsOk++;
                        rawParts.Add($"/* section {sectionNumber} */\n{vision.Content}");
                        snapshot = PanStackVisionColumnOrganizer.Organize(allColumns);
                        labelsSoFar = snapshot.Sum(column => column.Labels.Count);
                    }

                    int completed;
                    lock (mergeLock)
                    {
                        completed = sectionsOk;
                    }

                    progress?.Report(new PanStackVisionProgress
                    {
                        ChunkIndex = section.Index,
                        ChunkCount = sections.Count,
                        SectionsCompleted = completed,
                        IsSectionStarting = false,
                        Message = $"Section {sectionNumber} of {sections.Count} done — {labelsSoFar} label(s) so far…",
                        PartialColumns = snapshot,
                        LabelsReadSoFar = labelsSoFar,
                        SkippedLowConfidenceSoFar = skippedTotal
                    });
                }
                catch (Exception ex)
                {
                    lock (mergeLock)
                    {
                        sectionErrors.Add($"Section {sectionNumber}: {ex.Message}");
                    }
                }
            }).ConfigureAwait(false);

        if (allColumns.Count == 0)
        {
            var detail = sectionErrors.Count > 0
                ? string.Join(" ", sectionErrors)
                : "No labels found in any section.";
            return Fail(detail, string.Join("\n\n", rawParts));
        }

        var merged = PanStackVisionColumnOrganizer.Organize(allColumns);

        if (sectionErrors.Count > 0 && sectionsOk > 0)
        {
            rawParts.Add($"/* warnings: {string.Join("; ", sectionErrors)} */");
        }

        var finalLabels = merged.Sum(column => column.Labels.Count);
        progress?.Report(new PanStackVisionProgress
        {
            ChunkIndex = sections.Count,
            ChunkCount = sections.Count,
            Message = $"Done — {finalLabels} label(s). Review the matrix.",
            PartialColumns = merged,
            LabelsReadSoFar = finalLabels,
            SkippedLowConfidenceSoFar = skippedTotal,
            IsComplete = true,
            SectionsCompleted = sections.Count
        });

        return new PanStackVisionParseResult
        {
            IsSuccess = true,
            Columns = merged,
            SkippedLowConfidenceCount = skippedTotal,
            SectionsProcessed = sectionsOk,
            RawContent = string.Join("\n\n", rawParts),
            ErrorSummary = sectionErrors.Count > 0 ? string.Join("; ", sectionErrors) : string.Empty
        };
    }

    private async Task<PanStackVisionParseResult> ParseSingleImageVisionAsync(
        byte[] imagePng,
        CancellationToken cancellationToken,
        IProgress<PanStackVisionProgress>? progress)
    {
        progress?.Report(new PanStackVisionProgress
        {
            ChunkIndex = 0,
            ChunkCount = 1,
            Message = "AI vision — analyzing full image…"
        });

        byte[] visionImage;
        string imageMimeType;
        try
        {
            visionImage = PanStackVisionImageHelper.PrepareForVisionApi(imagePng);
            imageMimeType = visionImage.Length < imagePng.Length ? "image/jpeg" : "image/png";
        }
        catch (Exception ex)
        {
            return Fail($"Could not prepare image for vision API: {ex.Message}");
        }

        var visionSettings = LoadVisionSettings(singleShot: true);

        var vision = await _vision.AnalyzeImageAsync(
            visionSettings.ApiKey,
            visionSettings.Endpoint,
            visionImage,
            BuildFullImagePrompt(),
            visionSettings.MaxTokens,
            visionSettings.Temperature,
            visionSettings.TopP,
            cancellationToken,
            imageMimeType,
            enableThinking: false).ConfigureAwait(false);

        if (!vision.IsSuccess)
        {
            return Fail(
                string.IsNullOrWhiteSpace(vision.ErrorSummary)
                    ? "Vision API call failed."
                    : vision.ErrorSummary,
                vision.Content);
        }

        LogVisionJson("full image", vision.Content);

        if (!PanStackVisionJsonParser.TryParseMatrix(
                vision.Content,
                MinimumLabelConfidence,
                out var columns,
                out var skippedLowConfidence,
                out var parseError))
        {
            return Fail(parseError, vision.Content);
        }

        if (columns.Count == 0 || columns.All(column => column.Labels.Count == 0))
        {
            var detail = skippedLowConfidence > 0
                ? $" All {skippedLowConfidence} candidate label(s) were below {MinimumLabelConfidence:P0} confidence."
                : string.Empty;
            return Fail($"No high-confidence pan numbers on foreground box fronts.{detail}", vision.Content);
        }

        PanStackVisionColumnMerger.NormalizeSectionYAxis(columns);
        PanStackVisionColumnMerger.CaptureOverlayCoordinates(columns);
        LogParsedLabels("full image", columns);
        columns = PanStackVisionColumnOrganizer.Organize(columns);
        var labelCount = columns.Sum(column => column.Labels.Count);
        progress?.Report(new PanStackVisionProgress
        {
            ChunkIndex = 1,
            ChunkCount = 1,
            Message = $"AI vision done — {labelCount} label(s). Review the matrix.",
            PartialColumns = columns,
            LabelsReadSoFar = labelCount,
            SkippedLowConfidenceSoFar = skippedLowConfidence,
            IsComplete = true
        });

        return new PanStackVisionParseResult
        {
            IsSuccess = true,
            Columns = columns,
            SkippedLowConfidenceCount = skippedLowConfidence,
            SectionsProcessed = 1,
            RawContent = "vision\n" + vision.Content
        };
    }

    private static int ResolveChunkCount(byte[] imagePng)
    {
        _ = int.TryParse(DatabaseConnection.ReadStatsSetting(SettingPanStackVisionChunkCount), out int configured);
        if (configured <= 0)
        {
            configured = DefaultChunkCount;
        }

        configured = Math.Clamp(configured, 1, MaxChunkCount);
        if (configured == 1)
        {
            return 1;
        }

        try
        {
            using var stream = new MemoryStream(imagePng);
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnLoad);
            var aspect = frame.PixelWidth / (double)Math.Max(1, frame.PixelHeight);

            // Portrait / single stack — one shot is enough.
            if (aspect < 0.9)
            {
                return 1;
            }

            if (aspect < 1.35)
            {
                return Math.Min(2, configured);
            }

            if (aspect < 1.75)
            {
                return Math.Min(3, configured);
            }

            return Math.Clamp(Math.Max(configured, 7), 1, MaxChunkCount);
        }
        catch
        {
            return configured;
        }
    }

    private VisionSettings LoadVisionSettings(bool singleShot)
    {
        string apiKey = DatabaseConnection.ReadStatsSetting("Nvidia_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Nvidia_API_KEY is not set in Stats database Settings.");
        }

        string endpoint = DatabaseConnection.ReadStatsSetting(SettingEncodeIdentifierVisionEndpoint);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = DefaultEncodeIdentifierVisionEndpoint;
        }
        else if (endpoint.Contains("ai.api.nvidia.com/v1/vlm", StringComparison.OrdinalIgnoreCase))
        {
            endpoint = DefaultEncodeIdentifierVisionEndpoint;
        }

        int maxTokens;
        if (singleShot)
        {
            _ = int.TryParse(DatabaseConnection.ReadStatsSetting(SettingPanStackVisionMaxTokens), out maxTokens);
            if (maxTokens <= 0)
            {
                maxTokens = SingleShotMaxTokens;
            }

            maxTokens = Math.Clamp(maxTokens, MinimumMaxTokens, MaximumMaxTokens);
        }
        else
        {
            maxTokens = PerSectionMaxTokens;
        }

        _ = double.TryParse(DatabaseConnection.ReadStatsSetting(SettingEncodeIdentifierTemperature), out double temperature);
        if (temperature <= 0)
        {
            temperature = DefaultTemperature;
        }

        _ = double.TryParse(DatabaseConnection.ReadStatsSetting(SettingEncodeIdentifierTopP), out double topP);
        if (topP <= 0)
        {
            topP = DefaultTopP;
        }

        return new VisionSettings(apiKey, endpoint, maxTokens, temperature, topP);
    }

    public static string BuildFullImagePrompt() => BuildPanStackVisionPromptCore(
        sectionIntro: null,
        columnIndexRule: "columnIndex 1 = leftmost column in the FULL image.");

    public static string BuildSectionPrompt(int sectionIndex, int sectionCount) =>
        BuildPanStackVisionPromptCore(
            sectionIntro:
            $"""
            This image is horizontal section {sectionIndex + 1} of {sectionCount} (a left-to-right crop of the full pan stack).
            Only read boxes visible in THIS crop. Ignore boxes cut off at the edges unless the label is clearly readable.
            """,
            columnIndexRule:
            "columnIndex 1 = leftmost column visible IN THIS CROP, 2 = next column to the right in this crop only.");

    private static string BuildPanStackVisionPromptCore(string? sectionIntro, string columnIndexRule)
    {
        var minConf = MinimumLabelConfidence.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        var sectionBlock = string.IsNullOrWhiteSpace(sectionIntro)
            ? string.Empty
            : sectionIntro.Trim() + "\n\n";

        return $"""
            {sectionBlock}You read pan numbers printed on white labels on dental model storage boxes.
            Boxes may be any color (green, blue, etc.) — only the white label with black digits matters.

            CRITICAL — DEPTH / FOREGROUND ONLY:
            - Only read numbers on the FRONT FACE of boxes in the main FOREGROUND wall that directly face the camera.
            - NEVER read numbers from the BACKGROUND (shelf above, boxes behind, yellow containers, papers, blurrier boxes farther away).
            - If unclear, OMIT that label (do not guess).

            READ ORDER (strict — vertical stacks, NOT horizontal rows):
            1. Group labels into VERTICAL COLUMNS (stacks), left to right — {columnIndexRule}
            2. Within each column: TOP to BOTTOM (top box first).
            3. Do NOT list row-by-row across the image. Each columns[] entry is ONE vertical stack only.

            EXAMPLE shape: columns[0] = left stack top→bottom, columns[1] = next stack, etc.
            (NOT one row of numbers per column object.)

            CONFIDENCE:
            - confidence 0.0–1.0 per label. Only include labels with confidence >= {minConf}.
            - 2–4 digit pan numbers. Never invent digits.

            POSITION (required, for every label):
            - centerX, centerY: label center relative to the TOP-LEFT corner of THIS image, normalized 0.0–1.0
              (0,0 = top-left corner of the photo; 1,1 = bottom-right corner).
              The image is cropped to the pan stack only — use the full frame (not just the boxes sub-region).
            - centerX must increase from left columns to right columns. centerY must increase from top box to bottom box.

            OUTPUT:
            - Return ONLY valid, COMPLETE JSON (no markdown fences). Close every bracket.
            - Root must be a JSON object with a columns array — NOT a bare top-level array.
            - Compact JSON. columns[] — one object per VERTICAL stack with columnIndex and labels[] (number, confidence, centerX, centerY).
            - labels[] inside each column must be top-to-bottom for that stack only.
            - centerX/centerY required on every label (0.0–1.0 from top-left of THIS image).
            """;
    }

    private static PanStackVisionParseResult Fail(string error, string raw = "") =>
        new()
        {
            IsSuccess = false,
            ErrorSummary = error,
            RawContent = raw
        };

    private static void LogVisionJson(string scope, string json)
    {
        Debug.WriteLine($"[PanStackVision] {scope} — vision JSON:");
        Debug.WriteLine(json);
    }

    private static void LogParsedLabels(string scope, IEnumerable<PanStackVisionColumnData> columns)
    {
        Debug.WriteLine($"[PanStackVision] {scope} — labels (image coords 0–1, top-left origin):");
        foreach (var label in columns.SelectMany(column => column.Labels)
                     .OrderBy(label => label.OverlayCenterY ?? label.CenterY ?? 0)
                     .ThenBy(label => label.OverlayCenterX ?? label.CenterX ?? 0))
        {
            var matrix = label.GridColumn.HasValue && label.RowIndex.HasValue
                ? $"matrix=col {label.GridColumn.Value + 1} row {label.RowIndex.Value + 1}"
                : "matrix=(not placed yet)";

            Debug.WriteLine(
                $"  pan {label.Number}  image=({FormatCoord(label.OverlayCenterX ?? label.CenterX)},{FormatCoord(label.OverlayCenterY ?? label.CenterY)})  {matrix}  conf={label.Confidence:F2}");
        }
    }

    private static string FormatCoord(double? value) =>
        value.HasValue ? value.Value.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) : "null";

    private async Task<NvidiaVisionCallResult> CallVisionWithRetryAsync(
        VisionSettings settings,
        byte[] visionImage,
        string prompt,
        string imageMimeType,
        CancellationToken cancellationToken)
    {
        NvidiaVisionCallResult? last = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(400 * attempt, cancellationToken).ConfigureAwait(false);
            }

            last = await _vision.AnalyzeImageAsync(
                settings.ApiKey,
                settings.Endpoint,
                visionImage,
                prompt,
                settings.MaxTokens,
                settings.Temperature,
                settings.TopP,
                cancellationToken,
                imageMimeType,
                enableThinking: false).ConfigureAwait(false);

            if (last.IsSuccess)
            {
                return last;
            }

            var retryable = last.ErrorSummary.Contains("no message content", StringComparison.OrdinalIgnoreCase)
                            || last.HttpStatusCode == 429;
            if (!retryable)
            {
                break;
            }
        }

        return last!;
    }

    private sealed record VisionSettings(
        string ApiKey,
        string Endpoint,
        int MaxTokens,
        double Temperature,
        double TopP);
}
