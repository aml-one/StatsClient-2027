using StatsClient.MVVM.Model;

namespace StatsClient.MVVM.Core;

/// <summary>
/// Snaps vision labels to a row/column grid using normalized centerX/centerY.
/// </summary>
internal static class PanStackVisionColumnOrganizer
{
    private const double DedupePositionEpsilon = 0.028;

    public static List<PanStackVisionColumnData> Organize(IEnumerable<PanStackVisionColumnData> rawColumns)
    {
        var labels = rawColumns.SelectMany(column => column.Labels).ToList();
        if (labels.Count == 0)
        {
            return rawColumns.ToList();
        }

        PanStackVisionColumnMerger.CaptureOverlayCoordinates(rawColumns);
        labels = DedupeSpatial(labels);

        var positioned = labels
            .Where(label => OverlayX(label).HasValue && OverlayY(label).HasValue)
            .ToList();

        if (positioned.Count < 2)
        {
            return FallbackColumns(labels);
        }

        var columns = SnapLabelsToGrid(positioned);
        RefineOverlayFromGrid(columns);
        RenumberColumns(columns);
        return columns;
    }

    /// <summary>Place green tags from snapped matrix cell (row 0 = top), spread within label bounding box.</summary>
    private static void RefineOverlayFromGrid(List<PanStackVisionColumnData> columns)
    {
        var labels = columns.SelectMany(column => column.Labels)
            .Where(label => label.GridColumn.HasValue && label.RowIndex.HasValue)
            .ToList();

        if (labels.Count == 0)
        {
            return;
        }

        var maxCol = labels.Max(label => label.GridColumn!.Value);
        var maxRow = labels.Max(label => label.RowIndex!.Value);

        var xs = labels.Select(label => SnapX(label)).ToList();
        var minX = xs.Min();
        var maxX = xs.Max();
        if (maxX - minX < 0.05)
        {
            minX = 0.05;
            maxX = 0.95;
        }

        var ys = labels.Select(label => SnapY(label)).OrderBy(y => y).ToList();
        var yPad = Math.Max(0.02, (ys[^1] - ys[0]) * 0.05);
        var yMin = Math.Max(0.05, ys[0] - yPad);
        var yMax = Math.Min(0.98, ys[^1] + yPad);
        if (yMax - yMin < 0.08)
        {
            yMin = 0.12;
            yMax = 0.92;
        }

        foreach (var label in labels)
        {
            var col = label.GridColumn!.Value;
            var row = label.RowIndex!.Value;

            label.OverlayCenterX = maxCol > 0
                ? minX + (col + 0.5) / (maxCol + 1) * (maxX - minX)
                : (minX + maxX) / 2;

            label.OverlayCenterY = maxRow > 0
                ? yMin + (row + 0.5) / (maxRow + 1) * (yMax - yMin)
                : (yMin + yMax) / 2;
        }
    }

    private static double? OverlayX(PanStackVisionCell label) =>
        label.OverlayCenterX ?? label.CenterX;

    private static double? OverlayY(PanStackVisionCell label) =>
        label.OverlayCenterY ?? label.CenterY;

    private static double SnapX(PanStackVisionCell label) => OverlayX(label)!.Value;
    private static double SnapY(PanStackVisionCell label) => OverlayY(label)!.Value;

    private static List<PanStackVisionColumnData> SnapLabelsToGrid(List<PanStackVisionCell> labels)
    {
        var columnCount = EstimateGroupCount(labels.Select(SnapX).ToList(), labels.Count);
        var rowCount = EstimateGroupCount(labels.Select(SnapY).ToList(), labels.Count);

        if (labels.Count >= 36)
        {
            columnCount = Math.Max(columnCount, 8);
            rowCount = Math.Max(rowCount, 6);
        }
        else if (labels.Count >= 28)
        {
            columnCount = Math.Max(columnCount, 7);
            rowCount = Math.Max(rowCount, 5);
        }

        var xCentroids = GetSortedCentroids(labels, columnCount, SnapX);
        var yCentroids = GetSortedCentroids(labels, rowCount, SnapY);

        var bestPerCell = new Dictionary<(int Col, int Row), PanStackVisionCell>();
        foreach (var label in labels)
        {
            var col = NearestCentroidIndex(SnapX(label), xCentroids);
            var row = NearestCentroidIndex(SnapY(label), yCentroids);
            var key = (col, row);

            if (!bestPerCell.TryGetValue(key, out var existing) || label.Confidence > existing.Confidence)
            {
                bestPerCell[key] = label;
            }
        }

        foreach (var (key, label) in bestPerCell)
        {
            label.GridColumn = key.Col;
            label.RowIndex = key.Row;
        }

        var maxCol = Math.Max(columnCount - 1, bestPerCell.Keys.DefaultIfEmpty().Max(pair => pair.Col));
        var maxRow = Math.Max(rowCount - 1, bestPerCell.Keys.DefaultIfEmpty().Max(pair => pair.Row));

        CorrectRowOrderIfInverted(bestPerCell, maxRow);

        var columns = new List<PanStackVisionColumnData>();
        for (var col = 0; col <= maxCol; col++)
        {
            var columnLabels = bestPerCell.Values
                .Where(label => label.GridColumn == col)
                .OrderBy(label => label.RowIndex)
                .ToList();

            if (columnLabels.Count == 0)
            {
                continue;
            }

            columns.Add(new PanStackVisionColumnData
            {
                ColumnIndex = col + 1,
                Labels = columnLabels
            });
        }

        return columns;
    }

    /// <summary>Row 0 must be the top of the stack (smallest overlay Y).</summary>
    private static void CorrectRowOrderIfInverted(
        Dictionary<(int Col, int Row), PanStackVisionCell> cells,
        int maxRow)
    {
        if (cells.Count == 0 || maxRow <= 0)
        {
            return;
        }

        double OverlayY(PanStackVisionCell label) => SnapY(label);

        var topRowAvg = cells.Where(pair => pair.Key.Row == 0)
            .Select(pair => OverlayY(pair.Value))
            .Where(value => !double.IsNaN(value))
            .DefaultIfEmpty(double.NaN)
            .Average();

        var bottomRowAvg = cells.Where(pair => pair.Key.Row == maxRow)
            .Select(pair => OverlayY(pair.Value))
            .Where(value => !double.IsNaN(value))
            .DefaultIfEmpty(double.NaN)
            .Average();

        if (double.IsNaN(topRowAvg) || double.IsNaN(bottomRowAvg) || topRowAvg <= bottomRowAvg)
        {
            return;
        }

        foreach (var label in cells.Values)
        {
            if (label.RowIndex is int row)
            {
                label.RowIndex = maxRow - row;
            }
        }
    }

    private static List<double> GetSortedCentroids(
        List<PanStackVisionCell> labels,
        int k,
        Func<PanStackVisionCell, double> coordinate)
    {
        k = Math.Clamp(k, 1, labels.Count);
        var buckets = ClusterByCoordinate(labels, k, coordinate);
        var values = labels.Select(coordinate).OrderBy(value => value).ToList();
        var fallbacks = Enumerable.Range(0, k)
            .Select(i => values[Math.Min(values.Count - 1, i * values.Count / k)])
            .ToArray();

        var centroids = new List<double>(k);
        for (var i = 0; i < k; i++)
        {
            centroids.Add(buckets[i].Count > 0 ? buckets[i].Average(coordinate) : fallbacks[i]);
        }

        return centroids.OrderBy(value => value).ToList();
    }

    private static int NearestCentroidIndex(double value, List<double> centroids)
    {
        if (centroids.Count == 0)
        {
            return 0;
        }

        var best = 0;
        var bestDist = double.MaxValue;
        for (var i = 0; i < centroids.Count; i++)
        {
            var dist = Math.Abs(value - centroids[i]);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }

        return best;
    }

    private static void NormalizeCoordinates(List<PanStackVisionCell> labels)
    {
        var positioned = labels.Where(label => label.CenterX.HasValue && label.CenterY.HasValue).ToList();
        if (positioned.Count == 0)
        {
            return;
        }

        var maxX = positioned.Max(label => label.CenterX!.Value);
        var maxY = positioned.Max(label => label.CenterY!.Value);

        if (maxX > 1.01)
        {
            foreach (var label in positioned)
            {
                label.CenterX = label.CenterX!.Value / maxX;
            }
        }

        if (maxY > 1.01)
        {
            foreach (var label in positioned)
            {
                label.CenterY = label.CenterY!.Value / maxY;
            }
        }

        foreach (var label in positioned)
        {
            label.CenterX = Math.Clamp(label.CenterX!.Value, 0, 1);
            label.CenterY = Math.Clamp(label.CenterY!.Value, 0, 1);
            label.RowIndex = null;
            label.GridColumn = null;
        }
    }

    private static void AlignColumnMajorAxes(List<PanStackVisionCell> labels)
    {
        if (ScoreVerticalStacks(labels, swapAxes: false) >= ScoreVerticalStacks(labels, swapAxes: true))
        {
            return;
        }

        foreach (var label in labels)
        {
            (label.CenterX, label.CenterY) = (label.CenterY, label.CenterX);
        }
    }

    private static double ScoreVerticalStacks(List<PanStackVisionCell> labels, bool swapAxes)
    {
        var columnCount = EstimateGroupCount(labels.Select(Coord).ToList(), labels.Count);
        var buckets = ClusterByCoordinate(labels, columnCount, swapAxes ? CoordY : CoordX);

        var stackScore = 0.0;
        var scored = 0;
        foreach (var bucket in buckets.Where(bucket => bucket.Count >= 2))
        {
            var colSpread = Spread(bucket, swapAxes ? CoordY : CoordX);
            var rowSpread = Spread(bucket, swapAxes ? CoordX : CoordY);
            stackScore += rowSpread - colSpread;
            scored++;
        }

        if (scored == 0)
        {
            return 0;
        }

        var colAxisSpan = Spread(labels, swapAxes ? CoordY : CoordX);
        var rowAxisSpan = Spread(labels, swapAxes ? CoordX : CoordY);
        var aspect = colAxisSpan / Math.Max(0.05, rowAxisSpan);

        return stackScore / scored + aspect * 0.15;
    }

    private static List<List<PanStackVisionCell>> ClusterByCoordinate(
        List<PanStackVisionCell> labels,
        int k,
        Func<PanStackVisionCell, double> coordinate)
    {
        k = Math.Clamp(k, 1, labels.Count);
        var values = labels.Select(coordinate).OrderBy(value => value).ToList();
        var centroids = Enumerable.Range(0, k)
            .Select(i => values[Math.Min(values.Count - 1, i * values.Count / k)])
            .ToArray();

        var assignments = new int[labels.Count];
        for (var iter = 0; iter < 12; iter++)
        {
            var moved = false;
            for (var i = 0; i < labels.Count; i++)
            {
                var value = coordinate(labels[i]);
                var best = 0;
                var bestDist = double.MaxValue;
                for (var c = 0; c < k; c++)
                {
                    var dist = Math.Abs(value - centroids[c]);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = c;
                    }
                }

                if (assignments[i] != best)
                {
                    moved = true;
                }

                assignments[i] = best;
            }

            for (var c = 0; c < k; c++)
            {
                var members = labels.Where((_, i) => assignments[i] == c).ToList();
                centroids[c] = members.Count > 0 ? members.Average(coordinate) : centroids[c];
            }

            if (!moved)
            {
                break;
            }
        }

        var buckets = Enumerable.Range(0, k).Select(_ => new List<PanStackVisionCell>()).ToList();
        for (var i = 0; i < labels.Count; i++)
        {
            buckets[assignments[i]].Add(labels[i]);
        }

        return buckets;
    }

    private static int EstimateGroupCount(List<double> coords, int labelCount)
    {
        if (coords.Count < 2)
        {
            return Math.Max(1, coords.Count);
        }

        coords.Sort();
        var span = Math.Max(0.05, coords[^1] - coords[0]);
        var gaps = coords.Zip(coords.Skip(1), (left, right) => right - left).ToList();
        var medianGap = gaps.OrderBy(gap => gap).ElementAt(gaps.Count / 2);
        var threshold = Math.Max(medianGap * 2.0, span / 16);
        var fromGaps = gaps.Count(gap => gap >= threshold) + 1;

        var fromCount = labelCount switch
        {
            >= 40 => 8,
            >= 30 => 7,
            >= 20 => 6,
            _ => (int)Math.Round(Math.Sqrt(labelCount))
        };

        return Math.Clamp(Math.Max(fromGaps, fromCount), 1, 12);
    }

    private static List<PanStackVisionCell> DedupeSpatial(List<PanStackVisionCell> labels)
    {
        var kept = new List<PanStackVisionCell>();
        foreach (var label in labels.OrderByDescending(label => label.Confidence))
        {
            if (kept.Any(existing => IsSameLabel(existing, label)))
            {
                continue;
            }

            kept.Add(label);
        }

        return kept;
    }

    private static bool IsSameLabel(PanStackVisionCell a, PanStackVisionCell b)
    {
        if (!string.Equals(a.Number, b.Number, StringComparison.Ordinal))
        {
            return false;
        }

        var ax = OverlayX(a);
        var ay = OverlayY(a);
        var bx = OverlayX(b);
        var by = OverlayY(b);

        if (!ax.HasValue || !bx.HasValue || !ay.HasValue || !by.HasValue)
        {
            return true;
        }

        return Math.Abs(ax.Value - bx.Value) < DedupePositionEpsilon
               && Math.Abs(ay.Value - by.Value) < DedupePositionEpsilon;
    }

    private static List<PanStackVisionColumnData> FallbackColumns(List<PanStackVisionCell> labels)
    {
        return
        [
            new PanStackVisionColumnData
            {
                ColumnIndex = 1,
                Labels = labels.OrderBy(SnapY).ToList()
            }
        ];
    }

    private static void RenumberColumns(List<PanStackVisionColumnData> columns)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            columns[i].ColumnIndex = i + 1;
        }
    }

    public static (int Columns, int Rows) GetGridDimensions(IEnumerable<PanStackVisionColumnData> columns)
    {
        var labels = columns.SelectMany(column => column.Labels).ToList();
        if (labels.Any(label => label.GridColumn.HasValue && label.RowIndex.HasValue))
        {
            var maxCol = labels.Max(label => label.GridColumn!.Value);
            var maxRow = labels.Max(label => label.RowIndex!.Value);
            var colCount = maxCol + 1;
            var rowCount = maxRow + 1;
            if (labels.Count >= 36)
            {
                colCount = Math.Max(colCount, 8);
                rowCount = Math.Max(rowCount, 6);
            }

            return (colCount, rowCount);
        }

        var columnList = columns.ToList();
        var fallbackColCount = Math.Max(1, columnList.Count);
        var fallbackRowCount = Math.Max(1, columnList.DefaultIfEmpty().Max(column => column?.Labels.Count ?? 0));

        if (labels.Count >= 36)
        {
            fallbackColCount = Math.Max(fallbackColCount, 8);
            fallbackRowCount = Math.Max(fallbackRowCount, 6);
        }

        return (fallbackColCount, fallbackRowCount);
    }

    private static double CoordX(PanStackVisionCell label) => label.CenterX!.Value;
    private static double CoordY(PanStackVisionCell label) => label.CenterY!.Value;
    private static double Coord(PanStackVisionCell label) => label.CenterX!.Value;

    private static double Spread(IEnumerable<PanStackVisionCell> labels, Func<PanStackVisionCell, double> coordinate)
    {
        var values = labels.Select(coordinate).ToList();
        if (values.Count == 0)
        {
            return 0;
        }

        return values.Max() - values.Min();
    }

    private static double StdDev(IEnumerable<double> values)
    {
        var list = values.ToList();
        if (list.Count == 0)
        {
            return 0;
        }

        var mean = list.Average();
        return Math.Sqrt(list.Average(value => (value - mean) * (value - mean)));
    }
}
