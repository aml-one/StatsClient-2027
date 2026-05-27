using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>Repairs triangle soups produced by marginal facet decode errors (long bridge triangles).</summary>
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
        var needleRatio = ComputeNeedleTriangleRatio(positions, triangleIndices);

        if (triangleCount < 500 && longEdgeRatio < 0.01 && needleRatio < 0.15)
        {
            return;
        }

        // Bridge triangles only when the soup has obvious long-edge artifacts (bird-nest cases).
        if (longEdgeRatio >= 0.02 || (triangleCount >= 10_000 && longEdgeRatio >= 0.008))
        {
            PruneBridgeTriangles(positions, triangleIndices);
        }

        // Needle cleanup only for severely corrupted facet decodes; mild ratios are valid crown tessellation.
        if (needleRatio >= 0.15)
        {
            PruneNeedleTriangles(positions, triangleIndices);
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

    public static void PruneNeedleTriangles(List<Point3D> positions, List<int> triangleIndices)
    {
        var triangleCount = triangleIndices.Count / 3;
        if (triangleCount == 0)
        {
            return;
        }

        var rebuilt = new List<int>(triangleIndices.Count);
        for (var triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            if (IsNeedleTriangle(positions, triangleIndices, triangleIndex))
            {
                continue;
            }

            rebuilt.Add(triangleIndices[(triangleIndex * 3) + 0]);
            rebuilt.Add(triangleIndices[(triangleIndex * 3) + 1]);
            rebuilt.Add(triangleIndices[(triangleIndex * 3) + 2]);
        }

        if (rebuilt.Count == 0 || rebuilt.Count == triangleIndices.Count)
        {
            return;
        }

        var removedTriangleCount = triangleCount - (rebuilt.Count / 3);
        if (removedTriangleCount > triangleCount * 0.25)
        {
            return;
        }

        triangleIndices.Clear();
        triangleIndices.AddRange(rebuilt);
        CompactUnusedVertices(positions, triangleIndices);
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

        // Typical dental tessellation stays well below ~12% of model diagonal per edge; bridge artifacts span much farther.
        var threshold = Math.Min(
            Math.Min(percentile50 * 12.0, percentile90 * 3.5),
            diagonal * 0.12);

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

    public static void KeepDominantConnectedComponents(List<Point3D> positions, List<int> triangleIndices)
    {
        var triangleCount = triangleIndices.Count / 3;
        if (triangleCount < 3)
        {
            return;
        }

        var labels = LabelTriangleComponents(triangleIndices, triangleCount);
        var componentSizes = new Dictionary<int, int>();
        for (var i = 0; i < labels.Length; i++)
        {
            var label = labels[i];
            componentSizes.TryGetValue(label, out var count);
            componentSizes[label] = count + 1;
        }

        if (componentSizes.Count <= 1)
        {
            return;
        }

        var ordered = componentSizes
            .OrderByDescending(pair => pair.Value)
            .ToList();

        var largest = ordered[0].Value;
        var keepLabels = new HashSet<int> { ordered[0].Key };
        var keptTriangles = largest;

        for (var i = 1; i < ordered.Count; i++)
        {
            var size = ordered[i].Value;
            if (size < Math.Max(500, triangleCount / 200))
            {
                break;
            }

            if (size >= largest * 0.05)
            {
                keepLabels.Add(ordered[i].Key);
                keptTriangles += size;
            }
        }

        if (keptTriangles >= triangleCount * 0.98)
        {
            return;
        }

        var rebuilt = new List<int>(triangleIndices.Count);
        for (var triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            if (!keepLabels.Contains(labels[triangleIndex]))
            {
                continue;
            }

            rebuilt.Add(triangleIndices[(triangleIndex * 3) + 0]);
            rebuilt.Add(triangleIndices[(triangleIndex * 3) + 1]);
            rebuilt.Add(triangleIndices[(triangleIndex * 3) + 2]);
        }

        if (rebuilt.Count == 0)
        {
            return;
        }

        triangleIndices.Clear();
        triangleIndices.AddRange(rebuilt);
        CompactUnusedVertices(positions, triangleIndices);
    }

    private static void CompactUnusedVertices(List<Point3D> positions, List<int> triangleIndices)
    {
        if (triangleIndices.Count == 0)
        {
            return;
        }

        var remap = new Dictionary<int, int>();
        var compacted = new List<Point3D>(positions.Count);

        foreach (var index in triangleIndices)
        {
            if (index < 0 || index >= positions.Count)
            {
                continue;
            }

            if (!remap.ContainsKey(index))
            {
                remap[index] = compacted.Count;
                compacted.Add(positions[index]);
            }
        }

        for (var i = 0; i < triangleIndices.Count; i++)
        {
            var oldIndex = triangleIndices[i];
            triangleIndices[i] = remap.TryGetValue(oldIndex, out var newIndex) ? newIndex : 0;
        }

        positions.Clear();
        positions.AddRange(compacted);
    }

    private static int[] LabelTriangleComponents(IReadOnlyList<int> triangleIndices, int triangleCount)
    {
        var edgeToTriangles = new Dictionary<(int A, int B), List<int>>();

        static (int, int) EdgeKey(int a, int b) => a < b ? (a, b) : (b, a);

        for (var triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            var i0 = triangleIndices[(triangleIndex * 3) + 0];
            var i1 = triangleIndices[(triangleIndex * 3) + 1];
            var i2 = triangleIndices[(triangleIndex * 3) + 2];

            AddEdge(edgeToTriangles, EdgeKey(i0, i1), triangleIndex);
            AddEdge(edgeToTriangles, EdgeKey(i1, i2), triangleIndex);
            AddEdge(edgeToTriangles, EdgeKey(i2, i0), triangleIndex);
        }

        var labels = new int[triangleCount];
        Array.Fill(labels, -1);
        var currentLabel = 0;

        for (var seed = 0; seed < triangleCount; seed++)
        {
            if (labels[seed] >= 0)
            {
                continue;
            }

            var queue = new Queue<int>();
            queue.Enqueue(seed);
            labels[seed] = currentLabel;

            while (queue.Count > 0)
            {
                var triangleIndex = queue.Dequeue();
                var i0 = triangleIndices[(triangleIndex * 3) + 0];
                var i1 = triangleIndices[(triangleIndex * 3) + 1];
                var i2 = triangleIndices[(triangleIndex * 3) + 2];

                foreach (var neighbor in EnumerateEdgeNeighbors(edgeToTriangles, i0, i1, i2))
                {
                    if (labels[neighbor] >= 0)
                    {
                        continue;
                    }

                    labels[neighbor] = currentLabel;
                    queue.Enqueue(neighbor);
                }
            }

            currentLabel++;
        }

        return labels;
    }

    private static IEnumerable<int> EnumerateEdgeNeighbors(
        Dictionary<(int A, int B), List<int>> edgeToTriangles,
        int i0,
        int i1,
        int i2)
    {
        static (int, int) EdgeKey(int a, int b) => a < b ? (a, b) : (b, a);

        foreach (var edge in new[] { EdgeKey(i0, i1), EdgeKey(i1, i2), EdgeKey(i2, i0) })
        {
            if (!edgeToTriangles.TryGetValue(edge, out var neighbors))
            {
                continue;
            }

            foreach (var neighbor in neighbors)
            {
                yield return neighbor;
            }
        }
    }

    private static void AddEdge(Dictionary<(int A, int B), List<int>> edgeToTriangles, (int A, int B) edge, int triangleIndex)
    {
        if (!edgeToTriangles.TryGetValue(edge, out var list))
        {
            list = [];
            edgeToTriangles[edge] = list;
        }

        list.Add(triangleIndex);
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
