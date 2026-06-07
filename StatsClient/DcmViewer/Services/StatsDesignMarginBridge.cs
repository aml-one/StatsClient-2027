using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>
/// Builds ruled surfaces between two closed margin loops (library vs user-marked).
/// </summary>
internal static class StatsDesignMarginBridge
{
    public static MeshSnapshot BuildRuledBridge(
        IReadOnlyList<Point3D> libraryLoop,
        IReadOnlyList<Point3D> userLoop)
    {
        if (libraryLoop.Count < 3 || userLoop.Count < 3)
        {
            return new MeshSnapshot(Array.Empty<Point3D>(), Array.Empty<int>());
        }

        var sampleCount = Math.Clamp(
            Math.Max(libraryLoop.Count, userLoop.Count),
            48,
            256);

        var library = ResampleClosedLoop(libraryLoop, sampleCount);
        var user = ResampleClosedLoop(userLoop, sampleCount);
        AlignLoopWinding(library, user);

        var positions = new List<Point3D>(library.Count + user.Count);
        positions.AddRange(library);
        var userOffset = library.Count;
        positions.AddRange(user);

        var indices = new List<int>();
        for (var i = 0; i < sampleCount; i++)
        {
            var j = (i + 1) % sampleCount;
            var l0 = i;
            var l1 = j;
            var u0 = userOffset + i;
            var u1 = userOffset + j;
            indices.Add(l0);
            indices.Add(l1);
            indices.Add(u1);
            indices.Add(l0);
            indices.Add(u1);
            indices.Add(u0);
        }

        return new MeshSnapshot(positions.ToArray(), indices.ToArray());
    }

    public static MeshSnapshot BuildBoundaryToLoopRibbon(
        MeshSnapshot mesh,
        IReadOnlyList<Point3D> targetLoop)
    {
        var boundary = FindOpenBoundaryVertices(mesh);
        if (boundary.Count < 3 || targetLoop.Count < 3)
        {
            return new MeshSnapshot(Array.Empty<Point3D>(), Array.Empty<int>());
        }

        var sampleCount = Math.Clamp(targetLoop.Count, 32, 200);
        var loop = ResampleClosedLoop(targetLoop, sampleCount);

        var positions = new List<Point3D>();
        var indices = new List<int>();
        var boundaryMap = new Dictionary<int, int>();

        foreach (var vertexIndex in boundary)
        {
            boundaryMap[vertexIndex] = positions.Count;
            positions.Add(mesh.Positions[vertexIndex]);
        }

        var loopStart = positions.Count;
        positions.AddRange(loop);

        for (var i = 0; i < sampleCount; i++)
        {
            var j = (i + 1) % sampleCount;
            var boundaryA = FindNearestIndex(mesh.Positions, boundary, loop[i]);
            var boundaryB = FindNearestIndex(mesh.Positions, boundary, loop[j]);
            if (boundaryA < 0 || boundaryB < 0)
            {
                continue;
            }

            var ba = boundaryMap[boundaryA];
            var bb = boundaryMap[boundaryB];
            var la = loopStart + i;
            var lb = loopStart + j;
            indices.Add(ba);
            indices.Add(bb);
            indices.Add(lb);
            indices.Add(ba);
            indices.Add(lb);
            indices.Add(la);
        }

        return new MeshSnapshot(positions.ToArray(), indices.ToArray());
    }

    private static List<Point3D> ResampleClosedLoop(IReadOnlyList<Point3D> loop, int sampleCount)
    {
        var lengths = new double[loop.Count];
        var total = 0.0;
        for (var i = 0; i < loop.Count; i++)
        {
            var j = (i + 1) % loop.Count;
            var len = (loop[j] - loop[i]).Length;
            lengths[i] = len;
            total += len;
        }

        if (total < 1e-9)
        {
            return loop.ToList();
        }

        var result = new List<Point3D>(sampleCount);
        var step = total / sampleCount;
        var edgeIndex = 0;
        var edgeStart = 0.0;

        for (var sample = 0; sample < sampleCount; sample++)
        {
            var target = sample * step;
            while (edgeStart + lengths[edgeIndex] < target && edgeIndex < loop.Count)
            {
                edgeStart += lengths[edgeIndex];
                edgeIndex = (edgeIndex + 1) % loop.Count;
            }

            var edgeLen = lengths[edgeIndex];
            var t = edgeLen > 1e-9 ? (target - edgeStart) / edgeLen : 0.0;
            t = Math.Clamp(t, 0.0, 1.0);
            var a = loop[edgeIndex];
            var b = loop[(edgeIndex + 1) % loop.Count];
            result.Add(a + ((b - a) * t));
        }

        return result;
    }

    private static void AlignLoopWinding(IReadOnlyList<Point3D> reference, List<Point3D> target)
    {
        if (reference.Count < 3 || target.Count < 3)
        {
            return;
        }

        var refArea = SignedAreaXZ(reference);
        var targetArea = SignedAreaXZ(target);
        if (refArea * targetArea < 0.0)
        {
            target.Reverse();
        }
    }

    private static double SignedAreaXZ(IReadOnlyList<Point3D> loop)
    {
        var area = 0.0;
        for (var i = 0; i < loop.Count; i++)
        {
            var j = (i + 1) % loop.Count;
            area += (loop[i].X * loop[j].Z) - (loop[j].X * loop[i].Z);
        }

        return area * 0.5;
    }

    private static HashSet<int> FindOpenBoundaryVertices(MeshSnapshot mesh)
    {
        var edgeCounts = new Dictionary<(int A, int B), int>();
        foreach (var (i0, i1, i2) in VoxelUnionGrid.EnumerateTriangleIndices(mesh))
        {
            CountEdge(edgeCounts, i0, i1);
            CountEdge(edgeCounts, i1, i2);
            CountEdge(edgeCounts, i2, i0);
        }

        var boundary = new HashSet<int>();
        foreach (var ((a, b), count) in edgeCounts)
        {
            if (count == 1)
            {
                boundary.Add(a);
                boundary.Add(b);
            }
        }

        return boundary;
    }

    private static int FindNearestIndex(
        IReadOnlyList<Point3D> positions,
        HashSet<int> indices,
        Point3D target)
    {
        var best = -1;
        var bestDist = double.PositiveInfinity;
        foreach (var index in indices)
        {
            if (index < 0 || index >= positions.Count)
            {
                continue;
            }

            var p = positions[index];
            var dx = p.X - target.X;
            var dy = p.Y - target.Y;
            var dz = p.Z - target.Z;
            var dist = (dx * dx) + (dy * dy) + (dz * dz);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = index;
            }
        }

        return best;
    }

    private static void CountEdge(Dictionary<(int A, int B), int> edgeCounts, int a, int b)
    {
        var key = a < b ? (a, b) : (b, a);
        edgeCounts.TryGetValue(key, out var count);
        edgeCounts[key] = count + 1;
    }
}
