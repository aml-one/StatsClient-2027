using System.IO;
using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

internal static class StatsDesignExportService
{
    public static string ExportDesignStl(
        string orderFolderPath,
        StatsDesignManifest manifest,
        IReadOnlyList<MeshSnapshot> designMeshes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderFolderPath);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(designMeshes);

        if (designMeshes.Count == 0)
        {
            throw new InvalidOperationException("No design mesh is available to export.");
        }

        var outputPath = StatsDesignPaths.BuildExportStlPath(orderFolderPath, manifest.DesignName);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        MeshExportService.Export(outputPath, designMeshes);

        manifest.ExportedStlRelativePath = Path.GetRelativePath(orderFolderPath, outputPath);
        manifest.LastExportedUtc = DateTime.UtcNow;
        StatsDesignManifestStore.Save(orderFolderPath, manifest);
        return outputPath;
    }
}
