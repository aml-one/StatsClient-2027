using System.IO;
using System.Reflection;
using System.Xml.Linq;

namespace StatsClient.MVVM.Core.OrderCreation;

internal static class OrderCreationEmbeddedAssets
{
    private const string TemplateOrderResourceName = "StatsClient.Assets.OrderCreation.TemplateOrder.xml";
    private const string ManufacturersResourceName = "StatsClient.Assets.OrderCreation.Manufacturers.3ml";

    public static XDocument LoadTemplateOrderXml()
    {
        using Stream stream = OpenRequiredResource(TemplateOrderResourceName);
        return XDocument.Load(stream);
    }

    public static string CopyManufacturersFile(string outputFolderPath)
    {
        string outputPath = Path.Combine(outputFolderPath, "Manufacturers.3ml");
        using Stream stream = OpenRequiredResource(ManufacturersResourceName);
        using FileStream output = File.Create(outputPath);
        stream.CopyTo(output);
        return outputPath;
    }

    private static Stream OpenRequiredResource(string resourceName)
    {
        Assembly assembly = typeof(OrderCreationEmbeddedAssets).Assembly;
        Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        }

        return stream;
    }
}
