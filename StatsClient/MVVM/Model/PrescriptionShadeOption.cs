using System.Windows.Media;

namespace StatsClient.MVVM.Model;

public sealed class PrescriptionShadeOption
{
    public PrescriptionShadeOption(string group, string code, string display, Color swatchColor)
    {
        Group = group;
        Code = code;
        Display = display;
        SwatchColor = swatchColor;
        SwatchBrush = new SolidColorBrush(swatchColor);
        SwatchBrush.Freeze();
    }

    public string Group { get; }

    public string Code { get; }

    public string Display { get; }

    public Color SwatchColor { get; }

    public Brush SwatchBrush { get; }
}
