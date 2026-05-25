using System.Windows.Media;

namespace StatsClient.MVVM.Model;

public class PanColorModel
{
    public Brush? Color { get; set; }
    public string? RgbColor { get; set; }
    public string? FriendlyName { get; set; }
    public bool IsItDarkColor { get; set; } = false;
}
