using StatsClient.MVVM.Core;

namespace StatsClient.MVVM.Model;

public enum PrescriptionStampEditKind
{
    PanNumber,
    Rush,
    InfoLabel,
}

public enum PrescriptionInfoLabelKind
{
    None,
    SentTo,
    Missing,
}

/// <summary>
/// In-memory session for repositioning stamps on the current prescription (cleared on app restart).
/// </summary>
public sealed class PrescriptionPanEditSession
{
    public PrescriptionStampEditKind Kind { get; init; } = PrescriptionStampEditKind.PanNumber;

    /// <summary>Base PDF for the current edit layer (original, pan-only, pre-info, etc.).</summary>
    public required string OriginalPdfPath { get; init; }
    public required string PanNumber { get; init; }
    public required string StampHeader { get; init; }
    public string InfoDisplayText { get; init; } = "";
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    public float FontSizeDelta { get; set; }
    public bool CanEdit { get; set; } = true;

    public string SystemDisplayName => PrescriptionSystemCatalog.DisplayName(StampHeader);

    public string StampDisplayText => Kind switch
    {
        PrescriptionStampEditKind.Rush => "RUSH",
        PrescriptionStampEditKind.InfoLabel => InfoDisplayText,
        _ => PanNumber,
    };
}
