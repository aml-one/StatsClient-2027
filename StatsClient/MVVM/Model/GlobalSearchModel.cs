namespace StatsClient.MVVM.Model;

public class GlobalSearchModel
{
    public int Id { get; set; } = 0;
    public string? IntOrderId { get; set; }
    public string? PanNumber { get; set; }
    public string? Patient_FirstName { get; set; }
    public string? Patient_LastName { get; set; }
    public string? Customer { get; set; }
    public string? Items { get; set; }
    public string? UnitsFromItems { get; set; }
    public string? CreateDate { get; set; }
    public string? CreateDateLong { get; set; }
    public string? CreateYear { get; set; }
    public string? Designer { get; set; }
    public string? Icon { get; set; }
    public string? Background { get; set; }
    public string? Source { get; set; }
    public string? ReasonIsDead { get; set; } = "";
    public string? AddedToDatastore { get; set; } = "";
    public string? OrderFolder { get; set; } = "";
    public string? BaseFolder { get; set; } = "";
    public string? XMLFile { get; set; } = "";
}
