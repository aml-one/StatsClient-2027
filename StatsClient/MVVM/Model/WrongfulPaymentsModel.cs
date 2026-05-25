namespace StatsClient.MVVM.Model;

public class WrongfulPaymentsModel
{
    public string? PaidDesigner { get; set; }
    public string? DidNotGetPaidDesigner { get; set; }
    public int PaidCases { get; set; } = 0;
    public int PaidUnits { get; set; } = 0;
    public int PaidAmount { get; set; } = 0;
}
