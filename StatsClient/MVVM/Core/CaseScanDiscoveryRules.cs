using System.IO;

namespace StatsClient.MVVM.Core;

/// <summary>
/// Filters scan paths for case discovery (viewer + Stats Design). Matches legacy Stats Client behavior:
/// auxiliary scanner export folders and SID.* interface meshes are not designer/viewer meshes.
/// </summary>
public static class CaseScanDiscoveryRules
{
    public const string AuxScanDirectoryName = "AuxScanDir";

    public static bool IsUnderAuxScanDirectory(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var normalized = Path.GetFullPath(filePath).Replace('\\', '/');
        return normalized.Contains($"/{AuxScanDirectoryName}/", StringComparison.OrdinalIgnoreCase)
               || normalized.EndsWith($"/{AuxScanDirectoryName}", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>3Shape scanner interface meshes (SID.LowerPrep.DCM, etc.) — not clinical scan geometry.</summary>
    public static bool IsScannerInterfaceFile(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        return Path.GetFileName(filePath).StartsWith("SID.", StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldIncludeInCaseDiscovery(string? filePath) =>
        !IsUnderAuxScanDirectory(filePath) && !IsScannerInterfaceFile(filePath);
}
