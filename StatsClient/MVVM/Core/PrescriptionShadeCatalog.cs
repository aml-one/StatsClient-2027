using StatsClient.MVVM.Model;
using System.Windows.Media;

namespace StatsClient.MVVM.Core;

public static class PrescriptionShadeCatalog
{
    public static IReadOnlyList<PrescriptionShadeOption> All { get; } =
    [
        ..Group("Bleach", "040", "030", "020", "010"),
        ..Group("0M", "0M4", "0M3", "0M2", "0M1"),
        ..Group("BL", "BL4", "BL3", "BL2", "BL1"),
        ..Group("VITA A", "A4", "A35", "A3", "A2", "A1"),
        ..Group("VITA B", "B4", "B3", "B2", "B1"),
        ..Group("VITA C", "C4", "C3", "C2", "C1"),
        ..Group("VITA D", "D4", "D3", "D2"),
        ..Group("Special", "WAXUP", "NG", "GOLD"),
        ..Group("1M", "1M1", "1M2"),
        ..Group("2M", "2M1", "2M2", "2M3"),
        ..Group("3M", "3M1", "3M2", "3M3"),
        ..Group("4M", "4M1", "4M2", "4M3"),
        ..Group("5M", "5M1", "5M2", "5M3"),
        ..Group("2L", "2L15", "2L25"),
        ..Group("3L", "3L15", "3L25"),
        ..Group("4L", "4L15", "4L25"),
        ..Group("2R", "2R15", "2R25"),
        ..Group("3R", "3R15", "3R25"),
        ..Group("4R", "4R15", "4R25"),
    ];

    public static string? NormalizeExtractedShade(string? shade)
    {
        if (string.IsNullOrWhiteSpace(shade))
        {
            return null;
        }

        string trimmed = shade.Trim().ToUpperInvariant();
        trimmed = trimmed.Replace("A3.5", "A35", StringComparison.OrdinalIgnoreCase);
        trimmed = trimmed.Replace(" ", "");
        trimmed = trimmed.Replace("-", "");

        if (All.Any(s => string.Equals(s.Code, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return All.First(s => string.Equals(s.Code, trimmed, StringComparison.OrdinalIgnoreCase)).Code;
        }

        foreach (PrescriptionShadeOption option in All)
        {
            if (trimmed.Contains(option.Code, StringComparison.OrdinalIgnoreCase))
            {
                return option.Code;
            }
        }

        return null;
    }

    private static IEnumerable<PrescriptionShadeOption> Group(string groupName, params string[] codes)
    {
        for (int i = 0; i < codes.Length; i++)
        {
            string code = codes[i];
            string display = code switch
            {
                "A35" => "A3.5",
                "2L15" => "2L 1.5",
                "2L25" => "2L 2.5",
                "3L15" => "3L 1.5",
                "3L25" => "3L 2.5",
                "4L15" => "4L 1.5",
                "4L25" => "4L 2.5",
                "2R15" => "2R 1.5",
                "2R25" => "2R 2.5",
                "3R15" => "3R 1.5",
                "3R25" => "3R 2.5",
                "4R15" => "4R 1.5",
                "4R25" => "4R 2.5",
                "WAXUP" => "WAX UP",
                "NG" => "NIGHT GUARD",
                _ => code,
            };

            Color swatchColor = PrescriptionShadeColors.Resolve(groupName, i, codes.Length, code);
            yield return new PrescriptionShadeOption(groupName, code, display, swatchColor);
        }
    }
}
