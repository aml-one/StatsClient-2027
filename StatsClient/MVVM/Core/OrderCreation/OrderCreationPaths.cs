using System.IO;
using static StatsClient.MVVM.Core.DatabaseOperations;

namespace StatsClient.MVVM.Core.OrderCreation;

public static class OrderCreationPaths
{
    private const string ConfigurationShareRelativePath =
        @"3Shape Dental System Configuration\Dental System Control Panel\Materials.xml";

    public const string DefaultGlobalMaterialsXmlPath =
        @"C:\3Shape Configuration\DentalSystem\Dental System Control Panel\Materials.xml";

    public static string GetTempOutputRoot() =>
        Path.Combine(Path.GetTempPath(), "StatsClient", "OrderCreation");

    public static string ResolveGlobalMaterialsXmlPath()
    {
        string? networkPath = TryBuildNetworkMaterialsXmlPath(GetServerFileDirectory());
        if (!string.IsNullOrWhiteSpace(networkPath))
        {
            if (File.Exists(networkPath))
            {
                return networkPath;
            }

            throw new FileNotFoundException(
                "Global 3Shape Materials.xml was not found on the 3Shape server share.",
                networkPath);
        }

        if (File.Exists(DefaultGlobalMaterialsXmlPath))
        {
            return DefaultGlobalMaterialsXmlPath;
        }

        throw new FileNotFoundException(
            "Global 3Shape Materials.xml was not found. Configure ThreeShapeServer.FileDirectory or install Materials.xml locally.",
            DefaultGlobalMaterialsXmlPath);
    }

    internal static string? TryBuildNetworkMaterialsXmlPath(string? fileDirectory)
    {
        string? serverName = TryExtractUncServerName(fileDirectory);
        if (string.IsNullOrWhiteSpace(serverName))
        {
            return null;
        }

        return $@"\\{serverName}\{ConfigurationShareRelativePath}";
    }

    internal static string? TryExtractUncServerName(string? uncPath)
    {
        if (string.IsNullOrWhiteSpace(uncPath))
        {
            return null;
        }

        string normalized = uncPath.Trim().Replace('/', '\\').TrimEnd('\\');
        if (!normalized.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            string host = new Uri(normalized).Host;
            return string.IsNullOrWhiteSpace(host) ? null : host;
        }
        catch
        {
            string withoutPrefix = normalized[2..];
            int slashIndex = withoutPrefix.IndexOf('\\');
            if (slashIndex <= 0)
            {
                return null;
            }

            return withoutPrefix[..slashIndex];
        }
    }
}
