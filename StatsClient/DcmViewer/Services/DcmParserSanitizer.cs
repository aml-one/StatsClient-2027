using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>Removes unrealistic long-edge bridge triangles from marginal facet decode errors.</summary>
internal static class DcmParserSanitizer
{
    public static void TrySanitizeMeshConnectivity(List<Point3D> positions, List<int> triangleIndices)
    {
        var triangleCount = triangleIndices.Count / 3;
        if (triangleCount < 3 || positions.Count == 0)
        {
            return;
        }

        var longEdgeRatio = DcmParserDiagnostics.ComputeLongEdgeTriangleRatio(positions, triangleIndices);

        // Only prune when sampled edges show severe bridge artifacts from marginal facet decode (~15%+).
        if (longEdgeRatio >= 0.15)
        {
            PruneBridgeTriangles(positions, triangleIndices);
        }
    }

    public static double ComputeNeedleTriangleRatio(IReadOnlyList<Point3D> positions, IReadOnlyList<int> triangleIndices)
    {
        var triangleCount = triangleIndices.Count / 3;
        if (triangleCount == 0)
        {
            return 0.0;
        }

        var sampleCount = Math.Min(triangleCount, 8000);
        var stride = Math.Max(1, triangleCount / sampleCount);
        var needles = 0;
        var considered = 0;

        for (var triangleIndex = 0; triangleIndex < triangleCount; triangleIndex += stride)
        {
            if (IsNeedleTriangle(positions, triangleIndices, triangleIndex))
            {
                needles++;
            }

            considered++;
        }

        return considered == 0 ? 0.0 : (double)needles / considered;
    }

    private static bool IsNeedleTriangle(IReadOnlyList<Point3D> positions, IReadOnlyList<int> triangleIndices, int triangleIndex)
    {
        var i0 = triangleIndices[(triangleIndex * 3) + 0];
        var i1 = triangleIndices[(triangleIndex * 3) + 1];
        var i2 = triangleIndices[(triangleIndex * 3) + 2];

        if (i0 < 0 || i1 < 0 || i2 < 0 ||
            i0 >= positions.Count || i1 >= positions.Count || i2 >= positions.Count)
        {
            return true;
        }

        var p0 = positions[i0];
        var p1 = positions[i1];
        var p2 = positions[i2];
        var e01 = (p0 - p1).Length;
        var e12 = (p1 - p2).Length;
        var e20 = (p2 - p0).Length;
        var maxEdge = Math.Max(e01, Math.Max(e12, e20));
        var area2 = Vector3D.CrossProduct(p1 - p0, p2 - p0).LengthSquared;

        if (area2 < 1e-12)
        {
            return true;
        }

        var minEdge = Math.Min(e01, Math.Min(e12, e20));
        if (minEdge < 1e-6)
        {
            return true;
        }

        var aspectRatioSquared = (maxEdge * maxEdge) / area2;
        if (aspectRatioSquared > 120.0 && maxEdge / minEdge > 12.0)
        {
            return true;
        }

        return false;
    }

    public static void PruneBridgeTriangles(List<Point3D> positions, List<int> triangleIndices)
    {
        if (triangleIndices.Count < 9 || positions.Count == 0)
        {
            return;
        }

        var triangleCount = triangleIndices.Count / 3;
        var maxEdgeLengths = new double[triangleCount];

        for (var triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            var i0 = triangleIndices[(triangleIndex * 3) + 0];
            var i1 = triangleIndices[(triangleIndex * 3) + 1];
            var i2 = triangleIndices[(triangleIndex * 3) + 2];

            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= positions.Count || i1 >= positions.Count || i2 >= positions.Count)
            {
                maxEdgeLengths[triangleIndex] = double.PositiveInfinity;
                continue;
            }

            var p0 = positions[i0];
            var p1 = positions[i1];
            var p2 = positions[i2];

            var e01 = (p0 - p1).Length;
            var e12 = (p1 - p2).Length;
            var e20 = (p2 - p0).Length;
            maxEdgeLengths[triangleIndex] = Math.Max(e01, Math.Max(e12, e20));
        }

        var sorted = (double[])maxEdgeLengths.Clone();
        Array.Sort(sorted);

        var percentile50 = Percentile(sorted, 0.50);
        var percentile90 = Percentile(sorted, 0.90);
        if (!double.IsFinite(percentile50) || percentile50 <= 0)
        {
            return;
        }

        var bounds = ComputeBounds(positions);
        if (bounds == Rect3D.Empty)
        {
            return;
        }

        var diagonal = Math.Sqrt((bounds.SizeX * bounds.SizeX) + (bounds.SizeY * bounds.SizeY) + (bounds.SizeZ * bounds.SizeZ));
        if (!double.IsFinite(diagonal) || diagonal <= 0)
        {
            return;
        }

        // Match the long-edge detector (25% of diagonal) so we only drop true bridge artifacts.
        var threshold = Math.Min(
            Math.Min(percentile50 * 12.0, percentile90 * 3.5),
            diagonal * 0.25);

        if (!double.IsFinite(threshold) || threshold <= 0)
        {
            return;
        }

        var rebuilt = new List<int>(triangleIndices.Count);
        for (var triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            if (maxEdgeLengths[triangleIndex] <= threshold)
            {
                rebuilt.Add(triangleIndices[(triangleIndex * 3) + 0]);
                rebuilt.Add(triangleIndices[(triangleIndex * 3) + 1]);
                rebuilt.Add(triangleIndices[(triangleIndex * 3) + 2]);
            }
        }

        if (rebuilt.Count == 0)
        {
            return;
        }

        var removedTriangleCount = triangleCount - (rebuilt.Count / 3);
        if (removedTriangleCount <= 0)
        {
            return;
        }

        triangleIndices.Clear();
        triangleIndices.AddRange(rebuilt);
    }

    private static double Percentile(double[] sortedAscending, double percentile)
    {
        if (sortedAscending.Length == 0)
        {
            return double.NaN;
        }

        var index = (int)Math.Round((sortedAscending.Length - 1) * percentile);
        index = Math.Clamp(index, 0, sortedAscending.Length - 1);
        return sortedAscending[index];
    }

    private static Rect3D ComputeBounds(IReadOnlyList<Point3D> positions)
    {
        if (positions.Count == 0)
        {
            return Rect3D.Empty;
        }

        var minX = positions[0].X;
        var minY = positions[0].Y;
        var minZ = positions[0].Z;
        var maxX = minX;
        var maxY = minY;
        var maxZ = minZ;

        for (var i = 1; i < positions.Count; i++)
        {
            var point = positions[i];
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            minZ = Math.Min(minZ, point.Z);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
            maxZ = Math.Max(maxZ, point.Z);
        }

        return new Rect3D(minX, minY, minZ, maxX - minX, maxY - minY, maxZ - minZ);
    }
}
