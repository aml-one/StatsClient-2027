using System.IO;

namespace StatsClient.MVVM.Core;

/// <summary>
/// Stamp placement profiles loaded from colored calibration PNGs in
/// <c>Images/PrescriptionCalibrations/</c> (copied to output directory).
/// </summary>
public static class PrescriptionStampProfiles
{
    private static readonly object Gate = new();
    private static Dictionary<string, PrescriptionStampProfile>? _cache;

    private static readonly (string Code, string ImageFile)[] Map =
    [
        ("0", "ITERO.png"),
        ("1", "ITERO.png"),
        ("3", "IS3D.png"),
        ("4", "TRIOS.png"),
        ("5", "MEDIT.png"),
        ("6", "DSCORE.png"),
        ("7", "ASCONNECT.png"),
        ("8", "DEXIS.png"),
    ];

    public static PrescriptionStampProfile? ForPageHeaderIsHigh(string pageHeaderIsHigh)
    {
        if (string.IsNullOrWhiteSpace(pageHeaderIsHigh))
        {
            return null;
        }

        EnsureLoaded();
        return _cache!.TryGetValue(pageHeaderIsHigh, out PrescriptionStampProfile? profile) ? profile : null;
    }

    public static string CalibrationFolder =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "PrescriptionCalibrations");

    private static void EnsureLoaded()
    {
        if (_cache is not null)
        {
            return;
        }

        lock (Gate)
        {
            if (_cache is not null)
            {
                return;
            }

            var loaded = new Dictionary<string, PrescriptionStampProfile>(StringComparer.Ordinal);
            foreach ((string code, string imageFile) in Map)
            {
                string path = Path.Combine(CalibrationFolder, imageFile);
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    loaded[code] = PrescriptionStampCalibrationReader.ReadProfile(code, path);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Stamp calibration skipped for code {code}: {ex.Message}");
                }
            }

            _cache = loaded;
        }
    }

    /// <summary>Reload profiles after calibration PNG updates (dev/diag).</summary>
    public static void Reload()
    {
        lock (Gate)
        {
            _cache = null;
            EnsureLoaded();
        }
    }

    public static IReadOnlyDictionary<string, PrescriptionStampProfile> AllProfiles()
    {
        EnsureLoaded();
        return _cache!;
    }
}
