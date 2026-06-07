namespace StatsClient.MVVM.Model;

public sealed class WatchListEntry
{
    public string IntOrderID { get; set; } = "";
    public DateTime AddedUtc { get; set; }

    public string? LastProcessStatusID { get; set; }
    public string? LastProcessLockID { get; set; }

    public string? Patient_FirstName { get; set; }
    public string? Patient_LastName { get; set; }
    public string? Customer { get; set; }
    public string? Items { get; set; }
    public string? PanNumber { get; set; }
    public string? ImageSource { get; set; }
}
