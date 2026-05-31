namespace DCMViewer.Services;

internal static class MeshFuseService
{
    public static MeshSnapshot Fuse(
        IReadOnlyList<MeshSnapshot> meshes,
        MeshFuseMode? modeOverride = null,
        IReadOnlyList<FuseInnerSideHint>? innerSideHints = null,
        IProgress<double>? progress = null)
    {
        var options = MeshFuseSettings.Load();
        var mode = modeOverride ?? options.Mode;

        return mode switch
        {
            MeshFuseMode.VoxelEnvelope => VoxelShellFusion.Fuse(meshes, options),
            MeshFuseMode.UnifiedShell => MeshUnifiedShellFuse.Fuse(meshes, options, innerSideHints, progress),
            _ => MeshCombineFuse.Fuse(meshes)
        };
    }
}
