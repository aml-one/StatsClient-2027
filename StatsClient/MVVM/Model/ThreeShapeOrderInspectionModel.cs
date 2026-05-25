namespace StatsClient.MVVM.Model;

public class ThreeShapeOrderInspectionModel
{
    public bool IsCaseWereDesigned { get; set; } = false;
    public bool IsLocked { get; set; } = false;
    public bool IsCheckedOut { get; set; } = false;
    public int PanNumber { get; set; }
    public string? CaseStatus { get; set; }
    public string? OriginalLockStatusID { get; set; }
    public string? ModelJobIDForLock { get; set; }
}
