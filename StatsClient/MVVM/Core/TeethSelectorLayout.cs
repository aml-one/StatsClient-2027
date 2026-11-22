namespace StatsClient.MVVM.Core;

public enum TeethSelectorCrownCategory
{
    Incisor,
    Canine,
    Premolar,
    Molar,
}

/// <summary>
/// Normalized crown hit regions for the vector teeth chart (1096×420).
/// </summary>
public sealed record TeethSelectorSlot(int ToothNumber, double CenterX, double CenterY, double Width, double Height)
{
    public double Left(double canvasWidth) => (CenterX * canvasWidth) - (Width * canvasWidth / 2);

    public double Top(double canvasHeight) => (CenterY * canvasHeight) - (Height * canvasHeight / 2);

    public double PixelWidth(double canvasWidth) => Width * canvasWidth;

    public double PixelHeight(double canvasHeight) => Height * canvasHeight;
}

public static class TeethSelectorLayout
{
    public const double ReferenceWidth = 1096;
    public const double ReferenceHeight = 420;

    public static IReadOnlyList<TeethSelectorSlot> AllSlots { get; } = BuildSlots();

    public static IReadOnlyList<int> UniversalOrder { get; } =
        [..Enumerable.Range(1, 16), ..Enumerable.Range(17, 16).Reverse()];

    private static IReadOnlyList<TeethSelectorSlot> BuildSlots()
    {
        var slots = new List<TeethSelectorSlot>(32);

        const double startX = 0.052;
        const double stepX = 0.0592;
        const double topY = 0.36;
        const double bottomY = 0.64;
        const double width = 0.049;
        const double height = 0.196;

        for (int i = 0; i < 16; i++)
        {
            int tooth = i + 1;
            double archFactor = Math.Abs(i - 7.5) / 7.5;
            double centerX = startX + (i * stepX);
            double centerY = topY - (archFactor * 0.022);
            (double widthScale, double heightScale) = GetSlotScale(tooth);
            slots.Add(new TeethSelectorSlot(tooth, centerX, centerY, width * widthScale, height * heightScale));
        }

        for (int i = 0; i < 16; i++)
        {
            int tooth = 32 - i;
            double archFactor = Math.Abs(i - 7.5) / 7.5;
            double centerX = startX + (i * stepX);
            double centerY = bottomY + (archFactor * 0.022);
            (double widthScale, double heightScale) = GetSlotScale(tooth);
            slots.Add(new TeethSelectorSlot(tooth, centerX, centerY, width * widthScale, height * heightScale));
        }

        return slots;
    }

    private static (double WidthScale, double HeightScale) GetSlotScale(int toothNumber) =>
        toothNumber switch
        {
            8 or 9 => (1.08, 1.0),
            6 or 11 => (1.06, 1.0),
            7 or 10 => (0.92, 1.0),
            23 or 24 or 25 or 26 => (0.90, 1.0),
            22 or 27 => (1.02, 1.0),
            _ => (1.0, 1.0),
        };

    public static string FormatSelection(IEnumerable<int> selected)
    {
        var set = selected.Where(n => n is >= 1 and <= 32).ToHashSet();
        if (set.Count == 0)
        {
            return string.Empty;
        }

        var ordered = new List<int>();
        foreach (int tooth in UniversalOrder)
        {
            if (set.Contains(tooth))
            {
                ordered.Add(tooth);
            }
        }

        return string.Join(",", ordered);
    }

    public static HashSet<int> ParseSelection(string? teethNumbers)
    {
        var set = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(teethNumbers))
        {
            return set;
        }

        foreach (string part in teethNumbers.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(part.Trim(), out int n) && n is >= 1 and <= 32)
            {
                set.Add(n);
            }
        }

        return set;
    }

    public static TeethSelectorCrownCategory ResolveCrownCategory(int toothNumber) =>
        toothNumber switch
        {
            7 or 8 or 9 or 10 or 23 or 24 or 25 or 26 => TeethSelectorCrownCategory.Incisor,
            6 or 11 or 22 or 27 => TeethSelectorCrownCategory.Canine,
            4 or 5 or 12 or 13 or 20 or 21 or 28 or 29 => TeethSelectorCrownCategory.Premolar,
            _ => TeethSelectorCrownCategory.Molar,
        };
}
