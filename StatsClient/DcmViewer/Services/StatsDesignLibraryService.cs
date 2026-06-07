using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

internal static class StatsDesignLibraryService
{
    private static readonly Regex ToothNumberPattern = new(
        @"Crown\s+Unn(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string ResolveLibraryRoot()
    {
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "Smile Libraries"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Smile Libraries")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Smile Libraries")),
            @"C:\Users\ambru\source\repos\Stats Family\StatsClient2027\Smile Libraries"
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(
            "Smile Libraries folder was not found. Expected a folder named \"Smile Libraries\" next to the application or solution root.");
    }

    public static IReadOnlyList<StatsDesignToothLibraryEntry> ListToothEntries()
    {
        var root = ResolveLibraryRoot();
        var entries = new List<StatsDesignToothLibraryEntry>();

        void ScanFolder(string folder, bool isOad)
        {
            if (!Directory.Exists(folder))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(folder, "Crown Unn*.dcm", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var match = ToothNumberPattern.Match(name);
                if (!match.Success || !int.TryParse(match.Groups[1].Value, out var toothNumber))
                {
                    continue;
                }

                entries.Add(new StatsDesignToothLibraryEntry
                {
                    ToothNumber = toothNumber,
                    FilePath = file,
                    DisplayName = isOad ? $"#{toothNumber} (OAD)" : $"#{toothNumber}",
                    IsOad = isOad
                });
            }
        }

        ScanFolder(root, isOad: false);
        ScanFolder(Path.Combine(root, "OAD"), isOad: true);

        return entries
            .OrderBy(e => e.ToothNumber)
            .ThenBy(e => e.IsOad)
            .ToList();
    }

    public static StatsDesignToothLibraryEntry? FindByToothNumber(int toothNumber, bool preferOad = false)
    {
        var matches = ListToothEntries()
            .Where(e => e.ToothNumber == toothNumber)
            .ToList();
        if (matches.Count == 0)
        {
            return null;
        }

        return matches.FirstOrDefault(e => e.IsOad == preferOad) ?? matches[0];
    }

    public static MeshSnapshot LoadLibraryToothMesh(string filePath)
    {
        var parser = new DcmParser();
        var parsed = parser.ParseFile(filePath, allowThreeShapeFallback: true, sceneTransformKind: SceneTransformKind.DesignedFull);
        return parsed.Mesh;
    }

    public static MeshSnapshot PrepareLibraryBaseline(
        MeshSnapshot rawMesh,
        StatsDesignManifest manifest,
        Vector3D insertionAxis,
        Point3D marginCentroid)
    {
        var oriented = insertionAxis.LengthSquared > 1e-12
            ? StatsDesignMeshOrientation.OrientLibraryToInsertionAxis(rawMesh, insertionAxis)
            : rawMesh;

        CenterPlacementOnMargin(manifest, oriented, marginCentroid);

        if (insertionAxis.LengthSquared > 1e-12)
        {
            var axis = insertionAxis;
            axis.Normalize();
            const double liftMm = 1.5;
            manifest.LibraryOffsetXmm += axis.X * liftMm;
            manifest.LibraryOffsetYmm += axis.Y * liftMm;
            manifest.LibraryOffsetZmm += axis.Z * liftMm;
        }

        return oriented;
    }

    public static IReadOnlyList<Point3D> ApplyPlacementTransform(
        IReadOnlyList<Point3D> points,
        StatsDesignManifest manifest)
    {
        var sx = manifest.LibraryScaleX * manifest.LibraryUniformScale;
        var sy = manifest.LibraryScaleY * manifest.LibraryUniformScale;
        var sz = manifest.LibraryScaleZ * manifest.LibraryUniformScale;
        return points
            .Select(p => new Point3D(
                (p.X * sx) + manifest.LibraryOffsetXmm,
                (p.Y * sy) + manifest.LibraryOffsetYmm,
                (p.Z * sz) + manifest.LibraryOffsetZmm))
            .ToList();
    }

    public static IReadOnlyList<Point3D> ReadWorldMarginLoop(string libraryDcmPath, StatsDesignManifest manifest)
    {
        var local = StatsDesignLibraryMarginReader.ReadMarginLoop(libraryDcmPath);
        return ApplyPlacementTransform(local, manifest);
    }

    public static MeshSnapshot ApplyPlacementTransform(MeshSnapshot mesh, StatsDesignManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(manifest);

        var sx = manifest.LibraryScaleX * manifest.LibraryUniformScale;
        var sy = manifest.LibraryScaleY * manifest.LibraryUniformScale;
        var sz = manifest.LibraryScaleZ * manifest.LibraryUniformScale;
        if (Math.Abs(sx) < 1e-6 || Math.Abs(sy) < 1e-6 || Math.Abs(sz) < 1e-6)
        {
            throw new InvalidOperationException("Library tooth scale must be non-zero.");
        }

        var transformed = new Point3D[mesh.Positions.Length];
        for (var i = 0; i < mesh.Positions.Length; i++)
        {
            var p = mesh.Positions[i];
            transformed[i] = new Point3D(
                (p.X * sx) + manifest.LibraryOffsetXmm,
                (p.Y * sy) + manifest.LibraryOffsetYmm,
                (p.Z * sz) + manifest.LibraryOffsetZmm);
        }

        return new MeshSnapshot(transformed, mesh.TriangleIndices);
    }

    public static void CenterPlacementOnMargin(StatsDesignManifest manifest, MeshSnapshot mesh, Point3D marginCentroid)
    {
        if (mesh.Positions.Length == 0)
        {
            return;
        }

        var bounds = mesh.Bounds;
        var meshCenter = new Point3D(
            bounds.X + (bounds.SizeX * 0.5),
            bounds.Y + (bounds.SizeY * 0.5),
            bounds.Z + (bounds.SizeZ * 0.5));

        manifest.LibraryOffsetXmm = marginCentroid.X - meshCenter.X;
        manifest.LibraryOffsetYmm = marginCentroid.Y - meshCenter.Y;
        manifest.LibraryOffsetZmm = marginCentroid.Z - meshCenter.Z;
    }
}

public sealed class StatsDesignToothLibraryEntry
{
    public required int ToothNumber { get; init; }
    public required string FilePath { get; init; }
    public required string DisplayName { get; init; }
    public bool IsOad { get; init; }
}
