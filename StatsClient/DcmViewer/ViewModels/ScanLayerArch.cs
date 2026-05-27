using System.IO;

namespace DCMViewer.ViewModels;

public enum ScanLayerArch
{
    None,
    Upper,
    Lower
}

public static class ScanLayerArchResolver
{
    public static ScanLayerArch Resolve(string filePath, string? groupNameHint = null)
    {
        if (!string.IsNullOrWhiteSpace(groupNameHint))
        {
            if (groupNameHint.StartsWith("Upper", StringComparison.OrdinalIgnoreCase))
            {
                return ScanLayerArch.Upper;
            }

            if (groupNameHint.StartsWith("Lower", StringComparison.OrdinalIgnoreCase))
            {
                return ScanLayerArch.Lower;
            }
        }

        var normalizedPath = filePath.Replace('/', '\\');
        if (ContainsPathSegment(normalizedPath, "Scans", "Upper") ||
            ContainsPathSegment(normalizedPath, "Upper", "Scans") ||
            normalizedPath.Contains(@"\Scans\Upper\", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.Contains(@"\Scans\Upper.", StringComparison.OrdinalIgnoreCase))
        {
            return ScanLayerArch.Upper;
        }

        if (ContainsPathSegment(normalizedPath, "Scans", "Lower") ||
            ContainsPathSegment(normalizedPath, "Lower", "Scans") ||
            normalizedPath.Contains(@"\Scans\Lower\", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.Contains(@"\Scans\Lower.", StringComparison.OrdinalIgnoreCase))
        {
            return ScanLayerArch.Lower;
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
            normalizedName.StartsWith("lower", StringComparison.OrdinalIgnoreCase))
        {
            return ScanLayerArch.Lower;
        }

        return ScanLayerArch.None;
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
