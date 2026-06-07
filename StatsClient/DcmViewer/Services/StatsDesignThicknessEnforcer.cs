using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>Pushes crown wall vertices outward when wall thickness to prep falls below a material minimum.</summary>
internal static class StatsDesignThicknessEnforcer
{
    public sealed class Result
    {
        public required MeshSnapshot Mesh { get; init; }
        public int AdjustedVertexCount { get; init; }
        public double MinimumObservedThicknessMm { get; init; }
    }

    public static Result Enforce(
        MeshSnapshot crownMesh,
        MeshSnapshot preparationMesh,
        double minThicknessMm)
    {
        ArgumentNullException.ThrowIfNull(crownMesh);
        ArgumentNullException.ThrowIfNull(preparationMesh);

        var minThickness = Math.Max(minThicknessMm, 0.1);
        var positions = crownMesh.Positions.ToArray();
        var normals = ComputeVertexNormals(positions, crownMesh.TriangleIndices);
        var prepIndex = new StatsDesignMeshSpatialIndex(preparationMesh);
        var adjusted = 0;
        var adjustedIndices = new HashSet<int>();
        var minObserved = double.PositiveInfinity;

        for (var index = 0; index < positions.Length; index++)
        {
            var vertex = positions[index];
            var closest = prepIndex.ClosestPointOnMesh(vertex);
            minObserved = Math.Min(minObserved, closest.DistanceMm);

            if (closest.DistanceMm >= minThickness - 1e-6)
            {
                continue;
            }

            var deficit = minThickness - closest.DistanceMm;
            var normal = normals[index];
            if (normal.LengthSquared < 1e-12)
            {
                var awayFromPrep = vertex - closest.Point;
                if (awayFromPrep.LengthSquared < 1e-12)
                {
                    awayFromPrep = new Vector3D(0, 0, 1);
                }

                normal = awayFromPrep;
            }

            normal.Normalize();
            positions[index] = vertex + (normal * deficit);
            adjusted++;
            adjustedIndices.Add(index);
        }

        if (adjustedIndices.Count > 0)
        {
            StatsDesignMeshSmoothing.LaplacianFair(
                positions,
                crownMesh.TriangleIndices.ToArray(),
                adjustedIndices,
                passes: 2,
                lambda: 0.35,
                expandRings: 1);
        }

        var mesh = new MeshSnapshot(positions, crownMesh.TriangleIndices.ToArray());
        return new Result
        {
            Mesh = mesh,
            AdjustedVertexCount = adjusted,
            MinimumObservedThicknessMm = double.IsPositiveInfinity(minObserved) ? 0 : minObserved
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

            var edge1 = positions[i1] - positions[i0];
            var edge2 = positions[i2] - positions[i0];
            var faceNormal = Vector3D.CrossProduct(edge1, edge2);
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
