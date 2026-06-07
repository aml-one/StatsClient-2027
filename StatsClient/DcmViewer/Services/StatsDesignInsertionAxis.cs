using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>
/// Crown insertion direction (path of draw / least-undercut axis), perpendicular to the margin plane.
/// </summary>
internal static class StatsDesignInsertionAxis
{
    /// <summary>
    /// Average normal of the margin loop plane. Optional <paramref name="occlusalHint"/> flips the axis
    /// so the crown draws toward the tooth crown (away from gingiva).
    /// </summary>
    public static Vector3D Calculate(
        IReadOnlyList<Point3D> marginLoop,
        Point3D? occlusalHint = null)
    {
        if (marginLoop.Count < 3)
        {
            return new Vector3D(0, 0, 1);
        }

        var normal = ComputePlaneNormal(marginLoop);
        if (occlusalHint is not null)
        {
            var centroid = ComputeCentroid(marginLoop);
            var towardOcclusal = occlusalHint.Value - centroid;
            if (towardOcclusal.LengthSquared > 1e-12 &&
                Vector3D.DotProduct(normal, towardOcclusal) < 0.0)
            {
                normal = new Vector3D(-normal.X, -normal.Y, -normal.Z);
            }
        }

        normal.Normalize();
        return normal;
    }

    /// <summary>
    /// Estimates a point on the occlusal side of the prep using scan vertices inside the margin.
    /// </summary>
    public static Point3D? EstimateOcclusalHint(
        IReadOnlyList<Point3D> marginLoop,
        MeshSnapshot prepMesh)
    {
        if (marginLoop.Count < 3 || prepMesh.Positions.Length == 0)
        {
            return null;
        }

        var margin = StatsDesignMarginGeometry.Create(marginLoop);
        var axis = Calculate(marginLoop);
        var centroid = margin.Centroid;

        var bestScore = double.NegativeInfinity;
        Point3D? best = null;

        foreach (var p in prepMesh.Positions)
        {
            if (!margin.ContainsPoint(p))
            {
                continue;
            }

            var radial = p - centroid;
            var score = Vector3D.DotProduct(radial, axis);
            if (score > bestScore)
            {
                bestScore = score;
                best = p;
            }
        }

        return best;
    }

    private static Vector3D ComputePlaneNormal(IReadOnlyList<Point3D> marginLoop)
    {
        var sum = new Vector3D();
        var count = 0;
        for (var i = 0; i < marginLoop.Count; i++)
        {
            var p0 = marginLoop[i];
            var p1 = marginLoop[(i + 1) % marginLoop.Count];
            var p2 = marginLoop[(i + 2) % marginLoop.Count];
            var v1 = p1 - p0;
            var v2 = p2 - p0;
            var cross = Vector3D.CrossProduct(v1, v2);
            if (cross.LengthSquared < 1e-12)
            {
                continue;
            }

            cross.Normalize();
            sum += cross;
            count++;
        }

        if (count == 0 || sum.LengthSquared < 1e-12)
        {
            return new Vector3D(0, 0, 1);
        }

        sum.Normalize();
        return sum;
    }

    private static Point3D ComputeCentroid(IReadOnlyList<Point3D> marginLoop) =>
        new(
            marginLoop.Average(p => p.X),
            marginLoop.Average(p => p.Y),
            marginLoop.Average(p => p.Z));
}
