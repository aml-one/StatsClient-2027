namespace StatsClient.MVVM.Model;

public sealed class PrescriptionSavedState
{
    public required string PanNumber { get; init; }
    public required string StampHeader { get; init; }
    public DateTime SavedAt { get; init; }
    public bool HasPan { get; init; }
    public bool HasRush { get; init; }
    public bool HasInfoLabel { get; init; }
    public PrescriptionInfoLabelKind InfoLabelKind { get; init; }
    public string InfoLabelText { get; init; } = "";
    public string CurrentPdfPath { get; init; } = "";
    public string OriginalPdfPath { get; init; } = "";
    public string PanOnlyPdfPath { get; init; } = "";
    public string PreInfoPdfPath { get; init; } = "";
    public string PreviewPngPath { get; init; } = "";
}

public sealed class PrescriptionPreviousNumberRow
{
    public required string PanNumber { get; init; }
    public required string SavedTimeDisplay { get; init; }
    public required string StatusSummary { get; init; }
    public bool CanReload { get; init; }
}
