using System.IO;

namespace DCMViewer.Services;

internal static class StatsDesignPaths
{
    public const string DesignRootFolderName = "StatsDesign";
    public const string CadSubfolderName = "StatsCAD";
    public const string ManifestFileName = "manifest.json";

    public static string GetDesignRoot(string orderFolderPath) =>
        Path.Combine(orderFolderPath, DesignRootFolderName);

    public static string GetCadFolder(string orderFolderPath) =>
        Path.Combine(GetDesignRoot(orderFolderPath), CadSubfolderName);

    public static string GetManifestPath(string orderFolderPath) =>
        Path.Combine(GetDesignRoot(orderFolderPath), ManifestFileName);

    public static string GetSculptTreeRoot(string orderFolderPath) =>
        Path.Combine(GetDesignRoot(orderFolderPath), SculptTreeStore.TreeDirectoryName);

    public static string BuildExportStlPath(string orderFolderPath, string designName) =>
        BuildCadStlPath(orderFolderPath, designName, suffix: string.Empty);

    public static string BuildInnerShellStlPath(string orderFolderPath, string designName) =>
        BuildCadStlPath(orderFolderPath, designName, "_inner");

    public static string BuildCrownStlPath(string orderFolderPath, string designName) =>
        BuildCadStlPath(orderFolderPath, designName, "_crown");

    public static string? TryGetRelativeCadPath(string orderFolderPath, string fullPath)
    {
        var cad = Path.GetFullPath(GetCadFolder(orderFolderPath));
        var normalized = Path.GetFullPath(fullPath);
        if (!normalized.StartsWith(cad, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Path.GetRelativePath(cad, normalized);
    }

    private static string BuildCadStlPath(string orderFolderPath, string designName, string suffix)
    {
        var safe = MakeSafeFileName(designName);
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "design";
        }

        return Path.Combine(GetCadFolder(orderFolderPath), safe + suffix + ".stl");
    }

    private static string MakeSafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value
            .Trim()
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray();
        return new string(chars).Trim('_');
    }
}
