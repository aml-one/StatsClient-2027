using StatsClient.MVVM.Model;
using System.IO;
using System.Text.Json;

namespace StatsClient.MVVM.Core;

public static class PrescriptionSavedStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string StatePath(string pdfTempFolder, string panNumber) =>
        Path.Combine(pdfTempFolder, panNumber + ".rxstate.json");

    public static void Save(string pdfTempFolder, PrescriptionSavedState state)
    {
        Directory.CreateDirectory(pdfTempFolder);
        string path = StatePath(pdfTempFolder, state.PanNumber);
        File.WriteAllText(path, JsonSerializer.Serialize(state, JsonOptions));
    }

    public static PrescriptionSavedState? TryLoad(string pdfTempFolder, string panNumber)
    {
        string path = StatePath(pdfTempFolder, panNumber);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PrescriptionSavedState>(File.ReadAllText(path), JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
