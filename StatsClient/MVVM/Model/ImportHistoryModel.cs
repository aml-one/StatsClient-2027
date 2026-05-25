namespace StatsClient.MVVM.Model;

public class ImportHistoryModel
{
    public string? OrderID { get; set; }
    public string? DesignerID { get; set; }
    public string? FriendlyName { get; set; }
    public string? ImportPath { get; set; }
    public string? DateTime { get; set; }
    public string? ImportTime { get; set; }
    public string? Event { get; set; }
    public string? OrderBy { get; set; }
    public string? Age { get; set; }
    public double? AgeInSeconds { get; set; } = 100;
    public double? Multiplier { get; set; }
}
