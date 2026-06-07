using DCMViewer.ViewModels;
using System.IO;
using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>
/// Resolves which loaded scan mesh the margin sits on. Inner shell and safety tools use that surface,
/// not filename heuristics (intraoral shells, unsectioned models, antagonist, etc.).
/// </summary>
internal static class StatsDesignPrepMeshResolver
{
    public static bool CanHostMargin(LoadedMeshItemViewModel item)
    {
        if (item.IsLoadFailed || !item.IsVisible || item.MeshSnapshot is null)
        {
            return false;
        }

        if (item.Category is not MeshCategory.Scan and not MeshCategory.Model)
        {
            return false;
        }

        return !IsDesignArtifact(item.FilePath);
    }

    /// <summary>
    /// Scan/model the margin loop lies on (closest surface to margin points).
    /// </summary>
    public static LoadedMeshItemViewModel? ResolveMarginHost(
        IEnumerable<LoadedMeshItemViewModel> files,
        IReadOnlyList<Point3D> marginLoop)
    {
        if (marginLoop.Count < 3)
        {
            return null;
        }

        LoadedMeshItemViewModel? best = null;
        var bestScore = double.PositiveInfinity;

        foreach (var item in files.Where(CanHostMargin))
        {
            var score = AverageDistanceToMesh(marginLoop, item.MeshSnapshot!);
            if (score < bestScore)
            {
                bestScore = score;
                best = item;
            }
        }

        return best;
    }

    public static MeshSnapshot? ResolveMarginHostSnapshot(
        IEnumerable<LoadedMeshItemViewModel> files,
        IReadOnlyList<Point3D> marginLoop,
        string? preferredHostFilePath = null)
    {
        if (!string.IsNullOrWhiteSpace(preferredHostFilePath))
        {
            var preferred = files.FirstOrDefault(item =>
                CanHostMargin(item) &&
                string.Equals(Path.GetFullPath(item.FilePath), Path.GetFullPath(preferredHostFilePath), StringComparison.OrdinalIgnoreCase));
            if (preferred?.MeshSnapshot is not null)
            {
                return preferred.MeshSnapshot;
            }
        }

        return ResolveMarginHost(files, marginLoop)?.MeshSnapshot;
    }

    public static IReadOnlyList<MeshSnapshot> CollectSnapshotsForMargin(
        IEnumerable<LoadedMeshItemViewModel> files,
        IReadOnlyList<Point3D> marginLoop)
    {
        var host = ResolveMarginHostSnapshot(files, marginLoop);
        return host is null ? [] : [host];
    }

    public static IReadOnlyList<MeshSnapshot> CollectSnapshots(
        IEnumerable<(MeshSnapshot Snapshot, MeshCategory Category, bool IsVisible, bool IsLoadFailed, string FilePath)> meshes,
        IReadOnlyList<Point3D>? marginLoop = null)
    {
        if (marginLoop is { Count: >= 3 })
        {
            var asItems = meshes
                .Where(m => !m.IsLoadFailed && m.IsVisible && m.Category is MeshCategory.Scan or MeshCategory.Model && !IsDesignArtifact(m.FilePath))
                .Select(m => (m.Snapshot, m.FilePath))
                .ToList();

            var bestPath = ResolveMarginHostPathFromTuples(asItems, marginLoop);
            if (bestPath is not null)
            {
                var match = asItems.FirstOrDefault(t =>
                    string.Equals(Path.GetFullPath(t.FilePath), Path.GetFullPath(bestPath), StringComparison.OrdinalIgnoreCase));
                if (match.Snapshot is not null)
                {
                    return [match.Snapshot];
                }
            }
        }

        var snapshots = meshes
            .Where(m =>
                !m.IsLoadFailed &&
                m.IsVisible &&
                m.Category is MeshCategory.Scan or MeshCategory.Model &&
                !IsDesignArtifact(m.FilePath) &&
                !IsIncidentalReferenceScan(m.FilePath))
            .Select(m => m.Snapshot)
            .ToList();

        if (snapshots.Count <= 1 || marginLoop is not { Count: >= 3 })
        {
            return snapshots;
        }

        var best = StatsDesignCementShellBuilder.PickBestPrepMesh(snapshots, marginLoop);
        return [best, .. snapshots.Where(s => !ReferenceEquals(s, best))];
    }

    public static MeshSnapshot? PickPrimary(
        IEnumerable<LoadedMeshItemViewModel> files,
        IReadOnlyList<Point3D> marginLoop,
        string? preferredHostFilePath = null) =>
        ResolveMarginHostSnapshot(files, marginLoop, preferredHostFilePath);

    private static string? ResolveMarginHostPathFromTuples(
        IReadOnlyList<(MeshSnapshot Snapshot, string FilePath)> items,
        IReadOnlyList<Point3D> marginLoop)
    {
        string? bestPath = null;
        var bestScore = double.PositiveInfinity;
        foreach (var (snapshot, path) in items)
        {
            var score = AverageDistanceToMesh(marginLoop, snapshot);
            if (score < bestScore)
            {
                bestScore = score;
                bestPath = path;
            }
        }

        return bestPath;
    }

    private static double AverageDistanceToMesh(IReadOnlyList<Point3D> marginLoop, MeshSnapshot mesh)
    {
        var index = new StatsDesignMeshSpatialIndex(mesh);
        var total = 0.0;
        foreach (var point in marginLoop)
        {
            total += StatsDesignMeshProximity.ClosestDistanceToMesh(point, mesh, index);
        }

        return total / marginLoop.Count;
    }

    private static bool IsDesignArtifact(string? filePath) =>
        StatsDesignShellService.IsInnerShellPath(filePath) ||
        StatsDesignShellService.IsCrownPath(filePath);

    /// <summary>Scans kept out of automatic lists when margin host is unknown (bite, pre-prep, etc.).</summary>
    private static bool IsIncidentalReferenceScan(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return true;
        }

        if (PrepScanMaterialRules.IsPreopScan(filePath))
        {
            return true;
        }

        var normalized = NormalizeScanName(filePath);
        return normalized.Contains("bite", StringComparison.Ordinal) ||
               normalized.Contains("previousdesign", StringComparison.Ordinal);
    }

    private static string NormalizeScanName(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        return fileName
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);
    }
}