using System.Windows;
using System.Windows.Media.Media3D;
using System.Linq;

namespace DCMViewer.Services;

/// <summary>Fixes inverted or inconsistent triangle winding when fusing meshes from different sources.</summary>
internal static class MeshFuseOrientation
{
    private const int MaxSamplesPerMesh = 12_000;

    public static IReadOnlyList<MeshSnapshot> AlignMeshesForFusion(
        IReadOnlyList<MeshSnapshot> meshes,
        IReadOnlyList<FuseInnerSideHint>? innerSideHints = null)
    {
        if (meshes.Count <= 1)
        {
            return meshes;
        }

        var hintByMesh = innerSideHints?
            .GroupBy(static hint => hint.MeshIndex)
            .ToDictionary(static group => group.Key, static group => group.Last());
        var globalWorkingDirection = BuildGlobalWorkingDirection(meshes[0], innerSideHints);

        var aligned = new MeshSnapshot[meshes.Count];
        for (var index = 0; index < meshes.Count; index++)
        {
            FuseInnerSideHint? hint = null;
            if (hintByMesh is not null)
            {
                hintByMesh.TryGetValue(index, out hint);
            }

            aligned[index] = ShouldFlipToWorkingSide(meshes[index], hint, globalWorkingDirection)
                ? FlipWinding(meshes[index])
                : meshes[index];
        }

        return aligned;
    }

    /// <summary>
    /// Orients scans so contact normals oppose — used only before duplicate-face culling.
    /// </summary>
    public static IReadOnlyList<MeshSnapshot> AlignMeshesForInterfaceCull(IReadOnlyList<MeshSnapshot> meshes)
    {
        if (meshes.Count <= 1)
        {
            return meshes;
        }

        var referenceIndex = 0;
        var referenceCount = CountTriangles(meshes[0]);
        for (var index = 1; index < meshes.Count; index++)
        {
            var triangleCount = CountTriangles(meshes[index]);
            if (triangleCount > referenceCount)
            {
                referenceCount = triangleCount;
                referenceIndex = index;
            }
        }

        var reference = meshes[referenceIndex];
        var aligned = new MeshSnapshot[meshes.Count];
        for (var index = 0; index < meshes.Count; index++)
        {
            if (index == referenceIndex)
            {
                aligned[index] = meshes[index];
                continue;
            }

            aligned[index] = ShouldFlipRelativeToReference(meshes[index], reference)
                ? FlipWinding(meshes[index])
                : meshes[index];
        }

        return aligned;
    }

    /// <summary>
    /// Makes adjacent triangles consistent, then flips whole disconnected parts that still
    /// point the wrong way. Safe for Designer import (does not delete geometry).
    /// </summary>
    public static MeshSnapshot NormalizeFusedShellOrientation(
        MeshSnapshot mesh,
        IReadOnlyList<FuseInnerSideHint>? innerSideHints = null)
    {
        mesh = RepairAfterFuse(mesh);
        return AlignConnectedComponents(mesh, BuildGlobalWorkingDirection(mesh, innerSideHints));
    }

    private static Vector3D BuildGlobalWorkingDirection(
        MeshSnapshot mesh,
        IReadOnlyList<FuseInnerSideHint>? innerSideHints)
    {
        if (innerSideHints is { Count: > 0 })
        {
            var sum = new Vector3D();
            foreach (var hint in innerSideHints)
            {
                var preferred = hint.PreferredNormal;
                if (preferred.LengthSquared < 1e-12)
                {
                    continue;
                }

                preferred.Normalize();
                sum += preferred;
            }

            if (sum.LengthSquared > 1e-12)
            {
                sum.Normalize();
                return sum;
            }
        }

        return default;
    }

    private static bool ShouldFlipToWorkingSide(
        MeshSnapshot mesh,
        FuseInnerSideHint? hint,
        Vector3D globalWorkingDirection)
    {
        if (hint is not null && hint.PreferredNormal.LengthSquared > 1e-12)
        {
            return ShouldFlipToDirection(mesh, hint.PreferredNormal);
        }

        if (globalWorkingDirection.LengthSquared > 1e-12)
        {
            return ShouldFlipToDirection(mesh, globalWorkingDirection);
        }

        return IsMeshPredominantlyInverted(mesh);
    }

    private static bool ShouldFlipToDirection(MeshSnapshot mesh, Vector3D preferredDirection)
    {
        preferredDirection.Normalize();
        var aligned = 0;
        var opposed = 0;
        var stride = Math.Max(1, CountTriangles(mesh) / MaxSamplesPerMesh);
        var sampleIndex = 0;

        foreach (var (_, normal) in EnumerateTriangleFrames(mesh))
        {
            if (sampleIndex++ % stride != 0 || normal.LengthSquared < 1e-12)
            {
                continue;
            }

            var alignment = Vector3D.DotProduct(normal, preferredDirection);
            if (alignment > 0.15)
            {
                aligned++;
            }
            else if (alignment < -0.15)
            {
                opposed++;
            }
        }

        if (aligned + opposed < 8)
        {
            return false;
        }

        return opposed > aligned;
    }

    private static MeshSnapshot AlignConnectedComponents(MeshSnapshot mesh, Vector3D preferredDirection)
    {
        var positions = mesh.Positions.ToArray();
        var indices = mesh.TriangleIndices.Length >= 3
            ? mesh.TriangleIndices.ToArray()
            : BuildSoupIndices(positions.Length);

        var triangleCount = indices.Length / 3;
        if (triangleCount == 0)
        {
            return mesh;
        }

        var adjacency = BuildTriangleAdjacency(indices, triangleCount);
        var visited = new bool[triangleCount];

        for (var seed = 0; seed < triangleCount; seed++)
        {
            if (visited[seed])
            {
                continue;
            }

            var component = CollectComponent(adjacency, indices, triangleCount, seed, visited);
            if (ShouldFlipComponent(positions, indices, component, preferredDirection))
            {
                foreach (var triangleIndex in component)
                {
                    FlipTriangle(indices, triangleIndex);
                }
            }
        }

        return new MeshSnapshot(positions, indices);
    }

    private static List<int> CollectComponent(
        Dictionary<long, List<DirectedEdge>> adjacency,
        int[] indices,
        int triangleCount,
        int seed,
        bool[] visited)
    {
        var component = new List<int>();
        var queue = new Queue<int>();
        queue.Enqueue(seed);
        visited[seed] = true;

        while (queue.Count > 0)
        {
            var triangleIndex = queue.Dequeue();
            component.Add(triangleIndex);

            var baseIndex = triangleIndex * 3;
            var edges = new (int A, int B)[]
            {
                (indices[baseIndex], indices[baseIndex + 1]),
                (indices[baseIndex + 1], indices[baseIndex + 2]),
                (indices[baseIndex + 2], indices[baseIndex])
            };

            foreach (var (a, b) in edges)
            {
                var key = UndirectedEdgeKey(a, b);
                if (!adjacency.TryGetValue(key, out var neighbors))
                {
                    continue;
                }

                foreach (var neighbor in neighbors)
                {
                    if (neighbor.TriangleIndex == triangleIndex || visited[neighbor.TriangleIndex])
                    {
                        continue;
                    }

                    visited[neighbor.TriangleIndex] = true;
                    queue.Enqueue(neighbor.TriangleIndex);
                }
            }
        }

        return component;
    }

    private static bool ShouldFlipComponent(
        Point3D[] positions,
        int[] indices,
        List<int> component,
        Vector3D preferredDirection)
    {
        if (component.Count == 0)
        {
            return false;
        }

        var useWorkingSide = preferredDirection.LengthSquared > 1e-12;
        if (useWorkingSide)
        {
            preferredDirection.Normalize();
        }

        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var minZ = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;
        var maxZ = double.NegativeInfinity;

        foreach (var triangleIndex in component)
        {
            var baseIndex = triangleIndex * 3;
            var i0 = indices[baseIndex];
            var i1 = indices[baseIndex + 1];
            var i2 = indices[baseIndex + 2];
            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= positions.Length || i1 >= positions.Length || i2 >= positions.Length)
            {
                continue;
            }

            var p0 = positions[i0];
            var p1 = positions[i1];
            var p2 = positions[i2];
            minX = Math.Min(minX, Math.Min(p0.X, Math.Min(p1.X, p2.X)));
            minY = Math.Min(minY, Math.Min(p0.Y, Math.Min(p1.Y, p2.Y)));
            minZ = Math.Min(minZ, Math.Min(p0.Z, Math.Min(p1.Z, p2.Z)));
            maxX = Math.Max(maxX, Math.Max(p0.X, Math.Max(p1.X, p2.X)));
            maxY = Math.Max(maxY, Math.Max(p0.Y, Math.Max(p1.Y, p2.Y)));
            maxZ = Math.Max(maxZ, Math.Max(p0.Z, Math.Max(p1.Z, p2.Z)));
        }

        var componentCenter = new Point3D(
            (minX + maxX) * 0.5,
            (minY + maxY) * 0.5,
            (minZ + maxZ) * 0.5);

        var alignedArea = 0.0;
        var opposedArea = 0.0;

        foreach (var triangleIndex in component)
        {
            var baseIndex = triangleIndex * 3;
            var i0 = indices[baseIndex];
            var i1 = indices[baseIndex + 1];
            var i2 = indices[baseIndex + 2];
            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= positions.Length || i1 >= positions.Length || i2 >= positions.Length)
            {
                continue;
            }

            var p0 = positions[i0];
            var p1 = positions[i1];
            var p2 = positions[i2];

            var normal = ComputeFaceNormal(p0, p1, p2);
            var area = normal.Length;
            if (area <= 1e-12)
            {
                continue;
            }

            normal /= area;
            double alignment;
            if (useWorkingSide)
            {
                alignment = Vector3D.DotProduct(normal, preferredDirection);
            }
            else
            {
                var centroid = new Point3D(
                    (p0.X + p1.X + p2.X) / 3.0,
                    (p0.Y + p1.Y + p2.Y) / 3.0,
                    (p0.Z + p1.Z + p2.Z) / 3.0);
                var toCenter = componentCenter - centroid;
                if (toCenter.LengthSquared < 1e-12)
                {
                    continue;
                }

                toCenter.Normalize();
                alignment = -Vector3D.DotProduct(normal, toCenter);
            }

            if (alignment >= 0)
            {
                alignedArea += area;
            }
            else
            {
                opposedArea += area;
            }
        }

        return opposedArea > alignedArea;
    }

    public static MeshSnapshot RepairAfterFuse(MeshSnapshot mesh)
    {
        return MakeAdjacentWindingConsistent(mesh);
    }

    private static bool ShouldFlipRelativeToReference(MeshSnapshot candidate, MeshSnapshot reference)
    {
        var overlap = IntersectBounds(candidate.Bounds, reference.Bounds);
        if (overlap.IsEmpty)
        {
            return IsMeshPredominantlyInverted(candidate);
        }

        var sampleDistance = Math.Max(
            Math.Max(overlap.SizeX, overlap.SizeY),
            overlap.SizeZ) / 40.0;
        sampleDistance = Math.Clamp(sampleDistance, 0.05, 2.5);

        var referenceSamples = BuildTriangleSamples(reference, overlap, sampleDistance * 2.5);
        if (referenceSamples.Count == 0)
        {
            return IsMeshPredominantlyInverted(candidate);
        }

        var aligned = 0;
        var opposed = 0;
        var stride = Math.Max(1, CountTriangles(candidate) / MaxSamplesPerMesh);

        var sampleIndex = 0;
        foreach (var (centroid, normal) in EnumerateTriangleFrames(candidate))
        {
            if (sampleIndex++ % stride != 0)
            {
                continue;
            }

            if (!IsPointInsideExpandedBounds(centroid, overlap, sampleDistance))
            {
                continue;
            }

            if (!TryFindClosestSample(referenceSamples, centroid, sampleDistance, out var closestNormal))
            {
                continue;
            }

            var alignment = Vector3D.DotProduct(normal, closestNormal);
            if (alignment > 0.35)
            {
                aligned++;
            }
            else if (alignment < -0.35)
            {
                opposed++;
            }
        }

        if (aligned + opposed < 8)
        {
            return IsMeshPredominantlyInverted(candidate);
        }

        // Exterior shells in contact should have opposing normals — aligned means inverted.
        return aligned > opposed;
    }

    private static bool IsMeshPredominantlyInverted(MeshSnapshot mesh)
    {
        var bounds = mesh.Bounds;
        if (bounds.IsEmpty)
        {
            return false;
        }

        var center = new Point3D(
            bounds.X + (bounds.SizeX * 0.5),
            bounds.Y + (bounds.SizeY * 0.5),
            bounds.Z + (bounds.SizeZ * 0.5));

        var outward = 0;
        var inward = 0;
        var stride = Math.Max(1, CountTriangles(mesh) / MaxSamplesPerMesh);
        var sampleIndex = 0;

        foreach (var (centroid, normal) in EnumerateTriangleFrames(mesh))
        {
            if (sampleIndex++ % stride != 0)
            {
                continue;
            }

            if (normal.LengthSquared < 1e-12)
            {
                continue;
            }

            var toCenter = center - centroid;
            if (toCenter.LengthSquared < 1e-12)
            {
                continue;
            }

            toCenter.Normalize();
            if (Vector3D.DotProduct(normal, toCenter) > 0)
            {
                inward++;
            }
            else
            {
                outward++;
            }
        }

        return inward > outward;
    }

    private static MeshSnapshot MakeAdjacentWindingConsistent(MeshSnapshot mesh)
    {
        var positions = mesh.Positions.ToArray();
        var indices = mesh.TriangleIndices.Length >= 3
            ? mesh.TriangleIndices.ToArray()
            : BuildSoupIndices(positions.Length);

        var triangleCount = indices.Length / 3;
        if (triangleCount == 0)
        {
            return mesh;
        }

        var adjacency = BuildTriangleAdjacency(indices, triangleCount);
        var enqueued = new bool[triangleCount];

        for (var seed = 0; seed < triangleCount; seed++)
        {
            if (enqueued[seed])
            {
                continue;
            }

            var queue = new Queue<int>();
            queue.Enqueue(seed);
            enqueued[seed] = true;

            while (queue.Count > 0)
            {
                var triangleIndex = queue.Dequeue();
                var baseIndex = triangleIndex * 3;
                var edges = new (int A, int B)[]
                {
                    (indices[baseIndex], indices[baseIndex + 1]),
                    (indices[baseIndex + 1], indices[baseIndex + 2]),
                    (indices[baseIndex + 2], indices[baseIndex])
                };

                foreach (var (a, b) in edges)
                {
                    var key = UndirectedEdgeKey(a, b);
                    if (!adjacency.TryGetValue(key, out var neighbors))
                    {
                        continue;
                    }

                    foreach (var neighbor in neighbors)
                    {
                        if (neighbor.TriangleIndex == triangleIndex)
                        {
                            continue;
                        }

                        // Shared edge should be traversed in opposite directions.
                        if (neighbor.From == a && neighbor.To == b)
                        {
                            FlipTriangle(indices, neighbor.TriangleIndex);
                        }

                        if (!enqueued[neighbor.TriangleIndex])
                        {
                            enqueued[neighbor.TriangleIndex] = true;
                            queue.Enqueue(neighbor.TriangleIndex);
                        }
                    }
                }
            }
        }

        return new MeshSnapshot(positions, indices);
    }

    private static Dictionary<long, List<DirectedEdge>> BuildTriangleAdjacency(int[] indices, int triangleCount)
    {
        var adjacency = new Dictionary<long, List<DirectedEdge>>();

        for (var triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            var baseIndex = triangleIndex * 3;
            AddDirectedEdge(adjacency, indices[baseIndex], indices[baseIndex + 1], triangleIndex);
            AddDirectedEdge(adjacency, indices[baseIndex + 1], indices[baseIndex + 2], triangleIndex);
            AddDirectedEdge(adjacency, indices[baseIndex + 2], indices[baseIndex], triangleIndex);
        }

        return adjacency;
    }

    private static void AddDirectedEdge(
        Dictionary<long, List<DirectedEdge>> adjacency,
        int from,
        int to,
        int triangleIndex)
    {
        var key = UndirectedEdgeKey(from, to);
        if (!adjacency.TryGetValue(key, out var list))
        {
            list = new List<DirectedEdge>();
            adjacency[key] = list;
        }

        list.Add(new DirectedEdge(triangleIndex, from, to));
    }

    private static long UndirectedEdgeKey(int a, int b)
    {
        var min = Math.Min(a, b);
        var max = Math.Max(a, b);
        return ((long)min << 32) | (uint)max;
    }

    private static void FlipTriangle(int[] indices, int triangleIndex)
    {
        var baseIndex = triangleIndex * 3;
        (indices[baseIndex + 1], indices[baseIndex + 2]) = (indices[baseIndex + 2], indices[baseIndex + 1]);
    }

    private static MeshSnapshot FlipWinding(MeshSnapshot mesh)
    {
        var indices = mesh.TriangleIndices.Length >= 3
            ? mesh.TriangleIndices.ToArray()
            : BuildSoupIndices(mesh.Positions.Length);

        for (var index = 0; index + 2 < indices.Length; index += 3)
        {
            (indices[index + 1], indices[index + 2]) = (indices[index + 2], indices[index + 1]);
        }

        return new MeshSnapshot(mesh.Positions.ToArray(), indices);
    }

    private static List<(Point3D Centroid, Vector3D Normal)> BuildTriangleSamples(
        MeshSnapshot mesh,
        Rect3D overlap,
        double maxDistance)
    {
        var samples = new List<(Point3D, Vector3D)>();
        var stride = Math.Max(1, CountTriangles(mesh) / MaxSamplesPerMesh);
        var sampleIndex = 0;

        foreach (var frame in EnumerateTriangleFrames(mesh))
        {
            if (sampleIndex++ % stride != 0)
            {
                continue;
            }

            if (!IsPointInsideExpandedBounds(frame.Centroid, overlap, maxDistance))
            {
                continue;
            }

            samples.Add(frame);
        }

        if (samples.Count == 0)
        {
            foreach (var frame in EnumerateTriangleFrames(mesh))
            {
                samples.Add(frame);
                if (samples.Count >= 2048)
                {
                    break;
                }
            }
        }

        return samples;
    }

    private static bool TryFindClosestSample(
        IReadOnlyList<(Point3D Centroid, Vector3D Normal)> samples,
        Point3D point,
        double maxDistance,
        out Vector3D normal)
    {
        normal = default;
        var maxDistanceSq = maxDistance * maxDistance;
        var bestDistanceSq = double.PositiveInfinity;

        foreach (var (centroid, sampleNormal) in samples)
        {
            var dx = centroid.X - point.X;
            var dy = centroid.Y - point.Y;
            var dz = centroid.Z - point.Z;
            var distanceSq = (dx * dx) + (dy * dy) + (dz * dz);
            if (distanceSq >= bestDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            normal = sampleNormal;
        }

        return bestDistanceSq <= maxDistanceSq;
    }

    private static IEnumerable<(Point3D Centroid, Vector3D Normal)> EnumerateTriangleFrames(MeshSnapshot mesh)
    {
        foreach (var (i0, i1, i2) in VoxelUnionGrid.EnumerateTriangleIndices(mesh))
        {
            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= mesh.Positions.Length || i1 >= mesh.Positions.Length || i2 >= mesh.Positions.Length)
            {
                continue;
            }

            var p0 = mesh.Positions[i0];
            var p1 = mesh.Positions[i1];
            var p2 = mesh.Positions[i2];
            var centroid = new Point3D(
                (p0.X + p1.X + p2.X) / 3.0,
                (p0.Y + p1.Y + p2.Y) / 3.0,
                (p0.Z + p1.Z + p2.Z) / 3.0);
            var normal = ComputeFaceNormal(p0, p1, p2);
            if (normal.LengthSquared > 1e-12)
            {
                normal.Normalize();
            }

            yield return (centroid, normal);
        }
    }

    private static Vector3D ComputeFaceNormal(Point3D p0, Point3D p1, Point3D p2)
    {
        var ux = p1.X - p0.X;
        var uy = p1.Y - p0.Y;
        var uz = p1.Z - p0.Z;
        var vx = p2.X - p0.X;
        var vy = p2.Y - p0.Y;
        var vz = p2.Z - p0.Z;
        return new Vector3D(
            (uy * vz) - (uz * vy),
            (uz * vx) - (ux * vz),
            (ux * vy) - (uy * vx));
    }

    private static Rect3D IntersectBounds(Rect3D a, Rect3D b)
    {
        if (a.IsEmpty || b.IsEmpty)
        {
            return Rect3D.Empty;
        }

        var minX = Math.Max(a.X, b.X);
        var minY = Math.Max(a.Y, b.Y);
        var minZ = Math.Max(a.Z, b.Z);
        var maxX = Math.Min(a.X + a.SizeX, b.X + b.SizeX);
        var maxY = Math.Min(a.Y + a.SizeY, b.Y + b.SizeY);
        var maxZ = Math.Min(a.Z + a.SizeZ, b.Z + b.SizeZ);

        if (maxX <= minX || maxY <= minY || maxZ <= minZ)
        {
            return Rect3D.Empty;
        }

        return new Rect3D(minX, minY, minZ, maxX - minX, maxY - minY, maxZ - minZ);
    }

    private static bool IsPointInsideExpandedBounds(Point3D point, Rect3D bounds, double padding)
    {
        return point.X >= bounds.X - padding &&
               point.Y >= bounds.Y - padding &&
               point.Z >= bounds.Z - padding &&
               point.X <= bounds.X + bounds.SizeX + padding &&
               point.Y <= bounds.Y + bounds.SizeY + padding &&
               point.Z <= bounds.Z + bounds.SizeZ + padding;
    }

    private static int[] BuildSoupIndices(int positionCount)
    {
        var count = Math.Max(0, positionCount / 3) * 3;
        var indices = new int[count];
        for (var index = 0; index < count; index++)
        {
            indices[index] = index;
        }

        return indices;
    }

    private static int CountTriangles(MeshSnapshot mesh)
    {
        return mesh.TriangleIndices.Length >= 3
            ? mesh.TriangleIndices.Length / 3
            : mesh.Positions.Length / 3;
    }

    private readonly struct DirectedEdge(int triangleIndex, int from, int to)
    {
        public int TriangleIndex { get; } = triangleIndex;
        public int From { get; } = from;
        public int To { get; } = to;
    }
}
