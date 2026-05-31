using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>Light Laplacian smoothing to reduce voxel stair-steps on fused meshes.</summary>
internal static class MeshSmoothing
{
    private const double Strength = 0.42;

    public static MeshSnapshot Smooth(MeshSnapshot mesh, int passes)
    {
        if (passes <= 0 || mesh.Positions.Length == 0)
        {
            return mesh;
        }

        var positions = mesh.Positions.ToArray();
        var neighbors = BuildAdjacency(mesh);

        for (var pass = 0; pass < passes; pass++)
        {
            var next = new Point3D[positions.Length];
            for (var i = 0; i < positions.Length; i++)
            {
                var nbrs = neighbors[i];
                if (nbrs.Count == 0)
                {
                    next[i] = positions[i];
                    continue;
                }

                var avgX = 0.0;
                var avgY = 0.0;
                var avgZ = 0.0;
                foreach (var j in nbrs)
                {
                    avgX += positions[j].X;
                    avgY += positions[j].Y;
                    avgZ += positions[j].Z;
                }

                avgX /= nbrs.Count;
                avgY /= nbrs.Count;
                avgZ /= nbrs.Count;

                next[i] = new Point3D(
                    positions[i].X + (Strength * (avgX - positions[i].X)),
                    positions[i].Y + (Strength * (avgY - positions[i].Y)),
                    positions[i].Z + (Strength * (avgZ - positions[i].Z)));
            }

            positions = next;
        }

        return new MeshSnapshot(positions, mesh.TriangleIndices);
    }

    private static List<HashSet<int>> BuildAdjacency(MeshSnapshot mesh)
    {
        var neighbors = new List<HashSet<int>>(mesh.Positions.Length);
        for (var i = 0; i < mesh.Positions.Length; i++)
        {
            neighbors.Add(new HashSet<int>());
        }

        foreach (var (i0, i1, i2) in EnumerateTriangleIndices(mesh))
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

    private static IEnumerable<(int I0, int I1, int I2)> EnumerateTriangleIndices(MeshSnapshot mesh)
    {
        if (mesh.TriangleIndices.Length >= 3)
        {
            for (var index = 0; index + 2 < mesh.TriangleIndices.Length; index += 3)
            {
                yield return (mesh.TriangleIndices[index], mesh.TriangleIndices[index + 1], mesh.TriangleIndices[index + 2]);
            }

            yield break;
        }

        for (var index = 0; index + 2 < mesh.Positions.Length; index += 3)
        {
            yield return (index, index + 1, index + 2);
        }
    }
}
