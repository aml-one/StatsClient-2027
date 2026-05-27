using StatsClient.MVVM.Model;
using System.IO;
using DCMViewer.Services;
using System.Xml.Linq;

namespace StatsClient.MVVM.Core;

public static class DCMFinder
{
    public static DCMFinderResult FindForCase(ThreeShapeOrdersModel threeShapeObject)
    {
        ArgumentNullException.ThrowIfNull(threeShapeObject);

        if (string.IsNullOrWhiteSpace(threeShapeObject.IntOrderID))
        {
            return new DCMFinderResult
            {
                Warnings = ["Order has no IntOrderID."]
            };
        }

        string orderFolder = ResolveOrderFolder(threeShapeObject);
        return FindFromOrderFolder(orderFolder);
    }

    private static string ResolveOrderFolder(ThreeShapeOrdersModel threeShapeObject)
    {
        if (!string.IsNullOrWhiteSpace(threeShapeObject.OrderFolderPath) &&
            Directory.Exists(threeShapeObject.OrderFolderPath))
        {
            return threeShapeObject.OrderFolderPath;
        }

        if (!string.IsNullOrWhiteSpace(threeShapeObject.XmlFilePath))
        {
            string? xmlBasedFolder = Path.GetDirectoryName(threeShapeObject.XmlFilePath);
            if (!string.IsNullOrWhiteSpace(xmlBasedFolder) && Directory.Exists(xmlBasedFolder))
            {
                return xmlBasedFolder;
            }
        }

        string threeShapeDirectoryHelper = DatabaseOperations.GetServerFileDirectory();
        return $"{threeShapeDirectoryHelper}{threeShapeObject.IntOrderID}";
    }

    public static DCMFinderResult FindFromOrderFolder(string orderFolder)
    {
        var result = new DCMFinderResult
        {
            OrderFolderPath = orderFolder ?? string.Empty,
            XmlFilePath = BuildXmlPath(orderFolder)
        };

        if (string.IsNullOrWhiteSpace(orderFolder) || !Directory.Exists(orderFolder))
        {
            result.Warnings.Add("Order folder does not exist.");
            return result;
        }

        if (!File.Exists(result.XmlFilePath))
        {
            result.Warnings.Add("Order XML file does not exist.");
            return result;
        }

        XDocument document;
        try
        {
            document = XDocument.Load(result.XmlFilePath, LoadOptions.None);
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Could not load order XML: {ex.Message}");
            return result;
        }

        IReadOnlyList<XElement> orderItems = GetListItems(document, "OrderList");
        IReadOnlyList<XElement> modelElementItems = GetListItems(document, "ModelElementList");
        IReadOnlyList<XElement> toothElementItems = GetListItems(document, "ToothElementList");
        IReadOnlyList<XElement> scanItems = GetListItems(document, "ScanList");

        var orderProperties = orderItems.Select(GetProperties).FirstOrDefault()
                              ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        result.IsDigitalCase = IsDigitalCase(orderProperties, scanItems.Select(GetProperties));

        HashSet<string> preparedArches = GetPreparedArches(modelElementItems, toothElementItems);

        foreach (XElement modelElementItem in modelElementItems)
        {
            Dictionary<string, string> properties = GetProperties(modelElementItem);
            string processStatusId = GetProperty(properties, "ProcessStatusID");
            string modelFilename = GetProperty(properties, "ModelFilename");

            // Skip elements that have not been designed yet (no file produced)
            bool isNotDesignedStatus =
                string.IsNullOrWhiteSpace(processStatusId)
                || string.Equals(processStatusId, "psCreated", StringComparison.OrdinalIgnoreCase)
                || string.Equals(processStatusId, "psScanning", StringComparison.OrdinalIgnoreCase)
                || string.Equals(processStatusId, "psScanned", StringComparison.OrdinalIgnoreCase);

            if (isNotDesignedStatus || string.IsNullOrWhiteSpace(modelFilename))
            {
                continue;
            }

            result.HasDesignedElements = true;
            if (string.IsNullOrWhiteSpace(modelFilename))
            {
                continue;
            }

            string filePath = Path.Combine(orderFolder, NormalizeRelativePath(modelFilename));
            if (!File.Exists(filePath))
            {
                result.Warnings.Add($"Designed DCM file not found: {modelFilename}");
                continue;
            }

            result.DesignedElements.Add(new DCMFileItem
            {
                FilePath = filePath,
                RelativePath = modelFilename,
                DisplayName = Path.GetFileNameWithoutExtension(filePath),
                MaterialName = IsCadJawModel(modelFilename) ? "Model" : MapMaterial(GetProperty(properties, "CacheMaterialName")),
                GroupName = MapItemGroup(GetProperty(properties, "Items")),
                SourceKind = DCMFileSourceKind.DesignedElement,
                IsDesigned = true
            });
        }

        AddModelScans(result, orderFolder, preparedArches);
        RemoveDuplicateFiles(result);

        return result;
    }

    private static void AddModelScans(DCMFinderResult result, string orderFolder, IReadOnlySet<string> preparedArches)
    {
        string scansFolder = Path.Combine(orderFolder, "Scans");
        if (!Directory.Exists(scansFolder))
        {
            result.Warnings.Add("Scans folder does not exist.");
            return;
        }

        string modelScanMaterial = result.IsDigitalCase ? "Model" : "Stone";
        bool bothArchesArePreparation = preparedArches.Contains("Upper") && preparedArches.Contains("Lower");

        foreach (string folderName in new[] { "Upper", "Lower" })
        {
            string currentFolder = Path.Combine(scansFolder, folderName);
            if (!Directory.Exists(currentFolder))
            {
                continue;
            }

            var allCandidates = Directory
                .EnumerateFiles(currentFolder, "*.dcm", SearchOption.AllDirectories)
                .ToList();
            var preparationCandidates = new List<string>();
            var antagonistCandidates = new List<string>();

            foreach (string file in allCandidates)
            {
                string normalizedName = NormalizeName(Path.GetFileNameWithoutExtension(file));
                if (IsPreparationScan(normalizedName))
                {
                    preparationCandidates.Add(file);
                    continue;
                }

                if (IsAntagonistScan(normalizedName))
                {
                    antagonistCandidates.Add(file);
                }
            }

            bool isPreparationArch = preparedArches.Contains(folderName);

            if (bothArchesArePreparation)
            {
                AddPreferredScan(
                    result.ModelScans,
                    preparationCandidates.Count > 0 ? preparationCandidates : antagonistCandidates.Count > 0 ? antagonistCandidates : allCandidates,
                    modelScanMaterial,
                    $"{folderName} Preparation",
                    DCMFileSourceKind.ModelScan,
                    orderFolder);
                continue;
            }

            if (isPreparationArch)
            {
                AddPreferredScan(
                    result.ModelScans,
                    preparationCandidates.Count > 0 ? preparationCandidates : antagonistCandidates.Count > 0 ? antagonistCandidates : allCandidates,
                    modelScanMaterial,
                    $"{folderName} Preparation",
                    DCMFileSourceKind.ModelScan,
                    orderFolder);
            }
            else if (preparedArches.Count > 0)
            {
                AddPreferredScan(
                    result.ModelScans,
                    antagonistCandidates.Count > 0 ? antagonistCandidates : preparationCandidates.Count > 0 ? preparationCandidates : allCandidates,
                    modelScanMaterial,
                    $"{folderName} Antagonist",
                    DCMFileSourceKind.ModelScan,
                    orderFolder);
            }
            else
            {
                AddPreferredScan(result.ModelScans, preparationCandidates.Count > 0 ? preparationCandidates : allCandidates, modelScanMaterial, $"{folderName} Preparation", DCMFileSourceKind.ModelScan, orderFolder);
                AddPreferredScan(result.ModelScans, antagonistCandidates.Count > 0 ? antagonistCandidates : allCandidates, modelScanMaterial, $"{folderName} Antagonist", DCMFileSourceKind.ModelScan, orderFolder);
            }
        }

        string miscFolder = Path.Combine(scansFolder, "Misc");
        if (!Directory.Exists(miscFolder))
        {
            return;
        }

        var miscDcmFiles = Directory.EnumerateFiles(miscFolder, "*.dcm", SearchOption.AllDirectories).ToList();
        bool hasMultipleBiteFiles = miscDcmFiles.Count(file => NormalizeName(Path.GetFileNameWithoutExtension(file)).Contains("bite", StringComparison.Ordinal)) > 1;

        foreach (string file in miscDcmFiles)
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            string normalizedName = NormalizeName(fileName);
            bool isBiteScan = normalizedName.Contains("bite", StringComparison.Ordinal);

            string groupName = isBiteScan
                ? hasMultipleBiteFiles ? "Bite" : "Misc"
                : "Misc";

            result.ModelScans.Add(new DCMFileItem
            {
                FilePath = file,
                RelativePath = Path.GetRelativePath(orderFolder, file),
                DisplayName = fileName,
                MaterialName = PrepScanMaterialRules.IsPreopScan(file) ? PrepScanMaterialRules.TextureName : modelScanMaterial,
                GroupName = groupName,
                SourceKind = DCMFileSourceKind.ModelScan,
                IsDesigned = false,
                StartHidden = isBiteScan
            });
        }
    }

    private static bool IsCadJawModel(string modelFilename)
    {
        string normalized = NormalizeName(Path.GetFileNameWithoutExtension(modelFilename));
        return normalized.Contains("upperjaw", StringComparison.Ordinal)
            || normalized.Contains("lowerjaw", StringComparison.Ordinal);
    }

    private static HashSet<string> GetPreparedArches(
        IReadOnlyList<XElement> modelElementItems,
        IReadOnlyList<XElement> toothElementItems)
    {
        Dictionary<string, string> modelGroups = modelElementItems
            .Select(GetProperties)
            .Where(properties =>
            {
                string statusId = GetProperty(properties, "ProcessStatusID");
                bool isNotDesigned =
                    string.IsNullOrWhiteSpace(statusId)
                    || string.Equals(statusId, "psCreated", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(statusId, "psScanning", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(statusId, "psScanned", StringComparison.OrdinalIgnoreCase);
                return !isNotDesigned && !string.IsNullOrWhiteSpace(GetProperty(properties, "ModelFilename"));
            })
            .Select(properties => new
            {
                ModelElementId = GetProperty(properties, "ModelElementID"),
                GroupName = MapItemGroup(GetProperty(properties, "Items")),
                Items = GetProperty(properties, "Items")
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.ModelElementId))
            .ToDictionary(x => x.ModelElementId, x => $"{x.GroupName}|{x.Items}", StringComparer.OrdinalIgnoreCase);

        HashSet<string> preparedArches = new(StringComparer.OrdinalIgnoreCase);

        foreach (string value in modelGroups.Values)
        {
            string[] parts = value.Split('|', 2);
            string groupName = parts[0];
            string items = parts.Length > 1 ? parts[1] : string.Empty;

            if (string.Equals(groupName, "Model/Die", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (int toothNumber in ExtractToothNumbers(items))
            {
                string archFromItems = GetArchFromUniversalToothNumber(toothNumber);
                if (!string.IsNullOrWhiteSpace(archFromItems))
                {
                    preparedArches.Add(archFromItems);
                }
            }
        }

        if (preparedArches.Count > 0)
        {
            return preparedArches;
        }

        foreach (XElement toothElementItem in toothElementItems)
        {
            Dictionary<string, string> properties = GetProperties(toothElementItem);
            string modelElementId = GetProperty(properties, "ModelElementID");
            if (string.IsNullOrWhiteSpace(modelElementId)
                || !modelGroups.TryGetValue(modelElementId, out string? value))
            {
                continue;
            }

            string groupName = value.Split('|', 2)[0];
            if (string.Equals(groupName, "Model/Die", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string arch = GetArchFromToothNumber(GetProperty(properties, "ToothNumber"));
            if (!string.IsNullOrWhiteSpace(arch))
            {
                preparedArches.Add(arch);
            }
        }

        return preparedArches;
    }

    private static IEnumerable<int> ExtractToothNumbers(string items)
    {
        foreach (string part in items.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string trailingNumber = new(part.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
            if (int.TryParse(trailingNumber, out int toothNumber))
            {
                yield return toothNumber;
            }
        }
    }

    private static string GetArchFromUniversalToothNumber(int toothNumber)
    {
        if (toothNumber is >= 1 and <= 16)
        {
            return "Upper";
        }

        if (toothNumber is >= 17 and <= 32)
        {
            return "Lower";
        }

        return string.Empty;
    }

    private static void AddPreferredScan(
        ICollection<DCMFileItem> target,
        IReadOnlyCollection<string> candidates,
        string materialName,
        string groupName,
        DCMFileSourceKind sourceKind,
        string orderFolder)
    {
        if (candidates.Count == 0)
        {
            return;
        }

        string selectedFile = candidates
            .OrderBy(path => Path.GetFileName(path).StartsWith("Raw", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .First();

        target.Add(new DCMFileItem
        {
            FilePath = selectedFile,
            RelativePath = Path.GetRelativePath(orderFolder, selectedFile),
            DisplayName = Path.GetFileNameWithoutExtension(selectedFile),
            MaterialName = materialName,
            GroupName = groupName,
            SourceKind = sourceKind,
            IsDesigned = false
        });
    }

    private static void RemoveDuplicateFiles(DCMFinderResult result)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        result.DesignedElements = result.DesignedElements
            .Where(item => seen.Add(string.IsNullOrWhiteSpace(item.RelativePath) ? item.FilePath : item.RelativePath))
            .ToList();

        result.ModelScans = result.ModelScans
            .Where(item => seen.Add(string.IsNullOrWhiteSpace(item.RelativePath) ? item.FilePath : item.RelativePath))
            .ToList();
    }

    private static string GetArchFromToothNumber(string toothNumber)
    {
        string trimmed = toothNumber.Trim();
        if (!int.TryParse(trimmed, out int number))
        {
            return string.Empty;
        }

        if (trimmed.Length == 2)
        {
            int quadrant = number / 10;
            int toothInQuadrant = number % 10;
            if (toothInQuadrant is >= 1 and <= 8)
            {
                if (quadrant is 1 or 2)
                {
                    return "Upper";
                }

                if (quadrant is 3 or 4)
                {
                    return "Lower";
                }
            }
        }

        if (number is >= 1 and <= 16)
        {
            return "Upper";
        }

        if (number is >= 17 and <= 32)
        {
            return "Lower";
        }

        return string.Empty;
    }

    private static bool IsDigitalCase(
        IReadOnlyDictionary<string, string> orderProperties,
        IEnumerable<IReadOnlyDictionary<string, string>> scanItems)
    {
        string scanSource = GetProperty(orderProperties, "ScanSource");
        string normalizedScanSource = NormalizeName(scanSource);
        if (normalizedScanSource.Contains("itero", StringComparison.Ordinal)
            || normalizedScanSource.Contains("trios", StringComparison.Ordinal)
            || normalizedScanSource.Contains("import", StringComparison.Ordinal))
        {
            return true;
        }

        return scanItems.Any(scan => NormalizeName(GetProperty(scan, "ScanType")).Contains("intraoral", StringComparison.Ordinal));
    }

    private static string MapMaterial(string? cacheMaterialName)
    {
        string normalized = NormalizeName(cacheMaterialName);

        if (normalized.Contains("pmma", StringComparison.Ordinal))
            return "PMMA";

        if (normalized.Contains("zirconia", StringComparison.Ordinal)
            || normalized.Contains("zircon", StringComparison.Ordinal))
            return "Zirconia";

        if (normalized.Contains("wax", StringComparison.Ordinal)
            || normalized.Contains("composit", StringComparison.Ordinal)
            || normalized.Contains("prismatic", StringComparison.Ordinal))
            return "WAX";

        if (normalized.Contains("cocr", StringComparison.Ordinal)
            || normalized.Contains("slm", StringComparison.Ordinal)
            || normalized.Contains(" titanium ", StringComparison.Ordinal)
            || normalized.StartsWith("ti ", StringComparison.Ordinal)
            || normalized.EndsWith(" ti", StringComparison.Ordinal)
            || normalized.Contains(" ti ", StringComparison.Ordinal)
            || normalized.Equals("ti", StringComparison.Ordinal))
            return "SLM";

        if (normalized.Contains("gold", StringComparison.Ordinal))
            return "Gold";

        if (normalized.Contains("e max", StringComparison.Ordinal)
            || normalized.Contains("emax", StringComparison.Ordinal)
            || normalized.Contains("lisi", StringComparison.Ordinal))
            return "Emax";

        if (normalized.Contains("model", StringComparison.Ordinal))
            return "Stone";

        return "Zirconia";
    }

    private static string MapItemGroup(string? items)
    {
        string normalized = NormalizeName(items);

        if (normalized.Contains("model", StringComparison.Ordinal)
            || normalized.Contains("die", StringComparison.Ordinal))
            return "Model/Die";

        if (normalized.Contains("abutment", StringComparison.Ordinal))
            return "Abutment";

        return "Restoration";
    }

    private static bool IsPreparationScan(string normalizedName)
        => normalizedName.Contains("preparationscan", StringComparison.Ordinal)
           || normalizedName.Contains("preparation scan", StringComparison.Ordinal)
           || normalizedName.Contains("mbpreparationscan", StringComparison.Ordinal)
           || normalizedName.Contains("mb preparation scan", StringComparison.Ordinal)
           || normalizedName.Contains("rawpreparationscan", StringComparison.Ordinal)
           || normalizedName.Contains("raw preparation scan", StringComparison.Ordinal);

    private static bool IsAntagonistScan(string normalizedName)
        => normalizedName.Contains("antagonistscan", StringComparison.Ordinal)
           || normalizedName.Contains("antagonist scan", StringComparison.Ordinal)
           || normalizedName.Contains("mbantagonistscan", StringComparison.Ordinal)
           || normalizedName.Contains("mb antagonist scan", StringComparison.Ordinal)
           || normalizedName.Contains("rawantagonistscan", StringComparison.Ordinal)
           || normalizedName.Contains("raw antagonist scan", StringComparison.Ordinal);

    private static IReadOnlyList<XElement> GetListItems(XDocument document, string listObjectName)
    {
        XElement? listObject = document
            .Descendants("Object")
            .FirstOrDefault(x => string.Equals((string?)x.Attribute("name"), listObjectName, StringComparison.OrdinalIgnoreCase));

        return listObject?
            .Element("List")?
            .Elements("Object")
            .ToList()
            ?? [];
    }

    private static Dictionary<string, string> GetProperties(XElement objectElement)
        => objectElement
            .Elements("Property")
            .Where(x => x.Attribute("name") is not null)
            .GroupBy(x => (string?)x.Attribute("name") ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => (string?)x.Last().Attribute("value") ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

    private static string GetProperty(IReadOnlyDictionary<string, string> properties, string name)
        => properties.TryGetValue(name, out string? value) ? value ?? string.Empty : string.Empty;

    private static string BuildXmlPath(string? orderFolder)
    {
        if (string.IsNullOrWhiteSpace(orderFolder))
        {
            return string.Empty;
        }

        string folderName = Path.GetFileName(orderFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return Path.Combine(orderFolder, $"{folderName}.xml");
    }

    private static string NormalizeRelativePath(string value)
        => value.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

    private static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        char[] normalizedChars = value
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ')
            .ToArray();

        return $" {new string(normalizedChars)} ";
    }
}

public sealed class DCMFinderResult
{
    public string OrderFolderPath { get; set; } = string.Empty;
    public string XmlFilePath { get; set; } = string.Empty;
    public bool IsDigitalCase { get; set; }
    public bool HasDesignedElements { get; set; }
    public List<DCMFileItem> DesignedElements { get; set; } = [];
    public List<DCMFileItem> ModelScans { get; set; } = [];
    public List<string> Warnings { get; set; } = [];

    public IReadOnlyList<DCMFileItem> AllFiles => DesignedElements.Concat(ModelScans).ToList();
}

public sealed class DCMFileItem
{
    public string FilePath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public DCMFileSourceKind SourceKind { get; set; }
    public bool IsDesigned { get; set; }
    public bool StartHidden { get; set; }
}

public enum DCMFileSourceKind
{
    DesignedElement,
    ModelScan
}
