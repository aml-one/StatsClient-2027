using System.IO;

namespace DCMViewer.ViewModels;

public enum ScanLayerArch
{
    None,
    Upper,
    Lower,
    Misc
}

public static class ScanLayerArchResolver
{
    public static ScanLayerArch Resolve(string filePath, string? groupNameHint = null)
    {
        var normalizedPath = filePath.Replace('/', '\\');

        // Folder path wins over group hint and filename heuristics (Scans\Upper, Scans\Lower, Scans\Misc).
        if (IsUnderScansFolder(normalizedPath, "Misc"))
        {
            return ScanLayerArch.Misc;
        }

        if (IsUnderScansFolder(normalizedPath, "Upper"))
        {
            return ScanLayerArch.Upper;
        }

        if (IsUnderScansFolder(normalizedPath, "Lower"))
        {
            return ScanLayerArch.Lower;
        }

        if (!string.IsNullOrWhiteSpace(groupNameHint))
        {
            if (groupNameHint.StartsWith("Misc", StringComparison.OrdinalIgnoreCase) ||
                groupNameHint.StartsWith("Bite", StringComparison.OrdinalIgnoreCase))
            {
                return ScanLayerArch.Misc;
            }

            if (groupNameHint.StartsWith("Upper", StringComparison.OrdinalIgnoreCase))
            {
                return ScanLayerArch.Upper;
            }

            if (groupNameHint.StartsWith("Lower", StringComparison.OrdinalIgnoreCase))
            {
                return ScanLayerArch.Lower;
            }
        }

        var fileName = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return ScanLayerArch.None;
        }

        var normalizedName = fileName.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        if (normalizedName.Contains("upperjaw", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.StartsWith("upper", StringComparison.OrdinalIgnoreCase))
        {
            return ScanLayerArch.Upper;
        }

        if (normalizedName.Contains("lowerjaw", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.StartsWith("lower", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.Contains("antagonistscan", StringComparison.OrdinalIgnoreCase) ||
            (normalizedName.Contains("antagonist", StringComparison.OrdinalIgnoreCase) &&
             normalizedName.Contains("scan", StringComparison.OrdinalIgnoreCase)))
        {
            return ScanLayerArch.Lower;
        }

        if (normalizedName.Contains("preparationscan", StringComparison.OrdinalIgnoreCase) ||
            (normalizedName.Contains("preparation", StringComparison.OrdinalIgnoreCase) &&
             normalizedName.Contains("scan", StringComparison.OrdinalIgnoreCase)))
        {
            return ScanLayerArch.Upper;
        }

        return ScanLayerArch.None;
    }

    private static bool IsUnderScansFolder(string normalizedPath, string folderName)
    {
        return ContainsPathSegment(normalizedPath, "Scans", folderName) ||
               normalizedPath.Contains($@"\Scans\{folderName}\", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Contains($@"\Scans\{folderName}.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsPathSegment(string path, string segmentA, string segmentB)
    {
        var index = path.IndexOf(segmentA, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            var afterA = index + segmentA.Length;
            if (afterA < path.Length &&
                (path[afterA] == '\\' || path[afterA] == '/') &&
                path.AsSpan(afterA + 1).StartsWith(segmentB, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            index = path.IndexOf(segmentA, index + 1, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
