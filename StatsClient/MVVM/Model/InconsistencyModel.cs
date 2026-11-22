namespace StatsClient.MVVM.Model;

public class InconsistencyModel
{
    public string? OrderID { get; set; } = "";
    public string? PanNumber { get; set; } = "";
    public string? DigiPanNumber { get; set; } = "";
    public string? Patient_FirstName { get; set; } = "";
    public string? Patient_LastName { get; set; } = "";
    public bool Ignored { get; set; } = false;
    public bool HasLabnextMatch { get; set; } = false;
    public string? LabnextMatchCaseId { get; set; } = "";
}
