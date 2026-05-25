using System.Windows;

namespace StatsClient.MVVM.Model;

public class MainMenuItemModel
{
    public string? Icon { get; set; }
    public string? Header { get; set; }
    public string? Command { get; set; }
    public Visibility Visible { get; set; } = Visibility.Visible;
}
