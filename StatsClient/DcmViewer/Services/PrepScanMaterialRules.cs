using System.IO;

namespace DCMViewer.Services;

/// <summary>
/// PrePreparationScan and GenericDoublePrepScan always use Preop material (.dcm / .stl).
/// </summary>
public static class PrepScanMaterialRules
{
    public const string TextureName = "Preop";

    private static readonly string[] NamePatterns =
    [
        "PrePreparationScan",
        "GenericDoublePrepScan"
    ];

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dcm",
        ".stl"
    };

    public static bool IsPreopScan(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        if (!SupportedExtensions.Contains(Path.GetExtension(filePath)))
        {
            return false;
        }

        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var normalizedStem = NormalizeStem(fileName);
        var fullPath = filePath.Replace('\\', '/');

        foreach (var pattern in NamePatterns)
        {
            if (fileName.Contains(pattern, StringComparison.OrdinalIgnoreCase)
                || fullPath.Contains(pattern, StringComparison.OrdinalIgnoreCase)
                || normalizedStem.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeStem(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value.Where(char.IsLetterOrDigit).ToArray());
    }
}
