using StatsClient.MVVM.Model;

namespace StatsClient.MVVM.Core;

/// <summary>Bounds of the auto-cropped pan-stack region in the original photo (normalized 0–1).</summary>
public sealed class PanStackVisionCropResult
{
    public byte[] ImagePng { get; init; } = [];
    public bool WasCropped { get; init; }
    public double OriginalX0 { get; init; }
    public double OriginalX1 { get; init; } = 1;
    public double OriginalY0 { get; init; }
    public double OriginalY1 { get; init; } = 1;
}

/// <summary>
/// One horizontal crop of the full pan-stack photo (left → right).
/// </summary>
public sealed class PanStackVisionSection
{
    public int Index { get; init; }
    public int Count { get; init; }
    public byte[] ImagePng { get; init; } = [];
    /// <summary>Normalized X range in the full image (0–1).</summary>
    public double X0 { get; init; }
    public double X1 { get; init; }
}

public sealed class PanStackVisionProgress
{
    public int ChunkIndex { get; init; }
    public int ChunkCount { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool IsComplete { get; init; }
    public IReadOnlyList<PanStackVisionColumnData>? PartialColumns { get; init; }
    public int LabelsReadSoFar { get; init; }
    public int SkippedLowConfidenceSoFar { get; init; }
    public int SectionsCompleted { get; init; }
    public bool IsSectionStarting { get; init; }
}
