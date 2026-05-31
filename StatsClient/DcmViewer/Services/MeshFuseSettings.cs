using static StatsClient.MVVM.Core.LocalSettingsDB;

namespace DCMViewer.Services;

/// <summary>Loads mesh fuse options from Stats local settings (Settings tab).</summary>
internal static class MeshFuseSettings
{
    public const string ModeKey = "DcmViewerFuseMode";
    public const string ResolutionKey = "DcmViewerFuseResolution";
    public const string GapBridgeKey = "DcmViewerFuseGapBridgeVoxels";
    public const string SmoothPassesKey = "DcmViewerFuseSmoothPasses";
    public const string ShellThicknessKey = "DcmViewerFuseShellThicknessVoxels";
    public const string CleanupArtifactsKey = "DcmViewerFuseCleanupArtifacts";
    public const string CleanupStrengthKey = "DcmViewerFuseCleanupStrength";

    public static MeshFuseOptions Load()
    {
        var mode = ParseMode(ReadLocalSetting(ModeKey));

        var resolution = MeshFuseOptions.DefaultResolution;
        if (int.TryParse(ReadLocalSetting(ResolutionKey), out var parsedResolution))
        {
            resolution = Math.Clamp(parsedResolution, MeshFuseOptions.MinResolution, MeshFuseOptions.MaxResolution);
        }

        var gapBridge = MeshFuseOptions.DefaultGapBridgeVoxels;
        if (int.TryParse(ReadLocalSetting(GapBridgeKey), out var parsedGap))
        {
            gapBridge = Math.Clamp(parsedGap, MeshFuseOptions.MinGapBridgeVoxels, MeshFuseOptions.MaxGapBridgeVoxels);
        }

        var smoothPasses = MeshFuseOptions.DefaultSmoothPasses;
        if (int.TryParse(ReadLocalSetting(SmoothPassesKey), out var parsedSmooth))
        {
            smoothPasses = Math.Clamp(parsedSmooth, MeshFuseOptions.MinSmoothPasses, MeshFuseOptions.MaxSmoothPasses);
        }

        var shellThickness = MeshFuseOptions.DefaultShellThicknessVoxels;
        if (int.TryParse(ReadLocalSetting(ShellThicknessKey), out var parsedShell))
        {
            shellThickness = Math.Clamp(parsedShell, MeshFuseOptions.MinShellThicknessVoxels, MeshFuseOptions.MaxShellThicknessVoxels);
        }

        return new MeshFuseOptions(mode, resolution, gapBridge, smoothPasses, shellThickness);
    }

    public static bool LoadCleanupArtifactsEnabled()
    {
        return string.Equals(ReadLocalSetting(CleanupArtifactsKey), "true", StringComparison.OrdinalIgnoreCase);
    }

    public static int LoadCleanupStrength()
    {
        if (int.TryParse(ReadLocalSetting(CleanupStrengthKey), out var parsedStrength))
        {
            return Math.Clamp(parsedStrength, MeshFuseOptions.MinCleanupStrength, MeshFuseOptions.MaxCleanupStrength);
        }

        return MeshFuseOptions.DefaultCleanupStrength;
    }

    public static void SaveCleanupPreferences(bool cleanupArtifacts, int cleanupStrength)
    {
        WriteLocalSetting(CleanupArtifactsKey, cleanupArtifacts ? "true" : "false");
        WriteLocalSetting(
            CleanupStrengthKey,
            Math.Clamp(cleanupStrength, MeshFuseOptions.MinCleanupStrength, MeshFuseOptions.MaxCleanupStrength)
                .ToString());
    }

    public static void Save(MeshFuseOptions options)
    {
        WriteLocalSetting(ModeKey, options.Mode.ToString());
        WriteLocalSetting(ResolutionKey, options.Resolution.ToString());
        WriteLocalSetting(GapBridgeKey, options.GapBridgeVoxels.ToString());
        WriteLocalSetting(SmoothPassesKey, options.SmoothPasses.ToString());
        WriteLocalSetting(ShellThicknessKey, options.ShellThicknessVoxels.ToString());
    }

    private static MeshFuseMode ParseMode(string? value)
    {
        if (string.Equals(value, nameof(MeshFuseMode.VoxelEnvelope), StringComparison.OrdinalIgnoreCase))
        {
            return MeshFuseMode.VoxelEnvelope;
        }

        if (string.Equals(value, nameof(MeshFuseMode.UnifiedShell), StringComparison.OrdinalIgnoreCase))
        {
            return MeshFuseMode.UnifiedShell;
        }

        if (string.Equals(value, nameof(MeshFuseMode.SolidCombine), StringComparison.OrdinalIgnoreCase))
        {
            return MeshFuseMode.SolidCombine;
        }

        return MeshFuseOptions.DefaultMode;
    }
}
