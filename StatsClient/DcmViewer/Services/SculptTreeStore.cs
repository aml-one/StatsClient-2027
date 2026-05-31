using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>
/// Persists sculpt stroke history under <c>{orderFolder}/StatsSculpTree/</c> for reload, undo, and undo-all.
/// </summary>
internal sealed class SculptTreeStore
{
    public const string TreeDirectoryName = "StatsSculpTree";

    private const int ManifestVersion = 1;
    private const int MaxPersistedSteps = 200;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _treeRoot;
    private SculptTreeManifest _manifest;

    private SculptTreeStore(string orderFolderPath)
    {
        _treeRoot = Path.Combine(orderFolderPath, TreeDirectoryName);
        _manifest = ReadManifest();
    }

    public static SculptTreeStore? Open(string? orderFolderPath)
    {
        if (string.IsNullOrWhiteSpace(orderFolderPath) || !Directory.Exists(orderFolderPath))
        {
            return null;
        }

        return new SculptTreeStore(orderFolderPath);
    }

    public bool HasSteps => _manifest.Steps.Count > 0;

    public IReadOnlyList<SculptTreeStepRecord> Steps => _manifest.Steps;

    public static string GetRelativeMeshKey(string orderFolderPath, string meshFullPath)
    {
        var orderRoot = Path.GetFullPath(orderFolderPath);
        var fullPath = Path.GetFullPath(meshFullPath);
        if (fullPath.StartsWith(orderRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetRelativePath(orderRoot, fullPath);
        }

        return fullPath;
    }

    public static string MeshKeyToStorageName(string meshRelativePath) =>
        meshRelativePath
            .Replace('\\', '_')
            .Replace('/', '_')
            .Replace(':', '_');

    public void RecordStep(
        string orderFolderPath,
        string meshFullPath,
        Point3D[] beforePositions,
        Point3D[] afterPositions,
        SculptBrushTool tool,
        double radius,
        double strength)
    {
        ArgumentNullException.ThrowIfNull(beforePositions);
        ArgumentNullException.ThrowIfNull(afterPositions);

        EnsureDirectories();

        var meshRelativePath = GetRelativeMeshKey(orderFolderPath, meshFullPath);
        EnsureOriginalSaved(meshRelativePath, beforePositions);

        var stepId = _manifest.NextStepId++;
        var beforeFile = $"steps/{stepId:D6}_before.bin";
        var afterFile = $"steps/{stepId:D6}_after.bin";
        WritePositions(Path.Combine(_treeRoot, beforeFile), beforePositions);
        WritePositions(Path.Combine(_treeRoot, afterFile), afterPositions);

        _manifest.Steps.Add(new SculptTreeStepRecord
        {
            Id = stepId,
            MeshRelativePath = meshRelativePath,
            Tool = tool.ToString(),
            Radius = radius,
            Strength = strength,
            BeforeFile = beforeFile,
            AfterFile = afterFile,
            Utc = DateTime.UtcNow.ToString("O")
        });

        while (_manifest.Steps.Count > MaxPersistedSteps)
        {
            TrimOldestStep();
        }

        SaveManifest();
    }

    public bool TryPopLastStep(out Point3D[]? beforePositions)
    {
        beforePositions = null;
        if (_manifest.Steps.Count == 0)
        {
            return false;
        }

        var step = _manifest.Steps[^1];
        beforePositions = ReadPositions(Path.Combine(_treeRoot, step.BeforeFile));
        DeleteStepFiles(step);
        _manifest.Steps.RemoveAt(_manifest.Steps.Count - 1);
        SaveManifest();
        return beforePositions is not null;
    }

    public Point3D[]? ReadStepBefore(SculptTreeStepRecord step) =>
        ReadPositions(Path.Combine(_treeRoot, step.BeforeFile));

    public Point3D[]? ReadStepAfter(SculptTreeStepRecord step) =>
        ReadPositions(Path.Combine(_treeRoot, step.AfterFile));

    public void TrimOldestStep()
    {
        if (_manifest.Steps.Count == 0)
        {
            return;
        }

        var step = _manifest.Steps[0];
        DeleteStepFiles(step);
        _manifest.Steps.RemoveAt(0);
        SaveManifest();
    }

    public IReadOnlyDictionary<string, Point3D[]> GetLatestAfterPositionsByMesh()
    {
        var latest = new Dictionary<string, Point3D[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in _manifest.Steps)
        {
            var after = ReadPositions(Path.Combine(_treeRoot, step.AfterFile));
            if (after is not null)
            {
                latest[step.MeshRelativePath] = after;
            }
        }

        return latest;
    }

    public IEnumerable<(string MeshRelativePath, Point3D[] BeforePositions)> EnumerateBeforeSnapshotsInOrder()
    {
        foreach (var step in _manifest.Steps)
        {
            var before = ReadPositions(Path.Combine(_treeRoot, step.BeforeFile));
            if (before is not null)
            {
                yield return (step.MeshRelativePath, before);
            }
        }
    }

    public bool TryGetOriginalPositions(string meshRelativePath, out Point3D[]? positions)
    {
        var path = Path.Combine(_treeRoot, "originals", $"{MeshKeyToStorageName(meshRelativePath)}.bin");
        positions = ReadPositions(path);
        return positions is not null;
    }

    public bool TryGetFirstBeforePositions(string meshRelativePath, out Point3D[]? positions)
    {
        positions = null;
        foreach (var step in _manifest.Steps)
        {
            if (!string.Equals(step.MeshRelativePath, meshRelativePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            positions = ReadPositions(Path.Combine(_treeRoot, step.BeforeFile));
            return positions is not null;
        }

        return false;
    }

    public IEnumerable<string> GetAffectedMeshKeys() =>
        _manifest.Steps.Select(step => step.MeshRelativePath).Distinct(StringComparer.OrdinalIgnoreCase);

    public void ClearAll()
    {
        foreach (var step in _manifest.Steps.ToList())
        {
            DeleteStepFiles(step);
        }

        _manifest.Steps.Clear();
        _manifest.NextStepId = 1;
        SaveManifest();

        var originalsDir = Path.Combine(_treeRoot, "originals");
        if (Directory.Exists(originalsDir))
        {
            foreach (var file in Directory.EnumerateFiles(originalsDir, "*.bin"))
            {
                TryDeleteFile(file);
            }
        }
    }

    private void EnsureOriginalSaved(string meshRelativePath, Point3D[] beforePositions)
    {
        var path = Path.Combine(_treeRoot, "originals", $"{MeshKeyToStorageName(meshRelativePath)}.bin");
        if (File.Exists(path))
        {
            return;
        }

        WritePositions(path, beforePositions);
    }

    private void EnsureDirectories()
    {
        Directory.CreateDirectory(Path.Combine(_treeRoot, "steps"));
        Directory.CreateDirectory(Path.Combine(_treeRoot, "originals"));
    }

    private SculptTreeManifest ReadManifest()
    {
        var manifestPath = Path.Combine(_treeRoot, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return new SculptTreeManifest { Version = ManifestVersion, NextStepId = 1 };
        }

        try
        {
            var json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<SculptTreeManifest>(json, JsonOptions);
            if (manifest is null || manifest.Version != ManifestVersion)
            {
                return new SculptTreeManifest { Version = ManifestVersion, NextStepId = 1 };
            }

            manifest.Steps ??= [];
            if (manifest.NextStepId <= 0)
            {
                manifest.NextStepId = manifest.Steps.Count == 0
                    ? 1
                    : manifest.Steps.Max(step => step.Id) + 1;
            }

            return manifest;
        }
        catch
        {
            return new SculptTreeManifest { Version = ManifestVersion, NextStepId = 1 };
        }
    }

    private void SaveManifest()
    {
        EnsureDirectories();
        var manifestPath = Path.Combine(_treeRoot, "manifest.json");
        var json = JsonSerializer.Serialize(_manifest, JsonOptions);
        File.WriteAllText(manifestPath, json);
    }

    private void DeleteStepFiles(SculptTreeStepRecord step)
    {
        TryDeleteFile(Path.Combine(_treeRoot, step.BeforeFile));
        TryDeleteFile(Path.Combine(_treeRoot, step.AfterFile));
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private static void WritePositions(string filePath, Point3D[] positions)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        using var stream = File.Create(filePath);
        using var writer = new BinaryWriter(stream);
        writer.Write(positions.Length);
        foreach (var point in positions)
        {
            writer.Write(point.X);
            writer.Write(point.Y);
            writer.Write(point.Z);
        }
    }

    private static Point3D[]? ReadPositions(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            using var reader = new BinaryReader(stream);
            var count = reader.ReadInt32();
            if (count <= 0)
            {
                return Array.Empty<Point3D>();
            }

            var positions = new Point3D[count];
            for (var index = 0; index < count; index++)
            {
                positions[index] = new Point3D(reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());
            }

            return positions;
        }
        catch
        {
            return null;
        }
    }

    private sealed class SculptTreeManifest
    {
        public int Version { get; set; } = ManifestVersion;

        public int NextStepId { get; set; } = 1;

        public List<SculptTreeStepRecord> Steps { get; set; } = [];
    }
}

internal sealed class SculptTreeStepRecord
{
    public int Id { get; set; }

    public string MeshRelativePath { get; set; } = string.Empty;

    public string Tool { get; set; } = string.Empty;

    public double Radius { get; set; }

    public double Strength { get; set; }

    public string BeforeFile { get; set; } = string.Empty;

    public string AfterFile { get; set; } = string.Empty;

    public string? Utc { get; set; }
}
