namespace StatsClient.MVVM.Model;

public class InconsistencyModel
{
    public string? OrderID { get; set; } = "";
    public string? PanNumber { get; set; } = "";
    public bool Ignored { get; set; } = false;
}
