using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>
/// Removes seat-path undercuts by laterally shifting shadowed vertices along the insertion axis.
/// </summary>
internal static class StatsDesignUndercutRemover
{
    public sealed class Result
    {
        public required MeshSnapshot Mesh { get; init; }
        public int AdjustedVertexCount { get; init; }
        public int IterationsUsed { get; init; }
    }

    public static Result RemoveSeatPathUndercuts(
        MeshSnapshot mesh,
        IReadOnlyList<Point3D> marginLoop,
        Vector3D insertionAxis,
        int maxIterations = 2,
        bool intaglioBandOnly = true)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (marginLoop.Count < 3)
        {
            return new Result { Mesh = mesh, AdjustedVertexCount = 0, IterationsUsed = 0 };
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
        var positions = mesh.Positions.ToArray();
        var indices = mesh.TriangleIndices.ToArray();
        var spatial = new StatsDesignMeshSpatialIndex(mesh);
        var workZone = IdentifyWorkZone(positions, margin, axis, intaglioBandOnly);
        var adjustedTotal = 0;
        var iterationsUsed = 0;

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            var adjustedThisPass = 0;
            foreach (var vertexIndex in workZone)
            {
                var vertex = positions[vertexIndex];
                if (!margin.ContainsPoint(vertex))
                {
                    continue;
                }

                var rayOrigin = vertex + (axis * 0.02);
                if (!spatial.TryRaycast(rayOrigin, axis, maxDistanceMm: 18.0, out var hit, skipVertexIndex: vertexIndex))
                {
                    continue;
                }

                var correction = hit.Point - vertex;
                var axial = Vector3D.DotProduct(correction, axis);
                var lateral = correction - (axis * axial);
                if (lateral.LengthSquared < 1e-10)
                {
                    continue;
                }

                positions[vertexIndex] = vertex + lateral;
                adjustedThisPass++;
            }

            if (adjustedThisPass == 0)
            {
                break;
            }

            adjustedTotal += adjustedThisPass;
            iterationsUsed++;
            spatial = new StatsDesignMeshSpatialIndex(new MeshSnapshot(positions, indices));
        }

        if (adjustedTotal > 0)
        {
            StatsDesignMeshSmoothing.LaplacianFair(positions, indices, workZone, passes: 2, lambda: 0.42, expandRings: 1);
        }

        return new Result
        {
            Mesh = new MeshSnapshot(positions, indices),
            AdjustedVertexCount = adjustedTotal,
            IterationsUsed = iterationsUsed
        };
    }

    private static HashSet<int> IdentifyWorkZone(
        Point3D[] positions,
        StatsDesignMarginGeometry margin,
        Vector3D insertionAxis,
        bool intaglioBandOnly)
    {
        var zone = new HashSet<int>();
        if (!intaglioBandOnly)
        {
            for (var index = 0; index < positions.Length; index++)
            {
                if (margin.ContainsPoint(positions[index]))
                {
                    zone.Add(index);
                }
            }

            return zone;
        }

        var projections = new double[positions.Length];
        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        for (var index = 0; index < positions.Length; index++)
        {
            if (!margin.ContainsPoint(positions[index]))
            {
                projections[index] = double.NaN;
                continue;
            }

            var t = Vector3D.DotProduct(positions[index] - margin.Centroid, insertionAxis);
            projections[index] = t;
            min = Math.Min(min, t);
            max = Math.Max(max, t);
        }

        var range = max - min;
        if (range < 1e-6)
        {
            return zone;
        }

        var threshold = min + (range * 0.42);
        for (var index = 0; index < positions.Length; index++)
        {
            if (!double.IsNaN(projections[index]) && projections[index] <= threshold)
            {
                zone.Add(index);
            }
        }

        return zone;
    }
}
