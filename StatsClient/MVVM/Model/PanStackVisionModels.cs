namespace StatsClient.MVVM.Model;

public sealed class PanStackVisionCell
{
    public string Number { get; set; } = string.Empty;
    public double Confidence { get; set; }
    /// <summary>Label center X, 0–1 relative to image width.</summary>
    public double? CenterX { get; set; }
    /// <summary>Label center Y, 0–1 relative to image height.</summary>
    public double? CenterY { get; set; }
    /// <summary>Original vision X for drawing on the photo (not modified by grid organizer).</summary>
    public double? OverlayCenterX { get; set; }
    /// <summary>Original vision Y for drawing on the photo (not modified by grid organizer).</summary>
    public double? OverlayCenterY { get; set; }
    /// <summary>0-based matrix column after spatial alignment (left = 0).</summary>
    public int? GridColumn { get; set; }
    /// <summary>0-based matrix row after spatial alignment (top = 0).</summary>
    public int? RowIndex { get; set; }
}

public sealed class PanStackVisionColumnData
{
    public int ColumnIndex { get; set; }
    public List<PanStackVisionCell> Labels { get; set; } = [];
}
