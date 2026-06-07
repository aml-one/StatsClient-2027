using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>
/// Builds a restoration shell whose inner surface hugs the prep inside the margin:
/// zero gap at the margin line, nominal cement gap away from margin, and extra clearance on sharp scan features.
/// </summary>
internal static class StatsDesignCementShellBuilder
{
    private const int MaxRegionTriangles = 120_000;

    /// <summary>
    /// Open inner cement surface inside the margin (single sheet — not watertight).
    /// </summary>
    public static MeshSnapshot BuildOpenInnerSurface(
        MeshSnapshot prepMesh,
        IReadOnlyList<Point3D> marginLoop,
        StatsDesignManifest settings)
    {
        var layer = BuildInnerOffsetLayer(prepMesh, marginLoop, settings);
        return new MeshSnapshot(layer.Positions, layer.TriangleIndices);
    }

    public static MeshSnapshot Build(
        MeshSnapshot prepMesh,
        IReadOnlyList<Point3D> marginLoop,
        StatsDesignManifest settings)
    {
        var inner = BuildInnerOffsetLayer(prepMesh, marginLoop, settings);
        var margin = StatsDesignMarginGeometry.Create(
            marginLoop,
            ResolveInsertionAxis(settings));
        var axialGapMm = Math.Max(settings.CementGapMm + settings.ExtraCementGapMm, 0.0);
        var transitionMm = Math.Max(settings.OffsetFromMarginMm, 0.05);
        var thicknessMm = Math.Max(settings.MinimumThicknessMm, 0.1);
        var smoothMm = Math.Max(settings.SmoothDistanceMm, 0.05);
        var outer = BuildOffsetLayerFromRegion(
            ExtractRegionMesh(prepMesh, margin),
            margin,
            axialGapMm,
            transitionMm,
            thicknessMm,
            smoothMm,
            includeWallThickness: true);

        return StitchInnerOuterShell(inner, outer);
    }

    private static OffsetLayerMesh BuildInnerOffsetLayer(
        MeshSnapshot prepMesh,
        IReadOnlyList<Point3D> marginLoop,
        StatsDesignManifest settings)
    {
        ArgumentNullException.ThrowIfNull(prepMesh);
        ArgumentNullException.ThrowIfNull(marginLoop);
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.IsMarginClosed || marginLoop.Count < 3)
        {
            throw new InvalidOperationException("Close the margin loop before generating a design shell.");
        }

        var margin = StatsDesignMarginGeometry.Create(
            marginLoop,
            ResolveInsertionAxis(settings));
        var region = ExtractRegionMesh(prepMesh, margin);
        if (region.TriangleCount == 0)
        {
            throw new InvalidOperationException("No surface was found inside the margin on the visible arch scan. Re-draw the margin on the scan that contains the preparation.");
        }

        var axialGapMm = Math.Max(settings.CementGapMm + settings.ExtraCementGapMm, 0.0);
        var transitionMm = Math.Max(settings.OffsetFromMarginMm, 0.05);
        var thicknessMm = Math.Max(settings.MinimumThicknessMm, 0.1);
        var smoothMm = Math.Max(settings.SmoothDistanceMm, 0.05);

        return BuildOffsetLayerFromRegion(
            region,
            margin,
            axialGapMm,
            transitionMm,
            thicknessMm,
            smoothMm,
            includeWallThickness: false);
    }

    private static OffsetLayerMesh BuildOffsetLayer(
        MeshSnapshot region,
        StatsDesignMarginGeometry margin,
        double axialGapMm,
        double transitionMm,
        double thicknessMm,
        double smoothMm,
        bool includeWallThickness) =>
        BuildOffsetLayerFromRegion(region, margin, axialGapMm, transitionMm, thicknessMm, smoothMm, includeWallThickness);

    public static MeshSnapshot PickBestPrepMesh(
        IReadOnlyList<MeshSnapshot> prepMeshes,
        IReadOnlyList<Point3D> marginLoop)
    {
        if (prepMeshes.Count == 1)
        {
            return prepMeshes[0];
        }

        MeshSnapshot? best = null;
        var bestScore = double.PositiveInfinity;

        foreach (var mesh in prepMeshes)
        {
            var total = 0.0;
            foreach (var point in marginLoop)
            {
                total += ClosestDistanceToMesh(point, mesh);
            }

            var score = total / marginLoop.Count;
            if (score < bestScore)
            {
                bestScore = score;
                best = mesh;
            }
        }

        return best ?? prepMeshes[0];
    }

    private static double ClosestDistanceToMesh(Point3D point, MeshSnapshot mesh)
    {
        var best = double.PositiveInfinity;
        foreach (var (i0, i1, i2) in VoxelUnionGrid.EnumerateTriangleIndices(mesh))
        {
            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= mesh.Positions.Length || i1 >= mesh.Positions.Length || i2 >= mesh.Positions.Length)
            {
                continue;
            }

            var closest = ClosestPointOnTriangle(point, mesh.Positions[i0], mesh.Positions[i1], mesh.Positions[i2]);
            var dx = point.X - closest.X;
            var dy = point.Y - closest.Y;
            var dz = point.Z - closest.Z;
            best = Math.Min(best, Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz)));
        }

        return best;
    }

    private static MeshSnapshot ExtractRegionMesh(MeshSnapshot prepMesh, StatsDesignMarginGeometry margin)
    {
        var positions = prepMesh.Positions;
        var indices = new List<int>();
        var stride = 1;
        var estimated = prepMesh.TriangleCount;
        if (estimated > MaxRegionTriangles)
        {
            stride = (int)Math.Ceiling(estimated / (double)MaxRegionTriangles);
        }

        var triangleIndex = 0;
        foreach (var (i0, i1, i2) in VoxelUnionGrid.EnumerateTriangleIndices(prepMesh))
        {
            if (triangleIndex++ % stride != 0)
            {
                continue;
            }

            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= positions.Length || i1 >= positions.Length || i2 >= positions.Length)
            {
                continue;
            }

            var c = new Point3D(
                (positions[i0].X + positions[i1].X + positions[i2].X) / 3.0,
                (positions[i0].Y + positions[i1].Y + positions[i2].Y) / 3.0,
                (positions[i0].Z + positions[i1].Z + positions[i2].Z) / 3.0);

            if (!margin.ContainsPoint(c))
            {
                continue;
            }

            indices.Add(i0);
            indices.Add(i1);
            indices.Add(i2);
        }

        if (indices.Count < 3)
        {
            return new MeshSnapshot(Array.Empty<Point3D>(), Array.Empty<int>());
        }

        return new MeshSnapshot(positions, indices.ToArray());
    }

    private sealed record OffsetLayerMesh(Point3D[] Positions, int[] TriangleIndices);

    private static OffsetLayerMesh BuildOffsetLayerFromRegion(
        MeshSnapshot region,
        StatsDesignMarginGeometry margin,
        double axialGapMm,
        double transitionMm,
        double thicknessMm,
        double smoothMm,
        bool includeWallThickness)
    {
        var vertexCount = region.Positions.Length;
        var rawNormals = ComputeVertexNormals(region);
        var smoothNormals = SmoothNormals(region, rawNormals, smoothMm);
        var outward = OrientOutwardNormals(region.Positions, smoothNormals, margin);

        var remapped = new Dictionary<int, int>();
        var positions = new List<Point3D>();
        var indices = new List<int>();

        foreach (var (i0, i1, i2) in VoxelUnionGrid.EnumerateTriangleIndices(region))
        {
            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= vertexCount || i1 >= vertexCount || i2 >= vertexCount)
            {
                continue;
            }

            var remappedCorners = new int[3];
            for (var corner = 0; corner < 3; corner++)
            {
                var source = corner switch
                {
                    0 => i0,
                    1 => i1,
                    _ => i2
                };

                if (!remapped.TryGetValue(source, out var target))
                {
                    target = positions.Count;
                    remapped[source] = target;
                    positions.Add(OffsetVertex(
                        region.Positions[source],
                        outward[source],
                        rawNormals[source],
                        smoothNormals[source],
                        margin,
                        axialGapMm,
                        transitionMm,
                        thicknessMm,
                        smoothMm,
                        includeWallThickness));
                }

                remappedCorners[corner] = target;
            }

            indices.Add(remappedCorners[0]);
            indices.Add(remappedCorners[1]);
            indices.Add(remappedCorners[2]);
        }

        return new OffsetLayerMesh(positions.ToArray(), indices.ToArray());
    }

    private static Point3D OffsetVertex(
        Point3D source,
        Vector3D outward,
        Vector3D rawNormal,
        Vector3D smoothNormal,
        StatsDesignMarginGeometry margin,
        double axialGapMm,
        double transitionMm,
        double thicknessMm,
        double smoothMm,
        bool includeWallThickness)
    {
        var ramp = margin.NormalizedDistanceFromMargin(source, transitionMm);
        if (margin.DistanceToLoopMm(source) < 0.03)
        {
            ramp = 0.0;
        }

        var gap = axialGapMm * ramp;
        var curvature = EstimateCurvaturePenalty(rawNormal, smoothNormal);
        var sharpExtra = axialGapMm * curvature * Math.Clamp(smoothMm / 0.20, 0.35, 2.5);
        var radial = gap + sharpExtra;

        if (includeWallThickness)
        {
            radial += thicknessMm * ramp;
        }

        return source + (outward * radial);
    }

    private static double EstimateCurvaturePenalty(Vector3D rawNormal, Vector3D smoothNormal)
    {
        if (rawNormal.LengthSquared < 1e-12 || smoothNormal.LengthSquared < 1e-12)
        {
            return 0.0;
        }

        var raw = rawNormal;
        var smooth = smoothNormal;
        raw.Normalize();
        smooth.Normalize();
        var alignment = Math.Abs(Vector3D.DotProduct(raw, smooth));
        return Math.Clamp(1.0 - alignment, 0.0, 1.0);
    }

    private static Vector3D[] OrientOutwardNormals(
        IReadOnlyList<Point3D> positions,
        Vector3D[] normals,
        StatsDesignMarginGeometry margin)
    {
        var oriented = new Vector3D[normals.Length];
        for (var i = 0; i < normals.Length; i++)
        {
            var n = normals[i];
            if (n.LengthSquared < 1e-12)
            {
                n = margin.PlaneNormal;
            }
            else
            {
                n.Normalize();
            }

            var toVertex = positions[i] - margin.Centroid;
            if (Vector3D.DotProduct(n, toVertex) < 0.0)
            {
                n = new Vector3D(-n.X, -n.Y, -n.Z);
            }

            oriented[i] = n;
        }

        return oriented;
    }

    private static Vector3D[] ComputeVertexNormals(MeshSnapshot mesh)
    {
        var normals = new Vector3D[mesh.Positions.Length];
        foreach (var (i0, i1, i2) in VoxelUnionGrid.EnumerateTriangleIndices(mesh))
        {
            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= normals.Length || i1 >= normals.Length || i2 >= normals.Length)
            {
                continue;
            }

            var p0 = mesh.Positions[i0];
            var p1 = mesh.Positions[i1];
            var p2 = mesh.Positions[i2];
            var face = Vector3D.CrossProduct(p1 - p0, p2 - p0);
            if (face.LengthSquared < 1e-12)
            {
                continue;
            }

            normals[i0] += face;
            normals[i1] += face;
            normals[i2] += face;
        }

        for (var i = 0; i < normals.Length; i++)
        {
            if (normals[i].LengthSquared > 1e-12)
            {
                normals[i].Normalize();
            }
        }

        return normals;
    }

    private static Vector3D[] SmoothNormals(MeshSnapshot mesh, Vector3D[] normals, double smoothMm)
    {
        var neighbors = BuildAdjacency(mesh);
        var passes = Math.Clamp((int)Math.Round(smoothMm / 0.05), 2, 10);
        var current = normals;
        var strength = Math.Clamp(smoothMm / 0.35, 0.35, 0.85);

        for (var pass = 0; pass < passes; pass++)
        {
            var next = new Vector3D[current.Length];
            for (var i = 0; i < current.Length; i++)
            {
                var accum = current[i];
                var nbrs = neighbors[i];
                if (nbrs.Count == 0)
                {
                    next[i] = accum;
                    continue;
                }

                var avg = new Vector3D();
                foreach (var j in nbrs)
                {
                    avg += current[j];
                }

                avg *= 1.0 / nbrs.Count;
                if (avg.LengthSquared < 1e-12)
                {
                    next[i] = accum;
                    continue;
                }

                avg.Normalize();
                var baseNormal = accum.LengthSquared > 1e-12 ? accum : avg;
                baseNormal.Normalize();
                next[i] = (baseNormal * (1.0 - strength)) + (avg * strength);
                if (next[i].LengthSquared > 1e-12)
                {
                    next[i].Normalize();
                }
            }

            current = next;
        }

        return current;
    }

    private static List<HashSet<int>> BuildAdjacency(MeshSnapshot mesh)
    {
        var neighbors = new List<HashSet<int>>(mesh.Positions.Length);
        for (var i = 0; i < mesh.Positions.Length; i++)
        {
            neighbors.Add(new HashSet<int>());
        }

        foreach (var (i0, i1, i2) in VoxelUnionGrid.EnumerateTriangleIndices(mesh))
        {
            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= neighbors.Count || i1 >= neighbors.Count || i2 >= neighbors.Count)
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

    private static MeshSnapshot StitchInnerOuterShell(OffsetLayerMesh inner, OffsetLayerMesh outer)
    {
        var positions = new List<Point3D>();
        positions.AddRange(inner.Positions);
        var outerOffset = inner.Positions.Length;
        positions.AddRange(outer.Positions);

        var indices = new List<int>();
        AddTriangles(indices, inner.TriangleIndices, flip: true);
        AddTriangles(indices, outer.TriangleIndices, flip: false, vertexOffset: outerOffset);
        AddSideWalls(indices, inner, outerOffset);

        var shell = new MeshSnapshot(positions.ToArray(), indices.ToArray());
        var maxIsland = MeshFuseOptions.MapCleanupStrengthToMaxIslandTriangles(35);
        shell = MeshIslandCleanup.RemoveTinyIslands(shell, maxIsland);
        return MeshSmoothing.Smooth(shell, passes: 1);
    }

    private static void AddTriangles(List<int> indices, int[] source, bool flip, int vertexOffset = 0)
    {
        for (var i = 0; i + 2 < source.Length; i += 3)
        {
            var i0 = source[i] + vertexOffset;
            var i1 = source[i + 1] + vertexOffset;
            var i2 = source[i + 2] + vertexOffset;
            if (flip)
            {
                indices.Add(i0);
                indices.Add(i2);
                indices.Add(i1);
            }
            else
            {
                indices.Add(i0);
                indices.Add(i1);
                indices.Add(i2);
            }
        }
    }

    private static void AddSideWalls(List<int> indices, OffsetLayerMesh inner, int outerOffset)
    {
        var edgeCounts = new Dictionary<(int A, int B), int>();
        foreach (var (i0, i1, i2) in EnumerateTriangles(inner.TriangleIndices))
        {
            CountEdge(edgeCounts, i0, i1);
            CountEdge(edgeCounts, i1, i2);
            CountEdge(edgeCounts, i2, i0);
        }

        foreach (var ((a, b), count) in edgeCounts)
        {
            if (count != 1)
            {
                continue;
            }

            var oa = a + outerOffset;
            var ob = b + outerOffset;
            indices.Add(a);
            indices.Add(b);
            indices.Add(ob);
            indices.Add(a);
            indices.Add(ob);
            indices.Add(oa);
        }
    }

    private static void CountEdge(Dictionary<(int A, int B), int> edgeCounts, int a, int b)
    {
        var key = a < b ? (a, b) : (b, a);
        edgeCounts.TryGetValue(key, out var count);
        edgeCounts[key] = count + 1;
    }

    private static IEnumerable<(int I0, int I1, int I2)> EnumerateTriangles(int[] triangleIndices)
    {
        for (var i = 0; i + 2 < triangleIndices.Length; i += 3)
        {
            yield return (triangleIndices[i], triangleIndices[i + 1], triangleIndices[i + 2]);
        }
    }

    private static Point3D ClosestPointOnTriangle(Point3D point, Point3D a, Point3D b, Point3D c)
    {
        var ab = b - a;
        var ac = c - a;
        var ap = point - a;

        var d1 = Vector3D.DotProduct(ab, ap);
        var d2 = Vector3D.DotProduct(ac, ap);
        if (d1 <= 0.0 && d2 <= 0.0)
        {
            return a;
        }

        var bp = point - b;
        var d3 = Vector3D.DotProduct(ab, bp);
        var d4 = Vector3D.DotProduct(ac, bp);
        if (d3 >= 0.0 && d4 <= d3)
        {
            return b;
        }

        var vc = (d1 * d4) - (d3 * d2);
        if (vc <= 0.0 && d1 >= 0.0 && d3 <= 0.0)
        {
            var v = d1 / (d1 - d3);
            return a + (ab * v);
        }

        var cp = point - c;
        var d5 = Vector3D.DotProduct(ab, cp);
        var d6 = Vector3D.DotProduct(ac, cp);
        if (d6 >= 0.0 && d5 <= d6)
        {
            return c;
        }

        var vb = (d5 * d2) - (d1 * d6);
        if (vb <= 0.0 && d2 >= 0.0 && d6 <= 0.0)
        {
            var w = d2 / (d2 - d6);
            return a + (ac * w);
        }

        var va = (d3 * d6) - (d5 * d4);
        if (va <= 0.0 && (d4 - d3) >= 0.0 && (d5 - d6) >= 0.0)
        {
            var w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            return b + ((c - b) * w);
        }

        var denom = 1.0 / ((va + vb) + vc);
        var v2 = vb * denom;
        var w2 = vc * denom;
        return a + (ab * v2) + (ac * w2);
    }

    private static Vector3D? ResolveInsertionAxis(StatsDesignManifest settings)
    {
        var axis = new Vector3D(settings.InsertionAxisX, settings.InsertionAxisY, settings.InsertionAxisZ);
        return axis.LengthSquared > 1e-12 ? axis : null;
    }
}
