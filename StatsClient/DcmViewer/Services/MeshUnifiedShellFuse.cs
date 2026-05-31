using System.Windows;
using System.Windows.Media.Media3D;
using System.Linq;

namespace DCMViewer.Services;

/// <summary>
/// Keeps full scan triangles but removes duplicate opposing faces where different meshes touch,
/// then welds contact vertices so 3Shape tends to read one shell.
/// </summary>
internal static class MeshUnifiedShellFuse
{
    private const double DuplicateNormalThreshold = -0.88;
    private const double DuplicatePairDistanceScale = 0.22;
    private const double MinScoreDeltaToCull = 0.12;

    public static MeshSnapshot Fuse(
        IReadOnlyList<MeshSnapshot> meshes,
        MeshFuseOptions options,
        IReadOnlyList<FuseInnerSideHint>? innerSideHints = null,
        IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(meshes);

        progress?.Report(0.0);

        if (meshes.Count == 0)
        {
            throw new InvalidOperationException("No mesh data is available to fuse.");
        }

        if (meshes.Count == 1)
        {
            progress?.Report(1.0);
            return MeshFuseOrientation.NormalizeFusedShellOrientation(meshes[0], innerSideHints);
        }

        // Cull orientation opposes scans at contact so duplicate sheets can be found.
        // Working-side alignment is applied only after merge so contact geometry is kept.
        var cullMeshes = MeshFuseOrientation.AlignMeshesForInterfaceCull(meshes);
        progress?.Report(0.05);
        var combined = CombineWithMeshIds(cullMeshes);
        if (combined.TriangleCount == 0)
        {
            throw new InvalidOperationException("Combined meshes do not contain any triangles.");
        }

        progress?.Report(0.08);
        var bounds = ComputeBounds(combined.Positions);
        var cullDistance = ComputeInterfaceCullDistance(bounds, options);
        var keepFlags = new bool[combined.TriangleCount];
        Array.Fill(keepFlags, true);

        CullNearDuplicateOpposingPairs(combined, keepFlags, bounds, cullDistance, progress);

        var keptCount = keepFlags.Count(static flag => flag);
        if (keptCount == 0)
        {
            progress?.Report(1.0);
            return MeshFuseOrientation.NormalizeFusedShellOrientation(
                MeshCombineFuse.Fuse(cullMeshes),
                innerSideHints);
        }

        progress?.Report(0.88);
        var built = BuildKeptMesh(combined, keepFlags, cullDistance * 0.2);
        progress?.Report(0.94);
        var oriented = MeshFuseOrientation.NormalizeFusedShellOrientation(built, innerSideHints);
        progress?.Report(1.0);
        return oriented;
    }

    private sealed class CombinedMeshWithSources
    {
        public required Point3D[] Positions { get; init; }
        public required int[] Indices { get; init; }
        public required int[] MeshIdPerTriangle { get; init; }
        public int TriangleCount => MeshIdPerTriangle.Length;
    }

    private static CombinedMeshWithSources CombineWithMeshIds(IReadOnlyList<MeshSnapshot> meshes)
    {
        var positions = new List<Point3D>();
        var indices = new List<int>();
        var meshIds = new List<int>();

        for (var meshIndex = 0; meshIndex < meshes.Count; meshIndex++)
        {
            var mesh = meshes[meshIndex];
            if (mesh.Positions.Length == 0)
            {
                continue;
            }

            var vertexOffset = positions.Count;
            positions.AddRange(mesh.Positions);

            foreach (var (i0, i1, i2) in VoxelUnionGrid.EnumerateTriangleIndices(mesh))
            {
                if (i0 < 0 || i1 < 0 || i2 < 0 ||
                    i0 >= mesh.Positions.Length || i1 >= mesh.Positions.Length || i2 >= mesh.Positions.Length)
                {
                    continue;
                }

                indices.Add(vertexOffset + i0);
                indices.Add(vertexOffset + i1);
                indices.Add(vertexOffset + i2);
                meshIds.Add(meshIndex);
            }
        }

        return new CombinedMeshWithSources
        {
            Positions = positions.ToArray(),
            Indices = indices.ToArray(),
            MeshIdPerTriangle = meshIds.ToArray()
        };
    }

    /// <summary>Removes only nearly coincident duplicate sheets between scans.</summary>
    private static void CullNearDuplicateOpposingPairs(
        CombinedMeshWithSources combined,
        bool[] keepFlags,
        Rect3D bounds,
        double cullDistance,
        IProgress<double>? progress = null)
    {
        var triangleCount = combined.TriangleCount;
        var centroids = new Point3D[triangleCount];
        var normals = new Vector3D[triangleCount];
        var exteriorScores = new double[triangleCount];
        var sceneCenter = new Point3D(
            bounds.X + (bounds.SizeX * 0.5),
            bounds.Y + (bounds.SizeY * 0.5),
            bounds.Z + (bounds.SizeZ * 0.5));

        var progressInterval = Math.Max(triangleCount / 40, 5000);
        for (var triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            if (triangleIndex % progressInterval == 0)
            {
                progress?.Report(0.08 + (0.22 * triangleIndex / Math.Max(triangleCount, 1)));
            }

            if (!keepFlags[triangleIndex] ||
                !TryGetTriangle(combined, triangleIndex, out var p0, out var p1, out var p2))
            {
                continue;
            }

            var centroid = new Point3D(
                (p0.X + p1.X + p2.X) / 3.0,
                (p0.Y + p1.Y + p2.Y) / 3.0,
                (p0.Z + p1.Z + p2.Z) / 3.0);
            var normal = ComputeUnitNormal(p0, p1, p2);

            centroids[triangleIndex] = centroid;
            normals[triangleIndex] = normal;
            exteriorScores[triangleIndex] = ComputeExteriorScore(centroid, normal, sceneCenter);
        }

        progress?.Report(0.32);
        var pairDistance = cullDistance * DuplicatePairDistanceScale;
        var pairDistanceSq = pairDistance * pairDistance;
        var cellSize = Math.Max(pairDistance * 2.0, 0.03);
        var buckets = new Dictionary<(int X, int Y, int Z), List<int>>();

        for (var triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            if (!keepFlags[triangleIndex])
            {
                continue;
            }

            var key = BucketKey(centroids[triangleIndex], cellSize);
            if (!buckets.TryGetValue(key, out var list))
            {
                list = new List<int>();
                buckets[key] = list;
            }

            list.Add(triangleIndex);
        }

        progress?.Report(0.36);
        foreach (var bucket in buckets.Values)
        {
            CullOpposingPairsInBucket(
                combined,
                keepFlags,
                centroids,
                normals,
                exteriorScores,
                bucket,
                pairDistanceSq,
                DuplicateNormalThreshold);
        }

        progress?.Report(0.42);
        var bucketEntries = buckets.ToList();
        var processedTriangles = 0;
        foreach (var (key, bucket) in bucketEntries)
        {
            foreach (var triangleIndex in bucket)
            {
                if (!keepFlags[triangleIndex])
                {
                    continue;
                }

                foreach (var offset in NeighborBucketOffsets)
                {
                    var neighborKey = (key.X + offset.X, key.Y + offset.Y, key.Z + offset.Z);
                    if (!buckets.TryGetValue(neighborKey, out var neighborBucket))
                    {
                        continue;
                    }

                    CullOpposingPairsAcrossBuckets(
                        combined,
                        keepFlags,
                        centroids,
                        normals,
                        exteriorScores,
                        bucket,
                        neighborBucket,
                        triangleIndex,
                        pairDistanceSq,
                        DuplicateNormalThreshold);
                }

                processedTriangles++;
                if (processedTriangles % progressInterval == 0)
                {
                    progress?.Report(0.42 + (0.44 * processedTriangles / Math.Max(triangleCount, 1)));
                }
            }
        }

        progress?.Report(0.86);
    }

    private static void CullOpposingPairsInBucket(
        CombinedMeshWithSources combined,
        bool[] keepFlags,
        Point3D[] centroids,
        Vector3D[] normals,
        double[] exteriorScores,
        List<int> bucket,
        double cullDistanceSq,
        double opposingNormalThreshold = DuplicateNormalThreshold)
    {
        for (var a = 0; a < bucket.Count; a++)
        {
            var indexA = bucket[a];
            if (!keepFlags[indexA])
            {
                continue;
            }

            for (var b = a + 1; b < bucket.Count; b++)
            {
                var indexB = bucket[b];
                TryCullOpposingPair(
                    combined,
                    keepFlags,
                    centroids,
                    normals,
                    exteriorScores,
                    indexA,
                    indexB,
                    cullDistanceSq,
                    opposingNormalThreshold);
            }
        }
    }

    private static void CullOpposingPairsAcrossBuckets(
        CombinedMeshWithSources combined,
        bool[] keepFlags,
        Point3D[] centroids,
        Vector3D[] normals,
        double[] exteriorScores,
        List<int> bucket,
        List<int> neighborBucket,
        int minTriangleIndex,
        double cullDistanceSq,
        double opposingNormalThreshold = DuplicateNormalThreshold)
    {
        foreach (var triangleIndex in bucket)
        {
            if (triangleIndex < minTriangleIndex || !keepFlags[triangleIndex])
            {
                continue;
            }

            foreach (var otherIndex in neighborBucket)
            {
                if (otherIndex <= triangleIndex || !keepFlags[otherIndex])
                {
                    continue;
                }

                TryCullOpposingPair(
                    combined,
                    keepFlags,
                    centroids,
                    normals,
                    exteriorScores,
                    triangleIndex,
                    otherIndex,
                    cullDistanceSq,
                    opposingNormalThreshold);
            }
        }
    }

    private static void TryCullOpposingPair(
        CombinedMeshWithSources combined,
        bool[] keepFlags,
        Point3D[] centroids,
        Vector3D[] normals,
        double[] exteriorScores,
        int indexA,
        int indexB,
        double cullDistanceSq,
        double opposingNormalThreshold)
    {
        if (!keepFlags[indexA] || !keepFlags[indexB])
        {
            return;
        }

        if (combined.MeshIdPerTriangle[indexA] == combined.MeshIdPerTriangle[indexB])
        {
            return;
        }

        var delta = centroids[indexB] - centroids[indexA];
        if (delta.LengthSquared > cullDistanceSq)
        {
            return;
        }

        if (Vector3D.DotProduct(normals[indexA], normals[indexB]) > opposingNormalThreshold)
        {
            return;
        }

        var scoreA = exteriorScores[indexA];
        var scoreB = exteriorScores[indexB];
        if (Math.Abs(scoreA - scoreB) < MinScoreDeltaToCull)
        {
            return;
        }

        if (scoreA > scoreB)
        {
            keepFlags[indexB] = false;
        }
        else
        {
            keepFlags[indexA] = false;
        }
    }

    private static double ComputeExteriorScore(Point3D centroid, Vector3D normal, Point3D sceneCenter)
    {
        if (normal.LengthSquared < 1e-12)
        {
            return 0.0;
        }

        var toCenter = sceneCenter - centroid;
        if (toCenter.LengthSquared < 1e-12)
        {
            return 0.0;
        }

        toCenter.Normalize();
        return -Vector3D.DotProduct(normal, toCenter);
    }

    private static double ComputeInterfaceCullDistance(Rect3D bounds, MeshFuseOptions options)
    {
        var maxDim = Math.Max(bounds.SizeX, Math.Max(bounds.SizeY, bounds.SizeZ));
        if (maxDim <= 1e-9)
        {
            maxDim = 1.0;
        }

        var resolution = Math.Clamp(options.Resolution, MeshFuseOptions.MinResolution, MeshFuseOptions.MaxResolution);
        var voxelSize = maxDim / resolution;
        var gapBridge = Math.Clamp(options.GapBridgeVoxels, MeshFuseOptions.MinGapBridgeVoxels, MeshFuseOptions.MaxGapBridgeVoxels);
        var shellThickness = Math.Clamp(options.ShellThicknessVoxels, MeshFuseOptions.MinShellThicknessVoxels, MeshFuseOptions.MaxShellThicknessVoxels);

        var distance = voxelSize * (2.0 + gapBridge + (shellThickness * 0.5));
        return Math.Clamp(distance, 0.08, Math.Max(0.5, maxDim * 0.01));
    }

    private static Rect3D ComputeBounds(IReadOnlyList<Point3D> positions)
    {
        if (positions.Count == 0)
        {
            return Rect3D.Empty;
        }

        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var minZ = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;
        var maxZ = double.NegativeInfinity;

        foreach (var point in positions)
        {
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            minZ = Math.Min(minZ, point.Z);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
            maxZ = Math.Max(maxZ, point.Z);
        }

        return new Rect3D(minX, minY, minZ, maxX - minX, maxY - minY, maxZ - minZ);
    }

    private static readonly (int X, int Y, int Z)[] NeighborBucketOffsets =
    {
        (0, 0, 0),
        (1, 0, 0), (-1, 0, 0),
        (0, 1, 0), (0, -1, 0),
        (0, 0, 1), (0, 0, -1)
    };

    private static (int X, int Y, int Z) BucketKey(Point3D point, double cellSize)
    {
        return (
            (int)Math.Floor(point.X / cellSize),
            (int)Math.Floor(point.Y / cellSize),
            (int)Math.Floor(point.Z / cellSize));
    }

    private static bool TryGetTriangle(
        CombinedMeshWithSources combined,
        int triangleIndex,
        out Point3D p0,
        out Point3D p1,
        out Point3D p2)
    {
        p0 = default;
        p1 = default;
        p2 = default;

        if (triangleIndex < 0 || triangleIndex >= combined.TriangleCount)
        {
            return false;
        }

        var baseIndex = triangleIndex * 3;
        if (baseIndex + 2 >= combined.Indices.Length)
        {
            return false;
        }

        var i0 = combined.Indices[baseIndex];
        var i1 = combined.Indices[baseIndex + 1];
        var i2 = combined.Indices[baseIndex + 2];
        if (i0 < 0 || i1 < 0 || i2 < 0 ||
            i0 >= combined.Positions.Length || i1 >= combined.Positions.Length || i2 >= combined.Positions.Length)
        {
            return false;
        }

        p0 = combined.Positions[i0];
        p1 = combined.Positions[i1];
        p2 = combined.Positions[i2];
        return true;
    }

    private static MeshSnapshot BuildKeptMesh(CombinedMeshWithSources combined, bool[] keepFlags, double weldEpsilon)
    {
        var positions = new List<Point3D>();
        var indices = new List<int>();
        var vertexMap = new Dictionary<int, int>();

        for (var triangleIndex = 0; triangleIndex < combined.TriangleCount; triangleIndex++)
        {
            if (!keepFlags[triangleIndex])
            {
                continue;
            }

            var baseIndex = triangleIndex * 3;
            if (baseIndex + 2 >= combined.Indices.Length)
            {
                continue;
            }

            var remapped = new int[3];
            for (var corner = 0; corner < 3; corner++)
            {
                var sourceIndex = combined.Indices[baseIndex + corner];
                if (sourceIndex < 0 || sourceIndex >= combined.Positions.Length)
                {
                    remapped = Array.Empty<int>();
                    break;
                }

                if (!vertexMap.TryGetValue(sourceIndex, out var targetIndex))
                {
                    targetIndex = positions.Count;
                    vertexMap[sourceIndex] = targetIndex;
                    positions.Add(combined.Positions[sourceIndex]);
                }

                remapped[corner] = targetIndex;
            }

            if (remapped.Length != 3)
            {
                continue;
            }

            indices.Add(remapped[0]);
            indices.Add(remapped[1]);
            indices.Add(remapped[2]);
        }

        if (indices.Count < 3)
        {
            throw new InvalidOperationException("Unified shell filtering removed all triangles.");
        }

        WeldVertices(positions, indices, Math.Max(weldEpsilon, 0.02));
        return new MeshSnapshot(positions.ToArray(), indices.ToArray());
    }

    private static void WeldVertices(List<Point3D> positions, List<int> indices, double epsilon)
    {
        if (positions.Count == 0)
        {
            return;
        }

        var epsilonSq = epsilon * epsilon;
        var remap = new int[positions.Count];
        for (var index = 0; index < remap.Length; index++)
        {
            remap[index] = index;
        }

        var cellSize = epsilon;
        var buckets = new Dictionary<(int X, int Y, int Z), List<int>>();

        for (var index = 0; index < positions.Count; index++)
        {
            var key = BucketKey(positions[index], cellSize);
            if (!buckets.TryGetValue(key, out var bucket))
            {
                bucket = new List<int>();
                buckets[key] = bucket;
            }

            bucket.Add(index);
        }

        foreach (var bucket in buckets.Values)
        {
            for (var a = 0; a < bucket.Count; a++)
            {
                var indexA = bucket[a];
                var rootA = Find(remap, indexA);
                var pointA = positions[rootA];

                for (var b = a + 1; b < bucket.Count; b++)
                {
                    var indexB = bucket[b];
                    var rootB = Find(remap, indexB);
                    if (rootA == rootB)
                    {
                        continue;
                    }

                    var pointB = positions[rootB];
                    var dx = pointA.X - pointB.X;
                    var dy = pointA.Y - pointB.Y;
                    var dz = pointA.Z - pointB.Z;
                    if ((dx * dx) + (dy * dy) + (dz * dz) <= epsilonSq)
                    {
                        Union(remap, rootA, rootB);
                    }
                }
            }
        }

        for (var index = 0; index < indices.Count; index++)
        {
            if (indices[index] < 0 || indices[index] >= remap.Length)
            {
                continue;
            }

            indices[index] = Find(remap, indices[index]);
        }

        var compactMap = new Dictionary<int, int>();
        var compacted = new List<Point3D>();
        for (var index = 0; index < indices.Count; index++)
        {
            var root = indices[index];
            if (root < 0 || root >= positions.Count)
            {
                continue;
            }

            if (!compactMap.TryGetValue(root, out var compactIndex))
            {
                compactIndex = compacted.Count;
                compactMap[root] = compactIndex;
                compacted.Add(positions[root]);
            }

            indices[index] = compactIndex;
        }

        positions.Clear();
        positions.AddRange(compacted);
    }

    private static int Find(int[] remap, int index)
    {
        while (remap[index] != index)
        {
            remap[index] = remap[remap[index]];
            index = remap[index];
        }

        return index;
    }

    private static void Union(int[] remap, int a, int b)
    {
        var rootA = Find(remap, a);
        var rootB = Find(remap, b);
        if (rootA != rootB)
        {
            remap[rootB] = rootA;
        }
    }

    private static Vector3D ComputeUnitNormal(Point3D p0, Point3D p1, Point3D p2)
    {
        var ux = p1.X - p0.X;
        var uy = p1.Y - p0.Y;
        var uz = p1.Z - p0.Z;
        var vx = p2.X - p0.X;
        var vy = p2.Y - p0.Y;
        var vz = p2.Z - p0.Z;

        var nx = (uy * vz) - (uz * vy);
        var ny = (uz * vx) - (ux * vz);
        var nz = (ux * vy) - (uy * vx);
        var length = Math.Sqrt((nx * nx) + (ny * ny) + (nz * nz));
        if (length <= 1e-12)
        {
            return new Vector3D();
        }

        return new Vector3D(nx / length, ny / length, nz / length);
    }
}
