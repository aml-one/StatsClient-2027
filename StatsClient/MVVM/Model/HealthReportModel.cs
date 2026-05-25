namespace StatsClient.MVVM.Model;

public class HealthReportModel
{
    public string? TaskName { get; set; }
    public string? ServiceName { get; set; }
    public string? LastReport { get; set; }
    public string? OneBeforeLastReport { get; set; }
    public int? ExpectedDifference { get; set; }
    public int? ExpectedDifferenceNightTime { get; set; }
    public int? NightHoursStart { get; set; }
    public int? NightHoursEnd { get; set; }
    public string? CurrentTime { get; set; }
    public bool NoNightTime { get; set; } = false;

    public string ServiceStatus { get; set; } = "healthy";
    public string ForeColor { get; set; } = "LightGreen";
}
