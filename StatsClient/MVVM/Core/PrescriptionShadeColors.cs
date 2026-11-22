using System.Windows.Media;

namespace StatsClient.MVVM.Core;

public static class PrescriptionShadeColors
{
    private static readonly Dictionary<string, (Color Dark, Color Light)> GroupPalette = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Bleach"] = (Parse("#B7FCE2"), Parse("#F7FCE2")),
        ["0M"] = (Parse("#A7EAD2"), Parse("#D7FCE2")),
        ["BL"] = (Parse("#8ADCC8"), Parse("#B7FCE2")),
        ["VITA A"] = (Parse("#DCC860"), Parse("#F7DD74")),
        ["VITA B"] = (Parse("#E8D060"), Parse("#FEED74")),
        ["VITA C"] = (Parse("#F0E050"), Parse("#FDF979")),
        ["VITA D"] = (Parse("#F5EA70"), Parse("#FFFF94")),
        ["Special"] = (Parse("#C8C8C8"), Parse("#FDF979")),
        ["5M"] = (Parse("#5A9A90"), Parse("#77DDD4")),
        ["4M"] = (Parse("#7AAA9E"), Parse("#B2DDD4")),
        ["3M"] = (Parse("#92BAA8"), Parse("#C2DDD4")),
        ["2M"] = (Parse("#A8C4B4"), Parse("#D7DDD4")),
        ["1M"] = (Parse("#C8A8A8"), Parse("#F7CDD4")),
        ["2L"] = (Parse("#DCC860"), Parse("#F7DD74")),
        ["3L"] = (Parse("#E0D060"), Parse("#F7ED74")),
        ["4L"] = (Parse("#E8D860"), Parse("#F7FD74")),
        ["2R"] = (Parse("#C8C060"), Parse("#D7DD74")),
        ["3R"] = (Parse("#D0C868"), Parse("#D7ED74")),
        ["4R"] = (Parse("#D8D070"), Parse("#D7FD74")),
    };

    public static Color Resolve(string group, int index, int count, string code)
    {
        if (string.Equals(code, "GOLD", StringComparison.OrdinalIgnoreCase))
        {
            return Parse("#E3BD22");
        }

        if (string.Equals(code, "WAXUP", StringComparison.OrdinalIgnoreCase))
        {
            return Parse("#B7FCE2");
        }

        if (string.Equals(code, "NG", StringComparison.OrdinalIgnoreCase))
        {
            return Parse("#D7FCE2");
        }

        if (!GroupPalette.TryGetValue(group, out (Color Dark, Color Light) palette))
        {
            return Parse("#CCCCCC");
        }

        if (count <= 1)
        {
            return palette.Light;
        }

        double t = index / (double)(count - 1);
        return Lerp(palette.Dark, palette.Light, t);
    }

    private static Color Lerp(Color from, Color to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        byte A(byte a, byte b) => (byte)(a + ((b - a) * t));
        return Color.FromArgb(
            A(from.A, to.A),
            A(from.R, to.R),
            A(from.G, to.G),
            A(from.B, to.B));
    }

    private static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex)!;
}
