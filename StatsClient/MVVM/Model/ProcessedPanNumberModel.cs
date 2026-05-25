namespace StatsClient.MVVM.Model;

public class ProcessedPanNumberModel
{
    public string? Id { get; set; }
    public string? PanNumber { get; set; }
    public string? PostedTime { get; set; }
    public string? ProcessedTime { get; set; }
    public string? PostedBy { get; set; }
    public string? ProcessedBy { get; set; }
    public string? Comment { get; set; }
    public string? IsCollected { get; set; }
    public string? IsProcessed { get; set; }
    public string? PostedTimeForSorting { get; set; }
    public string? LineColor { get; set; } = "Black";
}
