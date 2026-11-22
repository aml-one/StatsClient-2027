using System.Windows;

namespace StatsClient.MVVM.Model;

public class MainMenuItemModel
{
    public string? Icon { get; set; }
    /// <summary>Segoe MDL2 Assets glyph when set instead of <see cref="Icon"/> image.</summary>
    public string? IconGlyph { get; set; }
    public string? Header { get; set; }
    public string? Command { get; set; }
    public Visibility Visible { get; set; } = Visibility.Visible;
    public bool IsFooterItem { get; set; }
}
