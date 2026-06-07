using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>
/// Detects prep undercuts by collision sweep along the insertion axis inside the margin.
/// </summary>
internal static class StatsDesignUndercutAnalyzer
{
    public sealed record Result(
        bool HasUndercuts,
        double MaxUndercutDepthMm,
        int AffectedCellCount,
        IReadOnlyList<Point3D> Hotspots)
    {
        public string Summary =>
            HasUndercuts
                ? $"Undercuts detected — max depth {MaxUndercutDepthMm:0.00} mm ({AffectedCellCount} zones)."
                : "No significant undercuts along insertion axis.";
    }

    public static Result Analyze(
        MeshSnapshot prepMesh,
        IReadOnlyList<Point3D> marginLoop,
        Vector3D insertionAxis,
        double cellSizeMm = 0.4,
        double depthThresholdMm = 0.07)
    {
        if (prepMesh.Positions.Length == 0 || marginLoop.Count < 3)
        {
            return new Result(false, 0, 0, []);
        }

        var axis = insertionAxis;
        if (axis.LengthSquared < 1e-12)
        {
            axis = StatsDesignInsertionAxis.Calculate(marginLoop);
        }
        else
        {
            axis.Normalize();
        }

        var margin = StatsDesignMarginGeometry.Create(marginLoop, axis);
        var (axisU, axisV) = BuildPlaneBasis(axis);

        var cells = new Dictionary<(int Iu, int Iv), List<Point3D>>();

        foreach (var p in prepMesh.Positions)
        {
            if (!margin.ContainsPoint(p))
            {
                continue;
            }

            var delta = p - margin.Centroid;
            var u = Vector3D.DotProduct(delta, axisU);
            var v = Vector3D.DotProduct(delta, axisV);
            var key = ((int)Math.Floor(u / cellSizeMm), (int)Math.Floor(v / cellSizeMm));
            if (!cells.TryGetValue(key, out var list))
            {
                list = new List<Point3D>();
                cells[key] = list;
            }

            list.Add(p);
        }

        var maxDepth = 0.0;
        var affected = 0;
        var hotspots = new List<Point3D>();

        foreach (var (key, points) in cells)
        {
            if (points.Count < 4)
            {
                continue;
            }

            var projections = points
                .Select(p => Vector3D.DotProduct(p - margin.Centroid, axis))
                .OrderBy(t => t)
                .ToList();

            var depth = projections[^1] - projections[0];
            if (depth < depthThresholdMm)
            {
                continue;
            }

            if (CountLayerGaps(projections, depthThresholdMm * 0.5) < 1)
            {
                continue;
            }

            affected++;
            maxDepth = Math.Max(maxDepth, depth);
            var cellCenter = margin.Centroid +
                             (axisU * ((key.Iu + 0.5) * cellSizeMm)) +
                             (axisV * ((key.Iv + 0.5) * cellSizeMm));
            hotspots.Add(cellCenter);
        }

        if (hotspots.Count > 8)
        {
            hotspots = hotspots.Take(8).ToList();
        }

        return new Result(
            affected > 0,
            maxDepth,
            affected,
            hotspots);
    }

    private static int CountLayerGaps(List<double> sortedProjections, double gapMin)
    {
        var gaps = 0;
        for (var i = 1; i < sortedProjections.Count; i++)
        {
            if (sortedProjections[i] - sortedProjections[i - 1] > gapMin)
            {
                gaps++;
            }
        }

        return gaps;
    }

    private static (Vector3D U, Vector3D V) BuildPlaneBasis(Vector3D normal)
    {
        var axisU = Vector3D.CrossProduct(normal, new Vector3D(0, 0, 1));
        if (axisU.LengthSquared < 1e-10)
        {
            axisU = Vector3D.CrossProduct(normal, new Vector3D(0, 1, 0));
        }

        axisU.Normalize();
        var axisV = Vector3D.CrossProduct(normal, axisU);
        axisV.Normalize();
        return (axisU, axisV);
    }
}
