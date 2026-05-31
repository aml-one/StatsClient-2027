using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>
/// Merges visible meshes into one STL by combining all source triangles (opaque, full detail).
/// </summary>
internal static class MeshCombineFuse
{
    public static MeshSnapshot Fuse(IReadOnlyList<MeshSnapshot> meshes)
    {
        ArgumentNullException.ThrowIfNull(meshes);

        if (meshes.Count == 0)
        {
            throw new InvalidOperationException("No mesh data is available to fuse.");
        }

        if (meshes.Count == 1)
        {
            return meshes[0];
        }

        var positions = new List<Point3D>();
        var indices = new List<int>();

        foreach (var mesh in meshes)
        {
            if (mesh.Positions.Length == 0)
            {
                continue;
            }

            var vertexOffset = positions.Count;
            foreach (var point in mesh.Positions)
            {
                positions.Add(point);
            }

            foreach (var (i0, i1, i2) in EnumerateTriangleIndices(mesh))
            {
                if (i0 < 0 || i1 < 0 || i2 < 0 ||
                    i0 >= mesh.Positions.Length || i1 >= mesh.Positions.Length || i2 >= mesh.Positions.Length)
                {
                    continue;
                }

                indices.Add(vertexOffset + i0);
                indices.Add(vertexOffset + i1);
                indices.Add(vertexOffset + i2);
            }
        }

        if (indices.Count < 3)
        {
            throw new InvalidOperationException("Combined meshes do not contain any triangles.");
        }

        return new MeshSnapshot(positions.ToArray(), indices.ToArray());
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
