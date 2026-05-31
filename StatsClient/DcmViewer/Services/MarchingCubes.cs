using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>Marching cubes isosurface extraction (Paul Bourke / ch3mbot tables).</summary>
internal static class MarchingCubes
{
    private const float IsoThreshold = 0.5f;

    public static MeshSnapshot ExtractSurface(bool[,,] occupied, double originX, double originY, double originZ, double voxelSize)
    {
        var nx = occupied.GetLength(0);
        var ny = occupied.GetLength(1);
        var nz = occupied.GetLength(2);
        if (nx < 2 || ny < 2 || nz < 2)
        {
            throw new InvalidOperationException("Voxel grid is too small to extract a surface.");
        }

        var density = new float[nx, ny, nz];
        for (var x = 0; x < nx; x++)
        {
            for (var y = 0; y < ny; y++)
            {
                for (var z = 0; z < nz; z++)
                {
                    density[x, y, z] = occupied[x, y, z] ? 1f : 0f;
                }
            }
        }

        var positions = new List<Point3D>();
        var indices = new List<int>();
        var vertexCache = new Dictionary<(int X, int Y, int Z, int Edge), int>();

        for (var x = 0; x < nx - 1; x++)
        {
            for (var y = 0; y < ny - 1; y++)
            {
                for (var z = 0; z < nz - 1; z++)
                {
                    AppendCellTriangles(
                        density,
                        x,
                        y,
                        z,
                        originX,
                        originY,
                        originZ,
                        voxelSize,
                        positions,
                        indices,
                        vertexCache);
                }
            }
        }

        if (indices.Count < 3)
        {
            throw new InvalidOperationException("Marching cubes did not produce any triangles.");
        }

        return new MeshSnapshot(positions.ToArray(), indices.ToArray());
    }

    private static void AppendCellTriangles(
        float[,,] density,
        int x,
        int y,
        int z,
        double originX,
        double originY,
        double originZ,
        double voxelSize,
        List<Point3D> positions,
        List<int> indices,
        Dictionary<(int X, int Y, int Z, int Edge), int> vertexCache)
    {
        var points = new[]
        {
            GridPoint(originX, originY, originZ, voxelSize, x, y, z),
            GridPoint(originX, originY, originZ, voxelSize, x + 1, y, z),
            GridPoint(originX, originY, originZ, voxelSize, x + 1, y, z + 1),
            GridPoint(originX, originY, originZ, voxelSize, x, y, z + 1),
            GridPoint(originX, originY, originZ, voxelSize, x, y + 1, z),
            GridPoint(originX, originY, originZ, voxelSize, x + 1, y + 1, z),
            GridPoint(originX, originY, originZ, voxelSize, x + 1, y + 1, z + 1),
            GridPoint(originX, originY, originZ, voxelSize, x, y + 1, z + 1)
        };

        var values = new[]
        {
            density[x, y, z],
            density[x + 1, y, z],
            density[x + 1, y, z + 1],
            density[x, y, z + 1],
            density[x, y + 1, z],
            density[x + 1, y + 1, z],
            density[x + 1, y + 1, z + 1],
            density[x, y + 1, z + 1]
        };

        var cubeIndex = 0;
        if (values[0] < IsoThreshold) cubeIndex |= 1;
        if (values[1] < IsoThreshold) cubeIndex |= 2;
        if (values[2] < IsoThreshold) cubeIndex |= 4;
        if (values[3] < IsoThreshold) cubeIndex |= 8;
        if (values[4] < IsoThreshold) cubeIndex |= 16;
        if (values[5] < IsoThreshold) cubeIndex |= 32;
        if (values[6] < IsoThreshold) cubeIndex |= 64;
        if (values[7] < IsoThreshold) cubeIndex |= 128;

        if (cubeIndex == 0 || cubeIndex == 255)
        {
            return;
        }

        var edgeMask = MarchingCubesTables.EdgeTable[cubeIndex];
        if (edgeMask == 0)
        {
            return;
        }

        var edgeVertices = new Point3D?[12];
        if ((edgeMask & 1) != 0) edgeVertices[0] = Interpolate(points[0], points[1], values[0], values[1]);
        if ((edgeMask & 2) != 0) edgeVertices[1] = Interpolate(points[1], points[2], values[1], values[2]);
        if ((edgeMask & 4) != 0) edgeVertices[2] = Interpolate(points[2], points[3], values[2], values[3]);
        if ((edgeMask & 8) != 0) edgeVertices[3] = Interpolate(points[3], points[0], values[3], values[0]);
        if ((edgeMask & 16) != 0) edgeVertices[4] = Interpolate(points[4], points[5], values[4], values[5]);
        if ((edgeMask & 32) != 0) edgeVertices[5] = Interpolate(points[5], points[6], values[5], values[6]);
        if ((edgeMask & 64) != 0) edgeVertices[6] = Interpolate(points[6], points[7], values[6], values[7]);
        if ((edgeMask & 128) != 0) edgeVertices[7] = Interpolate(points[7], points[4], values[7], values[4]);
        if ((edgeMask & 256) != 0) edgeVertices[8] = Interpolate(points[0], points[4], values[0], values[4]);
        if ((edgeMask & 512) != 0) edgeVertices[9] = Interpolate(points[1], points[5], values[1], values[5]);
        if ((edgeMask & 1024) != 0) edgeVertices[10] = Interpolate(points[2], points[6], values[2], values[6]);
        if ((edgeMask & 2048) != 0) edgeVertices[11] = Interpolate(points[3], points[7], values[3], values[7]);

        for (var i = 0; MarchingCubesTables.TriangleTable[cubeIndex, i] != -1; i += 3)
        {
            var e0 = MarchingCubesTables.TriangleTable[cubeIndex, i];
            var e1 = MarchingCubesTables.TriangleTable[cubeIndex, i + 1];
            var e2 = MarchingCubesTables.TriangleTable[cubeIndex, i + 2];

            indices.Add(GetEdgeVertex(x, y, z, e0, edgeVertices, positions, vertexCache));
            indices.Add(GetEdgeVertex(x, y, z, e1, edgeVertices, positions, vertexCache));
            indices.Add(GetEdgeVertex(x, y, z, e2, edgeVertices, positions, vertexCache));
        }
    }

    private static int GetEdgeVertex(
        int x,
        int y,
        int z,
        int edge,
        Point3D?[] edgeVertices,
        List<Point3D> positions,
        Dictionary<(int X, int Y, int Z, int Edge), int> vertexCache)
    {
        var key = (x, y, z, edge);
        if (vertexCache.TryGetValue(key, out var index))
        {
            return index;
        }

        var point = edgeVertices[edge] ?? throw new InvalidOperationException($"Missing edge vertex for edge {edge}.");
        index = positions.Count;
        positions.Add(point);
        vertexCache[key] = index;
        return index;
    }

    private static Point3D GridPoint(double originX, double originY, double originZ, double voxelSize, int x, int y, int z)
        => new(originX + (x * voxelSize), originY + (y * voxelSize), originZ + (z * voxelSize));

    private static Point3D Interpolate(Point3D a, Point3D b, float valueA, float valueB)
    {
        if (Math.Abs(IsoThreshold - valueA) < 1e-6f)
        {
            return a;
        }

        if (Math.Abs(IsoThreshold - valueB) < 1e-6f)
        {
            return b;
        }

        if (Math.Abs(valueA - valueB) < 1e-6f)
        {
            return a;
        }

        var t = (IsoThreshold - valueA) / (valueB - valueA);
        return new Point3D(
            a.X + (t * (b.X - a.X)),
            a.Y + (t * (b.Y - a.Y)),
            a.Z + (t * (b.Z - a.Z)));
    }
}
