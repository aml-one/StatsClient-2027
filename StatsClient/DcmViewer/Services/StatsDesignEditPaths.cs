using System.IO;

namespace DCMViewer.Services;

internal static class StatsDesignEditPaths
{
    public const string EditRootFolderName = "StatsDesignEdit";
    public const string ExportsSubfolderName = "Exports";
    public const string ManifestFileName = "steps.json";

    public static string GetEditRoot(string orderFolderPath) =>
        Path.Combine(orderFolderPath, EditRootFolderName);

    public static string GetStepsManifestPath(string orderFolderPath) =>
        Path.Combine(GetEditRoot(orderFolderPath), ManifestFileName);

    public static string GetExportsFolder(string orderFolderPath) =>
        Path.Combine(GetEditRoot(orderFolderPath), ExportsSubfolderName);

    public static string BuildExportStlPath(string orderFolderPath, string meshFileName)
    {
        var safe = MakeSafeFileName(Path.GetFileNameWithoutExtension(meshFileName));
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "restoration";
        }

        return Path.Combine(GetExportsFolder(orderFolderPath), safe + "_edited.stl");
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
