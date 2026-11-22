namespace StatsClient.MVVM.Model;

/// <summary>
/// Simplified prescription fields for display and reporting.
/// </summary>
public sealed record PrescriptionSummary
{
    public string System { get; init; } = "";
    public string? OrderId { get; init; }
    public string? DoctorName { get; init; }
    public string? PatientName { get; init; }
    public string? Shade { get; init; }
    public string? TeethNumbers { get; init; }
    public string? CommentsNotes { get; init; }
    public string SourcePdfPath { get; init; } = "";
}
