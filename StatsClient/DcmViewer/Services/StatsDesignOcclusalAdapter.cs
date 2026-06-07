using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>Shaves occlusal cusp height where the crown intrudes into opposing-arch scan data.</summary>
internal static class StatsDesignOcclusalAdapter
{
    public sealed class Result
    {
        public required MeshSnapshot Mesh { get; init; }
        public int AdjustedVertexCount { get; init; }
    }

    public static Result Adapt(
        MeshSnapshot crownMesh,
        IReadOnlyList<MeshSnapshot> opposingMeshes,
        IReadOnlyList<Point3D> marginLoop,
        Vector3D insertionAxis,
        double clearanceMm)
    {
        ArgumentNullException.ThrowIfNull(crownMesh);
        if (opposingMeshes.Count == 0 || marginLoop.Count < 3)
        {
            return new Result { Mesh = crownMesh, AdjustedVertexCount = 0 };
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
        var reference = margin.Centroid;
        var opposingIndices = opposingMeshes.Select(mesh => new StatsDesignMeshSpatialIndex(mesh)).ToArray();
        var positions = crownMesh.Positions.ToArray();
        var indices = crownMesh.TriangleIndices.ToArray();
        var normals = ComputeVertexNormals(positions, indices);
        var clearance = Math.Max(clearanceMm, 0.0);
        var adjusted = new HashSet<int>();

        for (var index = 0; index < positions.Length; index++)
        {
            if (Vector3D.DotProduct(normals[index], axis) < 0.25)
            {
                continue;
            }

            var vertex = positions[index];
            var closest = ClosestOnOpposing(vertex, opposingMeshes, opposingIndices);
            if (closest is null)
            {
                continue;
            }

            var tVertex = Vector3D.DotProduct(vertex - reference, axis);
            var tOpposing = Vector3D.DotProduct(closest.Value.Point - reference, axis);
            var targetT = tOpposing - clearance;
            if (tVertex <= targetT + 1e-4)
            {
                continue;
            }

            var excess = tVertex - targetT;
            positions[index] = vertex - (axis * excess);
            adjusted.Add(index);
        }

        if (adjusted.Count > 0)
        {
            StatsDesignMeshSmoothing.LaplacianFair(positions, indices, adjusted, passes: 2, lambda: 0.38, expandRings: 1);
        }

        return new Result
        {
            Mesh = new MeshSnapshot(positions, indices),
            AdjustedVertexCount = adjusted.Count
        };
    }

    private static StatsDesignMeshProximity.ClosestPointResult? ClosestOnOpposing(
        Point3D point,
        IReadOnlyList<MeshSnapshot> opposingMeshes,
        StatsDesignMeshSpatialIndex[] opposingIndices)
    {
        StatsDesignMeshProximity.ClosestPointResult? best = null;
        for (var index = 0; index < opposingMeshes.Count; index++)
        {
            var candidate = opposingIndices[index].ClosestPointOnMesh(point, searchRadiusMm: 8.0);
            if (best is null || candidate.DistanceMm < best.Value.DistanceMm)
            {
                best = candidate;
            }
        }

        return best;
    }

    private static Vector3D[] ComputeVertexNormals(Point3D[] positions, int[] triangleIndices)
    {
        var normals = new Vector3D[positions.Length];
        for (var triangle = 0; triangle + 2 < triangleIndices.Length; triangle += 3)
        {
            var i0 = triangleIndices[triangle];
            var i1 = triangleIndices[triangle + 1];
            var i2 = triangleIndices[triangle + 2];
            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= positions.Length || i1 >= positions.Length || i2 >= positions.Length)
            {
                continue;
            }

            var faceNormal = Vector3D.CrossProduct(positions[i1] - positions[i0], positions[i2] - positions[i0]);
            normals[i0] += faceNormal;
            normals[i1] += faceNormal;
            normals[i2] += faceNormal;
        }

        for (var index = 0; index < normals.Length; index++)
        {
            if (normals[index].LengthSquared > 1e-12)
            {
                normals[index].Normalize();
            }
            else
            {
                normals[index] = new Vector3D(0, 0, 1);
            }
        }

        return normals;
    }
}
