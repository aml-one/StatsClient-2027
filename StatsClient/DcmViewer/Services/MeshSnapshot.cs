using System.Windows;
using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>
/// Renderer-neutral mesh payload used for parsing, export, and section math.
/// </summary>
public sealed record MeshSnapshot(Point3D[] Positions, int[] TriangleIndices)
{
    public static MeshSnapshot FromLists(IReadOnlyList<Point3D> positions, IReadOnlyList<int> triangleIndices)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(triangleIndices);

        return new MeshSnapshot(positions.ToArray(), triangleIndices.ToArray());
    }

    public Rect3D Bounds => ComputeBounds(Positions);

    public int VertexCount => Positions.Length;

    public int TriangleCount => TriangleIndices.Length / 3;

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
}
