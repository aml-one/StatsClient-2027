namespace DCMViewer.Services;

internal enum MeshFuseMode
{
    /// <summary>Keep all source triangles — opaque, same detail as the viewer.</summary>
    SolidCombine,

    /// <summary>Keep scan triangles on the unified outer surface; drop interior duplicates where parts touch.</summary>
    UnifiedShell,

    /// <summary>Remesh via voxel shell + marching cubes — single outer envelope, can look thin.</summary>
    VoxelEnvelope
}

/// <summary>User-tunable options for mesh fuse export.</summary>
internal sealed record MeshFuseOptions(
    MeshFuseMode Mode,
    int Resolution,
    int GapBridgeVoxels,
    int SmoothPasses,
    int ShellThicknessVoxels)
{
    public const MeshFuseMode DefaultMode = MeshFuseMode.SolidCombine;
    public const int DefaultResolution = 256;
    public const int DefaultGapBridgeVoxels = 1;
    public const int DefaultSmoothPasses = 1;
    public const int DefaultShellThicknessVoxels = 4;

    public const int MinResolution = 96;
    public const int MaxResolution = 320;
    public const int MinGapBridgeVoxels = 0;
    public const int MaxGapBridgeVoxels = 6;
    public const int MinSmoothPasses = 0;
    public const int MaxSmoothPasses = 8;
    public const int MinShellThicknessVoxels = 1;
    public const int MaxShellThicknessVoxels = 8;

    public const int DefaultCleanupStrength = 35;
    public const int MinCleanupStrength = 0;
    public const int MaxCleanupStrength = 100;
    public const int MinCleanupIslandTriangles = 4;
    public const int MaxCleanupIslandTriangles = 72;

    public static int MapCleanupStrengthToMaxIslandTriangles(int cleanupStrength)
    {
        var normalized = Math.Clamp(cleanupStrength, MinCleanupStrength, MaxCleanupStrength) / (double)MaxCleanupStrength;
        return MinCleanupIslandTriangles +
               (int)Math.Round(normalized * (MaxCleanupIslandTriangles - MinCleanupIslandTriangles));
    }

    public static MeshFuseOptions Default { get; } = new(
        DefaultMode,
        DefaultResolution,
        DefaultGapBridgeVoxels,
        DefaultSmoothPasses,
        DefaultShellThicknessVoxels);
}
