using StatsClient.MVVM.Core;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DCMViewer.Services;

internal static class StatsDesignManifestStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static StatsDesignManifest LoadOrCreate(string orderFolderPath, string orderId)
    {
        var path = StatsDesignPaths.GetManifestPath(orderFolderPath);
        if (!File.Exists(path))
        {
            var created = new StatsDesignManifest { OrderId = orderId };
            StatsDesignDefaults.ApplyToNewManifest(created);
            created.UiDefaultsVersion = StatsDesignDefaults.CurrentUiDefaultsVersion;
            return created;
        }

        try
        {
            var json = File.ReadAllText(path);
            var manifest = JsonSerializer.Deserialize<StatsDesignManifest>(json, JsonOptions);
            return manifest ?? new StatsDesignManifest { OrderId = orderId };
        }
        catch
        {
            return new StatsDesignManifest { OrderId = orderId };
        }
    }

    public static void Save(string orderFolderPath, StatsDesignManifest manifest)
    {
        var root = StatsDesignPaths.GetDesignRoot(orderFolderPath);
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(StatsDesignPaths.GetCadFolder(orderFolderPath));
        var path = StatsDesignPaths.GetManifestPath(orderFolderPath);
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        File.WriteAllText(path, json);
    }
}
