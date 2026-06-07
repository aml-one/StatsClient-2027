using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>Topological Laplacian fairing for organic transitions after trims, stitches, and sculpt strokes.</summary>
internal static class StatsDesignMeshSmoothing
{
    /// <summary>
    /// Standard Laplacian: each target vertex moves toward the average of its full one-ring neighbors.
    /// </summary>
    public static void LaplacianFair(
        Point3D[] positions,
        int[] triangleIndices,
        IReadOnlyCollection<int> targetVertices,
        int passes = 3,
        double lambda = 0.5,
        int expandRings = 0)
    {
        if (positions.Length == 0 || targetVertices.Count == 0 || passes <= 0)
        {
            return;
        }

        var targets = targetVertices as HashSet<int> ?? targetVertices.ToHashSet();
        var neighbors = BuildAdjacency(positions.Length, triangleIndices);
        if (expandRings > 0)
        {
            targets = ExpandVertexRings(targets, neighbors, expandRings);
        }

        var clampedLambda = Math.Clamp(lambda, 0.05, 1.0);

        for (var pass = 0; pass < passes; pass++)
        {
            var next = new Point3D[positions.Length];
            Array.Copy(positions, next, positions.Length);

            foreach (var index in targets)
            {
                var nbrs = neighbors[index];
                if (nbrs.Count == 0)
                {
                    continue;
                }

                var avgX = 0.0;
                var avgY = 0.0;
                var avgZ = 0.0;
                foreach (var neighborIndex in nbrs)
                {
                    avgX += positions[neighborIndex].X;
                    avgY += positions[neighborIndex].Y;
                    avgZ += positions[neighborIndex].Z;
                }

                avgX /= nbrs.Count;
                avgY /= nbrs.Count;
                avgZ /= nbrs.Count;

                next[index] = new Point3D(
                    positions[index].X + (clampedLambda * (avgX - positions[index].X)),
                    positions[index].Y + (clampedLambda * (avgY - positions[index].Y)),
                    positions[index].Z + (clampedLambda * (avgZ - positions[index].Z)));
            }

            foreach (var index in targets)
            {
                positions[index] = next[index];
            }
        }
    }

    /// <summary>Legacy helper — fairing restricted to neighbors inside the same affected set.</summary>
    public static void LaplacianSmooth(
        Point3D[] positions,
        int[] triangleIndices,
        IReadOnlyCollection<int> affectedVertices,
        int passes,
        double strength) =>
        LaplacianFair(positions, triangleIndices, affectedVertices, passes, strength, expandRings: 0);

    public static HashSet<int> ExpandVertexRings(
        HashSet<int> seeds,
        IReadOnlyList<HashSet<int>> neighbors,
        int rings)
    {
        var expanded = new HashSet<int>(seeds);
        var frontier = new HashSet<int>(seeds);
        for (var ring = 0; ring < rings; ring++)
        {
            var nextFrontier = new HashSet<int>();
            foreach (var index in frontier)
            {
                if (index < 0 || index >= neighbors.Count)
                {
                    continue;
                }

                foreach (var neighborIndex in neighbors[index])
                {
                    if (expanded.Add(neighborIndex))
                    {
                        nextFrontier.Add(neighborIndex);
                    }
                }
            }

            frontier = nextFrontier;
            if (frontier.Count == 0)
            {
                break;
            }
        }

        return expanded;
    }

    public static HashSet<int> CollectChangedVertices(
        Point3D[] before,
        Point3D[] after,
        double epsilonMm = 0.001)
    {
        var changed = new HashSet<int>();
        var count = Math.Min(before.Length, after.Length);
        var epsilonSq = epsilonMm * epsilonMm;
        for (var index = 0; index < count; index++)
        {
            var delta = after[index] - before[index];
            if (delta.LengthSquared > epsilonSq)
            {
                changed.Add(index);
            }
        }

        return changed;
    }

    private static List<HashSet<int>> BuildAdjacency(int vertexCount, int[] indices)
    {
        var neighbors = new List<HashSet<int>>(vertexCount);
        for (var index = 0; index < vertexCount; index++)
        {
            neighbors.Add(new HashSet<int>());
        }

        for (var triangle = 0; triangle + 2 < indices.Length; triangle += 3)
        {
            var i0 = indices[triangle];
            var i1 = indices[triangle + 1];
            var i2 = indices[triangle + 2];
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
}
