using StatsClient.MVVM.Model;

namespace StatsClient.MVVM.Core;

internal static class PanStackVisionColumnMerger
{
    /// <summary>Maps label X from crop space (0–1) to full-image normalized X.</summary>
    public static void RemapToFullImage(IEnumerable<PanStackVisionColumnData> columns, double x0, double x1)
    {
        var span = x1 - x0;
        if (span <= 0)
        {
            return;
        }

        foreach (var column in columns)
        {
            foreach (var label in column.Labels)
            {
                if (label.CenterX.HasValue)
                {
                    label.CenterX = Math.Clamp(x0 + label.CenterX.Value * span, 0, 1);
                }
            }
        }
    }

    /// <summary>
    /// Vision models sometimes return Y increasing upward (0 = bottom). Convert to top-left image space.
    /// </summary>
    public static void NormalizeSectionYAxis(IEnumerable<PanStackVisionColumnData> columns)
    {
        var labels = columns.SelectMany(column => column.Labels)
            .Where(label => label.CenterY.HasValue)
            .ToList();

        if (labels.Count < 3)
        {
            return;
        }

        var invert = labels.Any(label => label.CenterY < -0.001);

        foreach (var label in labels)
        {
            label.CenterY = Math.Clamp(label.CenterY!.Value, 0, 1);
        }

        CorrectSectionRowOutliers(columns);

        if (!invert)
        {
            invert = SectionYAxisIsInverted(labels);
        }

        if (!invert)
        {
            return;
        }

        foreach (var label in labels)
        {
            label.CenterY = 1.0 - label.CenterY!.Value;
        }
    }

    private static bool SectionYAxisIsInverted(List<PanStackVisionCell> labels)
    {
        var bands = FindHorizontalBands(labels, tolerance: 0.055);
        if (bands.Count < 2)
        {
            return false;
        }

        // Only flip when Y clearly increases upward (section 1): bottom near 0, top below ~0.85, nothing at image bottom.
        var maxY = labels.Max(label => label.CenterY!.Value);
        if (maxY > 0.88)
        {
            return false;
        }

        bands.Sort((left, right) => left.AvgY.CompareTo(right.AvgY));
        var lowest = bands[0];
        var highest = bands[^1];

        return lowest.AvgY < 0.08
               && highest.AvgY > 0.45
               && highest.AvgY < 0.88
               && lowest.Labels.Count >= 3
               && highest.Labels.Count >= 3;
    }

    /// <summary>
    /// Model sometimes puts one horizontal row at y≈0 while other rows use normal spacing (e.g. 8027 at 0.04).
    /// </summary>
    public static void CorrectSectionRowOutliers(IEnumerable<PanStackVisionColumnData> columns)
    {
        var labels = columns.SelectMany(column => column.Labels)
            .Where(label => label.CenterY.HasValue)
            .ToList();

        if (labels.Count < 6)
        {
            return;
        }

        var bands = FindHorizontalBands(labels, tolerance: 0.055)
            .OrderBy(band => band.AvgY)
            .ToList();

        if (bands.Count < 4)
        {
            return;
        }

        var lowest = bands[0];
        var second = bands[1];
        var bottom = bands[^1];

        if (lowest.AvgY >= 0.10 || second.AvgY <= 0.12 || bottom.AvgY <= 0.88)
        {
            return;
        }

        var correctedY = bands.Count >= 2
            ? (bands[^2].AvgY + bottom.AvgY) / 2.0
            : bottom.AvgY - 0.08;

        correctedY = Math.Clamp(correctedY, second.AvgY + 0.04, bottom.AvgY - 0.04);

        foreach (var label in lowest.Labels)
        {
            label.CenterY = correctedY;
        }
    }

    private static List<(double AvgY, List<PanStackVisionCell> Labels)> FindHorizontalBands(
        List<PanStackVisionCell> labels,
        double tolerance)
    {
        var bands = new List<(double AvgY, List<PanStackVisionCell> Labels)>();
        foreach (var label in labels.OrderBy(label => label.CenterY))
        {
            if (bands.Count == 0)
            {
                bands.Add((label.CenterY!.Value, [label]));
                continue;
            }

            var last = bands[^1];
            if (Math.Abs(label.CenterY!.Value - last.AvgY) <= tolerance)
            {
                last.Labels.Add(label);
                bands[^1] = (last.Labels.Average(l => l.CenterY!.Value), last.Labels);
            }
            else
            {
                bands.Add((label.CenterY!.Value, [label]));
            }
        }

        return bands.Where(band => band.Labels.Count >= 2).ToList();
    }

    /// <summary>Stores vision coordinates for overlay drawing before grid organizer mutates CenterX/CenterY.</summary>
    public static void CaptureOverlayCoordinates(IEnumerable<PanStackVisionColumnData> columns)
    {
        foreach (var label in columns.SelectMany(column => column.Labels))
        {
            if (label.OverlayCenterX.HasValue && label.OverlayCenterY.HasValue)
            {
                continue;
            }

            if (label.CenterX.HasValue)
            {
                label.OverlayCenterX = NormalizeCoord(label.CenterX.Value);
            }

            if (label.CenterY.HasValue)
            {
                label.OverlayCenterY = NormalizeCoord(label.CenterY.Value);
            }
        }
    }

    private static double NormalizeCoord(double value)
    {
        if (value > 1.01)
        {
            value = value > 100 ? value / 1000.0 : value / 100.0;
        }

        return Math.Clamp(value, 0, 1);
    }

    /// <summary>Merges duplicate columns from overlapping crops; renumbers columnIndex left → right.</summary>
    public static List<PanStackVisionColumnData> MergeOverlappingColumns(List<PanStackVisionColumnData> columns)
    {
        if (columns.Count <= 1)
        {
            RenumberColumns(columns);
            return columns;
        }

        var sorted = columns
            .Select(column => (Column: column, AvgX: AverageCenterX(column)))
            .OrderBy(pair => pair.AvgX)
            .ToList();

        var merged = new List<PanStackVisionColumnData>();
        PanStackVisionColumnData? current = null;
        double currentAvgX = 0;

        foreach (var (column, avgX) in sorted)
        {
            if (current is null)
            {
                current = column;
                currentAvgX = avgX;
                continue;
            }

            if (Math.Abs(avgX - currentAvgX) < 0.04)
            {
                MergeLabels(current, column);
                currentAvgX = AverageCenterX(current);
            }
            else
            {
                merged.Add(current);
                current = column;
                currentAvgX = avgX;
            }
        }

        if (current is not null)
        {
            merged.Add(current);
        }

        RenumberColumns(merged);
        return merged;
    }

    private static void MergeLabels(PanStackVisionColumnData target, PanStackVisionColumnData other)
    {
        foreach (var label in other.Labels)
        {
            var existing = target.Labels.Find(l => l.Number == label.Number);
            if (existing is null)
            {
                target.Labels.Add(label);
                continue;
            }

            if (label.Confidence > existing.Confidence)
            {
                existing.Confidence = label.Confidence;
                existing.CenterX = label.CenterX;
                existing.CenterY = label.CenterY;
                existing.OverlayCenterX = label.OverlayCenterX;
                existing.OverlayCenterY = label.OverlayCenterY;
            }
        }

        target.Labels.Sort((a, b) => (a.CenterY ?? 0).CompareTo(b.CenterY ?? 0));
    }

    private static double AverageCenterX(PanStackVisionColumnData column)
    {
        var values = column.Labels.Where(label => label.CenterX.HasValue).Select(label => label.CenterX!.Value).ToList();
        return values.Count == 0 ? 0.5 : values.Average();
    }

    private static void RenumberColumns(List<PanStackVisionColumnData> columns)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            columns[i].ColumnIndex = i + 1;
        }
    }
}
