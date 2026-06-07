using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>
/// Morphs the library crown collar toward the user margin loop before connect (stitch / snap).
/// </summary>
internal static class StatsDesignCrownStitcher
{
    public static MeshSnapshot SnapCollarToUserMargin(
        MeshSnapshot libraryMesh,
        IReadOnlyList<Point3D> userMarginLoop,
        Vector3D insertionAxis,
        double snapStrength = 0.78,
        int smoothPasses = 2)
    {
        ArgumentNullException.ThrowIfNull(libraryMesh);
        if (userMarginLoop.Count < 3)
        {
            return libraryMesh;
        }

        var axis = insertionAxis;
        if (axis.LengthSquared < 1e-12)
        {
            axis = new Vector3D(0, 0, 1);
        }
        else
        {
            axis.Normalize();
        }

        var positions = libraryMesh.Positions.ToArray();
        var indices = libraryMesh.TriangleIndices;
        var collar = IdentifyCollarVertexIndices(positions, axis);
        if (collar.Count == 0)
        {
            return libraryMesh;
        }

        var marginDense = StatsDesignMarginSpline.DensifyClosed(userMarginLoop, targetSpacingMm: 0.25);
        var neighbors = BuildAdjacency(positions.Length, indices);

        foreach (var index in collar)
        {
            var current = positions[index];
            var target = ClosestPointOnPolyline(current, marginDense);
            var blend = Math.Clamp(snapStrength, 0.0, 1.0);
            positions[index] = new Point3D(
                current.X + ((target.X - current.X) * blend),
                current.Y + ((target.Y - current.Y) * blend),
                current.Z + ((target.Z - current.Z) * blend));
        }

        for (var pass = 0; pass < smoothPasses; pass++)
        {
            SmoothCollarRing(positions, neighbors, collar, strength: 0.45);
        }

        return new MeshSnapshot(positions, indices);
    }

    private static HashSet<int> IdentifyCollarVertexIndices(Point3D[] positions, Vector3D insertionAxis)
    {
        var projections = new double[positions.Length];
        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        for (var i = 0; i < positions.Length; i++)
        {
            var t = Vector3D.DotProduct(new Vector3D(positions[i].X, positions[i].Y, positions[i].Z), insertionAxis);
            projections[i] = t;
            min = Math.Min(min, t);
            max = Math.Max(max, t);
        }

        var range = max - min;
        if (range < 1e-6)
        {
            return new HashSet<int>();
        }

        var threshold = min + (range * 0.22);
        var collar = new HashSet<int>();
        for (var i = 0; i < positions.Length; i++)
        {
            if (projections[i] <= threshold)
            {
                collar.Add(i);
            }
        }

        return collar;
    }

    private static void SmoothCollarRing(
        Point3D[] positions,
        List<HashSet<int>> neighbors,
        HashSet<int> collar,
        double strength)
    {
        var next = new Point3D[positions.Length];
        Array.Copy(positions, next, positions.Length);

        foreach (var index in collar)
        {
            var nbrs = neighbors[index];
            if (nbrs.Count == 0)
            {
                continue;
            }

            var avgX = 0.0;
            var avgY = 0.0;
            var avgZ = 0.0;
            var count = 0;
            foreach (var j in nbrs)
            {
                if (!collar.Contains(j))
                {
                    continue;
                }

                avgX += positions[j].X;
                avgY += positions[j].Y;
                avgZ += positions[j].Z;
                count++;
            }

            if (count == 0)
            {
                continue;
            }

            avgX /= count;
            avgY /= count;
            avgZ /= count;
            next[index] = new Point3D(
                positions[index].X + (strength * (avgX - positions[index].X)),
                positions[index].Y + (strength * (avgY - positions[index].Y)),
                positions[index].Z + (strength * (avgZ - positions[index].Z)));
        }

        foreach (var index in collar)
        {
            positions[index] = next[index];
        }
    }

    private static List<HashSet<int>> BuildAdjacency(int vertexCount, int[] indices)
    {
        var neighbors = new List<HashSet<int>>(vertexCount);
        for (var i = 0; i < vertexCount; i++)
        {
            neighbors.Add(new HashSet<int>());
        }

        for (var i = 0; i + 2 < indices.Length; i += 3)
        {
            var i0 = indices[i];
            var i1 = indices[i + 1];
            var i2 = indices[i + 2];
            if (i0 < 0 || i1 < 0 || i2 < 0 || i0 >= vertexCount || i1 >= vertexCount || i2 >= vertexCount)
            {
                continue;
            }

            neighbors[i0].Add(i1);
            neighbors[i0].Add(i2);
            neighbors[i1].Add(i0);
            neighbors[i1].Add(i2);
            neighbors[i2].Add(i0);
            neighbors[i2].Add(i1);
        }

        return neighbors;
    }

    private static Point3D ClosestPointOnPolyline(Point3D point, IReadOnlyList<Point3D> polyline)
    {
        var best = polyline[0];
        var bestDist = double.PositiveInfinity;
        for (var i = 0; i < polyline.Count; i++)
        {
            var j = (i + 1) % polyline.Count;
            var closest = ClosestPointOnSegment(point, polyline[i], polyline[j]);
            var dx = point.X - closest.X;
            var dy = point.Y - closest.Y;
            var dz = point.Z - closest.Z;
            var dist = (dx * dx) + (dy * dy) + (dz * dz);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = closest;
            }
        }

        return best;
    }

    private static Point3D ClosestPointOnSegment(Point3D point, Point3D a, Point3D b)
    {
        var ab = b - a;
        var lengthSq = ab.LengthSquared;
        if (lengthSq < 1e-12)
        {
            return a;
        }

        var t = Math.Clamp(Vector3D.DotProduct(point - a, ab) / lengthSq, 0.0, 1.0);
        return a + (ab * t);
    }
}
