using HelixToolkit.Maths;
using WpfColor = System.Windows.Media.Color;

namespace DCMViewer.Services;

internal static class ColorExtensions
{
    public static Color4 ToColor4(this WpfColor color, double opacity = 1.0)
    {
        var alpha = (float)Math.Clamp(color.A / 255.0 * opacity, 0.0, 1.0);
        return new Color4(color.R / 255f, color.G / 255f, color.B / 255f, alpha);
    }
}
