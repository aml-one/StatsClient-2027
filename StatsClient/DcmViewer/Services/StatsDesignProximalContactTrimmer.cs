using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>Projects proximal side-wall vertices off adjacent scan collisions.</summary>
internal static class StatsDesignProximalContactTrimmer
{
    public sealed class Result
    {
        public required MeshSnapshot Mesh { get; init; }
        public int TrimmedVertexCount { get; init; }
    }

    public static Result Trim(
        MeshSnapshot crownMesh,
        IReadOnlyList<MeshSnapshot> adjacentMeshes,
        Vector3D insertionAxis,
        double safetyBufferMm)
    {
        ArgumentNullException.ThrowIfNull(crownMesh);
        if (adjacentMeshes.Count == 0)
        {
            return new Result { Mesh = crownMesh, TrimmedVertexCount = 0 };
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

        var indices = adjacentMeshes.Select(mesh => new StatsDesignMeshSpatialIndex(mesh)).ToArray();
        var positions = crownMesh.Positions.ToArray();
        var triangleIndices = crownMesh.TriangleIndices.ToArray();
        var normals = ComputeVertexNormals(positions, triangleIndices);
        var buffer = Math.Max(safetyBufferMm, 0.005);
        var trimmed = new HashSet<int>();

        for (var vertexIndex = 0; vertexIndex < positions.Length; vertexIndex++)
        {
            if (Math.Abs(Vector3D.DotProduct(normals[vertexIndex], axis)) > 0.55)
            {
                continue;
            }

            var vertex = positions[vertexIndex];
            var normal = normals[vertexIndex];
            var closest = StatsDesignMeshProximity.ClosestPointOnMesh(vertex, adjacentMeshes[0], indices[0]);
            for (var meshIndex = 1; meshIndex < adjacentMeshes.Count; meshIndex++)
            {
                var candidate = indices[meshIndex].ClosestPointOnMesh(vertex, searchRadiusMm: 3.0);
                if (candidate.DistanceMm < closest.DistanceMm)
                {
                    closest = candidate;
                }
            }

            if (closest.DistanceMm > 0.35)
            {
                continue;
            }

            var heading = closest.Point - vertex;
            if (Vector3D.DotProduct(heading, normal) >= 0.0)
            {
                continue;
            }

            positions[vertexIndex] = closest.Point + (normal * buffer);
            trimmed.Add(vertexIndex);
        }

        if (trimmed.Count > 0)
        {
            StatsDesignMeshSmoothing.LaplacianFair(positions, triangleIndices, trimmed, passes: 2, lambda: 0.4, expandRings: 1);
        }

        return new Result
        {
            Mesh = new MeshSnapshot(positions, triangleIndices),
            TrimmedVertexCount = trimmed.Count
        };
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
                normals[index] = new Vector3D(0, 1, 0);
            }
        }

        return normals;
    }
}
