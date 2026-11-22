using System.Xml.Linq;

namespace StatsClient.MVVM.Core.OrderCreation;

internal sealed class OrderCreationConfigResolver
{
    private readonly Dictionary<string, OrderCreationMaterialConfig> _materialsByName;

    public OrderCreationConfigResolver(string globalMaterialsXmlPath)
    {
        XDocument doc = XDocument.Load(globalMaterialsXmlPath);
        _materialsByName = doc
            .Descendants("Object")
            .Where(x => (string?)x.Attribute("type") == "TDM_Item_Material")
            .Select(x =>
            {
                string name = GetPropertyValue(x, "Name");
                string creator = GetPropertyValue(x, "CreatorSiteID");
                string itemId = GetPropertyValue(x, "ItemID");
                return new OrderCreationMaterialConfig(name, creator, itemId);
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    public OrderCreationMaterialConfig ResolveMaterial(string materialName)
    {
        if (_materialsByName.TryGetValue(materialName, out OrderCreationMaterialConfig? value))
        {
            return value;
        }

        throw new InvalidOperationException($"Material '{materialName}' not found in global materials source.");
    }

    public string NormalizeMaterialForCase(string caseType, string materialName)
    {
        if ((caseType.Equals("crown", StringComparison.OrdinalIgnoreCase)
            || caseType.Equals("crownOnAbutment", StringComparison.OrdinalIgnoreCase))
            && materialName.Equals("Ivoclar IPS e.max CAD C16 (Veneer)", StringComparison.OrdinalIgnoreCase)
            && _materialsByName.ContainsKey("Ivoclar IPS e.max CAD C16 (Crown)"))
        {
            return "Ivoclar IPS e.max CAD C16 (Crown)";
        }

        return materialName;
    }

    public static string GetMaterialId(OrderCreationMaterialConfig cfg) => $"{cfg.CreatorSiteID}_{cfg.ItemID}";

    public static string ResolveToothElementTypeId(string caseType, string materialName, bool isPontic)
    {
        if (caseType.Equals("abutment", StringComparison.OrdinalIgnoreCase))
        {
            return "Abutment1";
        }

        if (isPontic)
        {
            return "Configuration_11_ICCrownPonticWax10";
        }

        if (caseType.Equals("veneer", StringComparison.OrdinalIgnoreCase))
        {
            return "Configuration_02_IPSemaxCADC16Veneer";
        }

        if (caseType.Equals("crown", StringComparison.OrdinalIgnoreCase)
            || caseType.Equals("crownOnAbutment", StringComparison.OrdinalIgnoreCase))
        {
            if (materialName.Contains("Ivoclar IPS e.max CAD C16", StringComparison.OrdinalIgnoreCase))
            {
                return "Configuration_02_IPSemaxCADC16Crown";
            }

            return "Configuration_11_IDCrownZircon10";
        }

        return "Configuration_11_IDCrownZircon10";
    }

    public static string ResolveManufacturingProcessId(string caseType, string materialName)
    {
        if (caseType.Equals("veneer", StringComparison.OrdinalIgnoreCase)
            || materialName.Contains("Ivoclar", StringComparison.OrdinalIgnoreCase))
        {
            return "Configuration_02_RawSTL0655";
        }

        return "Configuration_11_ManufProcess30";
    }

    public static string ResolveCacheToothTypeClass(string caseType, bool isPontic)
    {
        if (caseType.Equals("abutment", StringComparison.OrdinalIgnoreCase))
        {
            return "teAbutment";
        }

        if (isPontic)
        {
            return "teCrownPontic";
        }

        if (caseType.Equals("veneer", StringComparison.OrdinalIgnoreCase))
        {
            return "teVeneer";
        }

        return "teCrown";
    }

    public static string ResolveColorId(string? shade)
    {
        if (string.IsNullOrWhiteSpace(shade))
        {
            return string.Empty;
        }

        string normalized = shade.Replace(" ", "_");
        return $"Configuration_02_{normalized}";
    }

    public static string GetPropertyValue(XElement element, string propertyName)
    {
        return (string?)element
            .Elements("Property")
            .FirstOrDefault(p => string.Equals((string?)p.Attribute("name"), propertyName, StringComparison.Ordinal))
            ?.Attribute("value") ?? string.Empty;
    }
}

internal sealed record OrderCreationMaterialConfig(string Name, string CreatorSiteID, string ItemID);
