using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

internal sealed class StatsDesignEditStepStore
{
    private const int ManifestVersion = 1;
    private const int MaxPersistedSteps = 200;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _editRoot;
    private DesignEditManifest _manifest;

    private StatsDesignEditStepStore(string orderFolderPath)
    {
        _editRoot = StatsDesignEditPaths.GetEditRoot(orderFolderPath);
        _manifest = ReadManifest();
    }

    public static StatsDesignEditStepStore? Open(string? orderFolderPath)
    {
        if (string.IsNullOrWhiteSpace(orderFolderPath))
        {
            return null;
        }

        var orderRoot = Path.GetFullPath(orderFolderPath);
        if (!Directory.Exists(orderRoot))
        {
            return null;
        }

        Directory.CreateDirectory(StatsDesignEditPaths.GetEditRoot(orderRoot));
        return new StatsDesignEditStepStore(orderRoot);
    }

    public static string NormalizeMeshRelativeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        return key
            .Replace('\\', '/')
            .Trim()
            .TrimStart('/');
    }

    public bool HasSteps => _manifest.Steps.Count > 0;

    public IReadOnlyList<DesignEditStepRecord> Steps => _manifest.Steps;

    public static string GetRelativeMeshKey(string orderFolderPath, string meshFullPath)
    {
        var orderRoot = Path.GetFullPath(orderFolderPath);
        var fullPath = Path.GetFullPath(meshFullPath);
        if (fullPath.StartsWith(orderRoot, StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeMeshRelativeKey(Path.GetRelativePath(orderRoot, fullPath));
        }

        return NormalizeMeshRelativeKey(fullPath);
    }

    public void RecordSculptStep(
        string orderFolderPath,
        string meshFullPath,
        Point3D[] beforePositions,
        Point3D[] afterPositions,
        SculptBrushTool tool,
        double radius,
        double strength)
    {
        EnsureDirectories();
        var meshRelativePath = GetRelativeMeshKey(orderFolderPath, meshFullPath);
        EnsureOriginalSaved(meshRelativePath, beforePositions, readIndices: null);

        var stepId = _manifest.NextStepId++;
        var beforeFile = $"steps/{stepId:D6}_before.bin";
        var afterFile = $"steps/{stepId:D6}_after.bin";
        WritePositions(Path.Combine(_editRoot, beforeFile), beforePositions);
        WritePositions(Path.Combine(_editRoot, afterFile), afterPositions);

        _manifest.Steps.Add(new DesignEditStepRecord
        {
            Id = stepId,
            Type = DesignEditStepType.Sculpt,
            MeshRelativePath = NormalizeMeshRelativeKey(meshRelativePath),
            Tool = tool.ToString(),
            Radius = radius,
            Strength = strength,
            BeforeFile = beforeFile,
            AfterFile = afterFile,
            Utc = DateTime.UtcNow.ToString("O")
        });

        TrimIfNeeded();
        SaveManifest();
    }

    public void RecordCutStep(
        string orderFolderPath,
        string meshFullPath,
        MeshSnapshot beforeMesh,
        MeshSnapshot afterMesh,
        Point3D planePoint,
        Vector3D planeNormal,
        bool removePositiveSide)
    {
        EnsureDirectories();
        var meshRelativePath = GetRelativeMeshKey(orderFolderPath, meshFullPath);
        EnsureOriginalSaved(meshRelativePath, beforeMesh.Positions, beforeMesh.TriangleIndices);

        var stepId = _manifest.NextStepId++;
        var beforeFile = $"steps/{stepId:D6}_before_mesh.bin";
        var afterFile = $"steps/{stepId:D6}_after_mesh.bin";
        WriteMesh(Path.Combine(_editRoot, beforeFile), beforeMesh);
        WriteMesh(Path.Combine(_editRoot, afterFile), afterMesh);

        _manifest.Steps.Add(new DesignEditStepRecord
        {
            Id = stepId,
            Type = DesignEditStepType.Cut,
            MeshRelativePath = NormalizeMeshRelativeKey(meshRelativePath),
            PlanePointX = planePoint.X,
            PlanePointY = planePoint.Y,
            PlanePointZ = planePoint.Z,
            PlaneNormalX = planeNormal.X,
            PlaneNormalY = planeNormal.Y,
            PlaneNormalZ = planeNormal.Z,
            RemovePositiveSide = removePositiveSide,
            BeforeFile = beforeFile,
            AfterFile = afterFile,
            Utc = DateTime.UtcNow.ToString("O")
        });

        TrimIfNeeded();
        SaveManifest();
    }

    public bool TryPopLastStep(out DesignEditStepRecord? removedStep)
    {
        removedStep = null;
        if (_manifest.Steps.Count == 0)
        {
            return false;
        }

        removedStep = _manifest.Steps[^1];
        DeleteStepFiles(removedStep);
        _manifest.Steps.RemoveAt(_manifest.Steps.Count - 1);
        SaveManifest();
        return true;
    }

    public void PushStepBack(DesignEditStepRecord step)
    {
        _manifest.Steps.Add(step);
        SaveManifest();
    }

    public Point3D[]? ReadSculptBefore(DesignEditStepRecord step) =>
        step.Type == DesignEditStepType.Sculpt
            ? ReadPositions(Path.Combine(_editRoot, step.BeforeFile))
            : null;

    public Point3D[]? ReadSculptAfter(DesignEditStepRecord step) =>
        step.Type == DesignEditStepType.Sculpt
            ? ReadPositions(Path.Combine(_editRoot, step.AfterFile))
            : null;

    public MeshSnapshot? ReadMeshBefore(DesignEditStepRecord step) =>
        ReadMesh(Path.Combine(_editRoot, step.BeforeFile));

    public MeshSnapshot? ReadMeshAfter(DesignEditStepRecord step) =>
        ReadMesh(Path.Combine(_editRoot, step.AfterFile));

    public bool TryGetOriginalMesh(string meshRelativePath, out MeshSnapshot? mesh)
    {
        mesh = null;
        var originalPath = Path.Combine(_editRoot, "originals", MeshKeyToStorageName(meshRelativePath) + ".mesh.bin");
        if (!File.Exists(originalPath))
        {
            return false;
        }

        mesh = ReadMesh(originalPath);
        return mesh is not null;
    }

    public bool TryGetOriginalPositions(string meshRelativePath, out Point3D[]? positions)
    {
        positions = null;
        if (TryGetOriginalMesh(meshRelativePath, out var mesh) && mesh is not null)
        {
            positions = mesh.Positions;
            return true;
        }

        var positionsPath = Path.Combine(_editRoot, "originals", MeshKeyToStorageName(meshRelativePath) + ".bin");
        if (!File.Exists(positionsPath))
        {
            return false;
        }

        positions = ReadPositions(positionsPath);
        return positions is not null;
    }

    public void EnsureOriginalFromMesh(string orderFolderPath, string meshFullPath, MeshSnapshot mesh)
    {
        EnsureDirectories();
        var meshRelativePath = GetRelativeMeshKey(orderFolderPath, meshFullPath);
        EnsureOriginalSaved(meshRelativePath, mesh.Positions, mesh.TriangleIndices);
    }

    public void ClearAll()
    {
        foreach (var step in _manifest.Steps.ToList())
        {
            DeleteStepFiles(step);
        }

        _manifest.Steps.Clear();
        SaveManifest();
    }

    private void EnsureOriginalSaved(string meshRelativePath, Point3D[] positions, int[]? readIndices)
    {
        var storageName = MeshKeyToStorageName(meshRelativePath);
        var meshPath = Path.Combine(_editRoot, "originals", storageName + ".mesh.bin");
        if (File.Exists(meshPath))
        {
            return;
        }

        if (readIndices is not null)
        {
            WriteMesh(meshPath, new MeshSnapshot(positions, readIndices));
            return;
        }

        var positionsPath = Path.Combine(_editRoot, "originals", storageName + ".bin");
        if (File.Exists(positionsPath))
        {
            return;
        }

        WritePositions(positionsPath, positions);
    }

    private void TrimIfNeeded()
    {
        while (_manifest.Steps.Count > MaxPersistedSteps)
        {
            var step = _manifest.Steps[0];
            DeleteStepFiles(step);
            _manifest.Steps.RemoveAt(0);
        }
    }

    private void DeleteStepFiles(DesignEditStepRecord step)
    {
        TryDelete(Path.Combine(_editRoot, step.BeforeFile));
        TryDelete(Path.Combine(_editRoot, step.AfterFile));
    }

    private static void TryDelete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private void EnsureDirectories()
    {
        Directory.CreateDirectory(_editRoot);
        Directory.CreateDirectory(Path.Combine(_editRoot, "steps"));
        Directory.CreateDirectory(Path.Combine(_editRoot, "originals"));
        Directory.CreateDirectory(StatsDesignEditPaths.GetExportsFolder(Path.GetDirectoryName(_editRoot)!));
    }

    private DesignEditManifest ReadManifest()
    {
        var manifestPath = Path.Combine(_editRoot, StatsDesignEditPaths.ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return new DesignEditManifest();
        }

        try
        {
            var json = File.ReadAllText(manifestPath);
            return JsonSerializer.Deserialize<DesignEditManifest>(json, JsonOptions) ?? new DesignEditManifest();
        }
        catch (IOException)
        {
            return new DesignEditManifest();
        }
        catch (JsonException)
        {
            return new DesignEditManifest();
        }
    }

    private void SaveManifest()
    {
        EnsureDirectories();
        var manifestPath = Path.Combine(_editRoot, StatsDesignEditPaths.ManifestFileName);
        var json = JsonSerializer.Serialize(_manifest, JsonOptions);
        File.WriteAllText(manifestPath, json);
    }

    private static string MeshKeyToStorageName(string meshRelativePath) =>
        NormalizeMeshRelativeKey(meshRelativePath)
            .Replace('/', '_')
            .Replace(':', '_');

    private static void WritePositions(string path, Point3D[] positions)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write(positions.Length);
        foreach (var point in positions)
        {
            writer.Write(point.X);
            writer.Write(point.Y);
            writer.Write(point.Z);
        }
    }

    private static Point3D[]? ReadPositions(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        var count = reader.ReadInt32();
        if (count < 0)
        {
            return null;
        }

        var positions = new Point3D[count];
        for (var index = 0; index < count; index++)
        {
            positions[index] = new Point3D(reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());
        }

        return positions;
    }

    private static void WriteMesh(string path, MeshSnapshot mesh)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write(mesh.Positions.Length);
        foreach (var point in mesh.Positions)
        {
            writer.Write(point.X);
            writer.Write(point.Y);
            writer.Write(point.Z);
        }

        writer.Write(mesh.TriangleIndices.Length);
        foreach (var index in mesh.TriangleIndices)
        {
            writer.Write(index);
        }
    }

    private static MeshSnapshot? ReadMesh(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        var vertexCount = reader.ReadInt32();
        if (vertexCount < 0)
        {
            return null;
        }

        var positions = new Point3D[vertexCount];
        for (var index = 0; index < vertexCount; index++)
        {
            positions[index] = new Point3D(reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());
        }

        var indexCount = reader.ReadInt32();
        if (indexCount < 0)
        {
            return null;
        }

        var indices = new int[indexCount];
        for (var index = 0; index < indexCount; index++)
        {
            indices[index] = reader.ReadInt32();
        }

        return new MeshSnapshot(positions, indices);
    }

    private sealed class DesignEditManifest
    {
        public int Version { get; set; } = ManifestVersion;
        public int NextStepId { get; set; } = 1;
        public List<DesignEditStepRecord> Steps { get; set; } = [];
    }
}

internal enum DesignEditStepType
{
    Sculpt,
    Cut
}

internal sealed class DesignEditStepRecord
{
    public int Id { get; set; }
    public DesignEditStepType Type { get; set; }
    public string MeshRelativePath { get; set; } = string.Empty;
    public string? Tool { get; set; }
    public double Radius { get; set; }
    public double Strength { get; set; }
    public double PlanePointX { get; set; }
    public double PlanePointY { get; set; }
    public double PlanePointZ { get; set; }
    public double PlaneNormalX { get; set; }
    public double PlaneNormalY { get; set; }
    public double PlaneNormalZ { get; set; }
    public bool RemovePositiveSide { get; set; }
    public string BeforeFile { get; set; } = string.Empty;
    public string AfterFile { get; set; } = string.Empty;
    public string? Utc { get; set; }
}
