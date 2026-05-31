using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>
/// Fuses open (non-watertight) meshes by unioning thickened surface shells in a voxel grid,
/// then extracting one outer surface via marching cubes.
/// </summary>
internal static class VoxelShellFusion
{
    public static MeshSnapshot Fuse(IReadOnlyList<MeshSnapshot> meshes)
        => Fuse(meshes, MeshFuseSettings.Load());

    public static MeshSnapshot Fuse(IReadOnlyList<MeshSnapshot> meshes, MeshFuseOptions options)
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

        var union = VoxelUnionGrid.Build(meshes, options);
        var fused = MarchingCubes.ExtractSurface(union.Grid.Occupied, union.Origin.X, union.Origin.Y, union.Origin.Z, union.VoxelSize);
        return MeshSmoothing.Smooth(fused, options.SmoothPasses);
    }
}
