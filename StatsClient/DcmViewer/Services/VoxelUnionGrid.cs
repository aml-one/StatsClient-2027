using System.Windows;
using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>
/// Builds a dilated voxel occupancy grid that unions visible mesh surfaces (shared by voxel and unified-shell fuse).
/// </summary>
internal static class VoxelUnionGrid
{
    private const int MaxTrianglesPerMesh = 500_000;

    internal sealed class Grid(int nx, int ny, int nz)
    {
        private readonly bool[,,] _occupied = new bool[nx, ny, nz];

        public int Nx { get; } = nx;
        public int Ny { get; } = ny;
        public int Nz { get; } = nz;

        public bool[,,] Occupied => _occupied;

        public void Set(int x, int y, int z, bool value) => _occupied[x, y, z] = value;

        public bool IsOccupied(int x, int y, int z) =>
            x >= 0 && y >= 0 && z >= 0 && x < Nx && y < Ny && z < Nz && _occupied[x, y, z];

        public bool AnyOccupied()
        {
            for (var x = 0; x < Nx; x++)
            {
                for (var y = 0; y < Ny; y++)
                {
                    for (var z = 0; z < Nz; z++)
                    {
                        if (_occupied[x, y, z])
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public void Dilate(int radius)
        {
            for (var pass = 0; pass < radius; pass++)
            {
                var copy = (bool[,,])_occupied.Clone();
                for (var x = 0; x < Nx; x++)
                {
                    for (var y = 0; y < Ny; y++)
                    {
                        for (var z = 0; z < Nz; z++)
                        {
                            if (!copy[x, y, z])
                            {
                                continue;
                            }

                            for (var dx = -1; dx <= 1; dx++)
                            {
                                for (var dy = -1; dy <= 1; dy++)
                                {
                                    for (var dz = -1; dz <= 1; dz++)
                                    {
                                        var nx = x + dx;
                                        var ny = y + dy;
                                        var nz = z + dz;
                                        if (nx >= 0 && ny >= 0 && nz >= 0 && nx < Nx && ny < Ny && nz < Nz)
                                        {
                                            _occupied[nx, ny, nz] = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    internal readonly struct BuildResult(Grid grid, Vector3D origin, double voxelSize)
    {
        public Grid Grid { get; } = grid;
        public Vector3D Origin { get; } = origin;
        public double VoxelSize { get; } = voxelSize;
    }

    public static BuildResult Build(IReadOnlyList<MeshSnapshot> meshes, MeshFuseOptions options)
    {
        ArgumentNullException.ThrowIfNull(meshes);

        var bounds = ComputeCombinedBounds(meshes);
        if (bounds.IsEmpty)
        {
            throw new InvalidOperationException("Meshes have no spatial extent.");
        }

        var grid = BuildGrid(bounds, options.Resolution, out var origin, out var voxelSize);
        var markDistance = voxelSize * 2.25;

        foreach (var mesh in meshes)
        {
            var triangles = ExtractTriangles(mesh, MaxTrianglesPerMesh);
            if (triangles.Count == 0)
            {
                continue;
            }

            RasterizeTriangles(grid, triangles, origin, voxelSize, markDistance);
        }

        if (!grid.AnyOccupied())
        {
            throw new InvalidOperationException("No surface voxels were generated from the visible meshes.");
        }

        var thicken = Math.Max(options.ShellThicknessVoxels, 1);
        grid.Dilate(thicken);

        if (options.GapBridgeVoxels > 0)
        {
            grid.Dilate(options.GapBridgeVoxels);
        }

        return new BuildResult(grid, origin, voxelSize);
    }

    private static Rect3D ComputeCombinedBounds(IReadOnlyList<MeshSnapshot> meshes)
    {
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var minZ = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;
        var maxZ = double.NegativeInfinity;

        foreach (var mesh in meshes)
        {
            var meshBounds = mesh.Bounds;
            if (meshBounds.IsEmpty)
            {
                continue;
            }

            minX = Math.Min(minX, meshBounds.X);
            minY = Math.Min(minY, meshBounds.Y);
            minZ = Math.Min(minZ, meshBounds.Z);
            maxX = Math.Max(maxX, meshBounds.X + meshBounds.SizeX);
            maxY = Math.Max(maxY, meshBounds.Y + meshBounds.SizeY);
            maxZ = Math.Max(maxZ, meshBounds.Z + meshBounds.SizeZ);
        }

        if (double.IsPositiveInfinity(minX))
        {
            return Rect3D.Empty;
        }

        var pad = Math.Max(0.25, (maxX - minX + maxY - minY + maxZ - minZ) / 300.0);
        return new Rect3D(minX - pad, minY - pad, minZ - pad, maxX - minX + pad * 2, maxY - minY + pad * 2, maxZ - minZ + pad * 2);
    }

    private static Grid BuildGrid(Rect3D bounds, int resolution, out Vector3D origin, out double voxelSize)
    {
        var sizeX = bounds.SizeX;
        var sizeY = bounds.SizeY;
        var sizeZ = bounds.SizeZ;
        var maxDim = Math.Max(sizeX, Math.Max(sizeY, sizeZ));
        if (maxDim <= 1e-9)
        {
            maxDim = 1.0;
        }

        voxelSize = maxDim / resolution;
        var nx = Math.Max(4, (int)Math.Ceiling(sizeX / voxelSize) + 1);
        var ny = Math.Max(4, (int)Math.Ceiling(sizeY / voxelSize) + 1);
        var nz = Math.Max(4, (int)Math.Ceiling(sizeZ / voxelSize) + 1);

        origin = new Vector3D(bounds.X, bounds.Y, bounds.Z);
        return new Grid(nx, ny, nz);
    }

    private static void RasterizeTriangles(
        Grid grid,
        IReadOnlyList<FuseTriangle> triangles,
        Vector3D origin,
        double voxelSize,
        double markDistance)
    {
        var markDistanceSq = markDistance * markDistance;
        var invVoxel = 1.0 / voxelSize;

        foreach (var tri in triangles)
        {
            var ix0 = Math.Clamp((int)Math.Floor((tri.MinX - origin.X) * invVoxel) - 1, 0, grid.Nx - 1);
            var ix1 = Math.Clamp((int)Math.Floor((tri.MaxX - origin.X) * invVoxel) + 1, 0, grid.Nx - 1);
            var iy0 = Math.Clamp((int)Math.Floor((tri.MinY - origin.Y) * invVoxel) - 1, 0, grid.Ny - 1);
            var iy1 = Math.Clamp((int)Math.Floor((tri.MaxY - origin.Y) * invVoxel) + 1, 0, grid.Ny - 1);
            var iz0 = Math.Clamp((int)Math.Floor((tri.MinZ - origin.Z) * invVoxel) - 1, 0, grid.Nz - 1);
            var iz1 = Math.Clamp((int)Math.Floor((tri.MaxZ - origin.Z) * invVoxel) + 1, 0, grid.Nz - 1);

            for (var ix = ix0; ix <= ix1; ix++)
            {
                var cx = origin.X + ((ix + 0.5) * voxelSize);
                for (var iy = iy0; iy <= iy1; iy++)
                {
                    var cy = origin.Y + ((iy + 0.5) * voxelSize);
                    for (var iz = iz0; iz <= iz1; iz++)
                    {
                        var cz = origin.Z + ((iz + 0.5) * voxelSize);
                        if (DistanceSquaredToTriangle(cx, cy, cz, tri) <= markDistanceSq)
                        {
                            grid.Set(ix, iy, iz, true);
                        }
                    }
                }
            }
        }
    }

    private static List<FuseTriangle> ExtractTriangles(MeshSnapshot mesh, int maxTriangles)
    {
        var triangles = new List<FuseTriangle>();
        var stride = 1;
        var estimated = mesh.TriangleIndices.Length >= 3
            ? mesh.TriangleIndices.Length / 3
            : mesh.Positions.Length / 3;

        if (estimated > maxTriangles)
        {
            stride = (int)Math.Ceiling(estimated / (double)maxTriangles);
        }

        var index = 0;
        foreach (var (i0, i1, i2) in EnumerateTriangleIndices(mesh))
        {
            if (index++ % stride != 0)
            {
                continue;
            }

            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= mesh.Positions.Length || i1 >= mesh.Positions.Length || i2 >= mesh.Positions.Length)
            {
                continue;
            }

            var p0 = mesh.Positions[i0];
            var p1 = mesh.Positions[i1];
            var p2 = mesh.Positions[i2];
            triangles.Add(new FuseTriangle(
                new Vector3D(p0.X, p0.Y, p0.Z),
                new Vector3D(p1.X, p1.Y, p1.Z),
                new Vector3D(p2.X, p2.Y, p2.Z)));
        }

        return triangles;
    }

    internal static IEnumerable<(int I0, int I1, int I2)> EnumerateTriangleIndices(MeshSnapshot mesh)
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

    private static double DistanceSquaredToTriangle(double px, double py, double pz, FuseTriangle tri)
    {
        var closest = ClosestPointOnTriangle(px, py, pz, tri.A, tri.B, tri.C);
        var dx = px - closest.X;
        var dy = py - closest.Y;
        var dz = pz - closest.Z;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    private static Vector3D ClosestPointOnTriangle(
        double px, double py, double pz,
        Vector3D a, Vector3D b, Vector3D c)
    {
        var ab = b - a;
        var ac = c - a;
        var ap = new Vector3D(px - a.X, py - a.Y, pz - a.Z);

        var d1 = Vector3D.DotProduct(ab, ap);
        var d2 = Vector3D.DotProduct(ac, ap);
        if (d1 <= 0 && d2 <= 0)
        {
            return a;
        }

        var bp = new Vector3D(px - b.X, py - b.Y, pz - b.Z);
        var d3 = Vector3D.DotProduct(ab, bp);
        var d4 = Vector3D.DotProduct(ac, bp);
        if (d3 >= 0 && d4 <= d3)
        {
            return b;
        }

        var vc = (d1 * d4) - (d3 * d2);
        if (vc <= 0 && d1 >= 0 && d3 <= 0)
        {
            var v = d1 / (d1 - d3);
            return a + (ab * v);
        }

        var cp = new Vector3D(px - c.X, py - c.Y, pz - c.Z);
        var d5 = Vector3D.DotProduct(ab, cp);
        var d6 = Vector3D.DotProduct(ac, cp);
        if (d6 >= 0 && d5 <= d6)
        {
            return c;
        }

        var vb = (d5 * d2) - (d1 * d6);
        if (vb <= 0 && d2 >= 0 && d6 <= 0)
        {
            var w = d2 / (d2 - d6);
            return a + (ac * w);
        }

        var va = (d3 * d6) - (d5 * d4);
        if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0)
        {
            var w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            return b + ((c - b) * w);
        }

        var denom = 1.0 / ((va + vb) + vc);
        var v2 = vb * denom;
        var w2 = vc * denom;
        return a + (ab * v2) + (ac * w2);
    }

    private readonly struct FuseTriangle(Vector3D a, Vector3D b, Vector3D c)
    {
        public Vector3D A { get; } = a;
        public Vector3D B { get; } = b;
        public Vector3D C { get; } = c;

        public double MinX => Math.Min(A.X, Math.Min(B.X, C.X));
        public double MinY => Math.Min(A.Y, Math.Min(B.Y, C.Y));
        public double MinZ => Math.Min(A.Z, Math.Min(B.Z, C.Z));
        public double MaxX => Math.Max(A.X, Math.Max(B.X, C.X));
        public double MaxY => Math.Max(A.Y, Math.Max(B.Y, C.Y));
        public double MaxZ => Math.Max(A.Z, Math.Max(B.Z, C.Z));
    }
}
