using System.Globalization;

namespace StatsClient.MVVM.Core;

/// <summary>
/// Global pan-number stamp defaults per prescription system, stored in StatsDB dbo.Settings.
/// Coordinates are normalized 0–1 relative to page width/height.
/// </summary>
public static class PrescriptionPanPositionStore
{
    private const string PosKeyPrefix = "PrescriptionPanPos_";
    private const string FontKeyPrefix = "PrescriptionPanFont_";
    private const string RushPosKeyPrefix = "PrescriptionRushPos_";
    private const string RushFontKeyPrefix = "PrescriptionRushFont_";
    private const string InfoPosKeyPrefix = "PrescriptionInfoPos_";
    private const string InfoFontKeyPrefix = "PrescriptionInfoFont_";

    private static readonly object CacheGate = new();
    private static readonly Dictionary<string, string> Cache = new(StringComparer.Ordinal);

    public static bool TryGetNormalized(string pageHeaderIsHigh, out double x, out double y)
    {
        x = 0;
        y = 0;
        string code = PrescriptionSystemCatalog.NormalizeCode(pageHeaderIsHigh);
        string raw = ReadRaw(PosKeyPrefix + code);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        string[] parts = raw.Split(',');
        if (parts.Length != 2)
        {
            return false;
        }

        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x))
        {
            return false;
        }

        if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y))
        {
            return false;
        }

        return x is >= 0 and <= 1 && y is >= 0 and <= 1;
    }

    public static void SaveNormalized(string pageHeaderIsHigh, double x, double y)
    {
        string code = PrescriptionSystemCatalog.NormalizeCode(pageHeaderIsHigh);
        x = Math.Clamp(x, 0, 1);
        y = Math.Clamp(y, 0, 1);
        string value = $"{x.ToString(CultureInfo.InvariantCulture)},{y.ToString(CultureInfo.InvariantCulture)}";
        WriteRaw(PosKeyPrefix + code, value);
    }

    public static bool TryGetFontSize(string pageHeaderIsHigh, out float fontSize)
    {
        fontSize = 0;
        string code = PrescriptionSystemCatalog.NormalizeCode(pageHeaderIsHigh);
        string raw = ReadRaw(FontKeyPrefix + code);
        if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out fontSize))
        {
            return false;
        }

        return fontSize is >= 14f and <= 72f;
    }

    public static void SaveFontSize(string pageHeaderIsHigh, float fontSize)
    {
        string code = PrescriptionSystemCatalog.NormalizeCode(pageHeaderIsHigh);
        fontSize = Math.Clamp(fontSize, 14f, 72f);
        WriteRaw(
            FontKeyPrefix + code,
            fontSize.ToString(CultureInfo.InvariantCulture));
    }

    public static bool TryGetRushNormalized(string pageHeaderIsHigh, out double x, out double y)
    {
        x = 0;
        y = 0;
        string code = PrescriptionSystemCatalog.NormalizeCode(pageHeaderIsHigh);
        string raw = ReadRaw(RushPosKeyPrefix + code);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        string[] parts = raw.Split(',');
        if (parts.Length != 2)
        {
            return false;
        }

        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x))
        {
            return false;
        }

        if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y))
        {
            return false;
        }

        return x is >= 0 and <= 1 && y is >= 0 and <= 1;
    }

    public static void SaveRushNormalized(string pageHeaderIsHigh, double x, double y)
    {
        string code = PrescriptionSystemCatalog.NormalizeCode(pageHeaderIsHigh);
        x = Math.Clamp(x, 0, 1);
        y = Math.Clamp(y, 0, 1);
        string value = $"{x.ToString(CultureInfo.InvariantCulture)},{y.ToString(CultureInfo.InvariantCulture)}";
        WriteRaw(RushPosKeyPrefix + code, value);
    }

    public static bool TryGetRushFontSize(string pageHeaderIsHigh, out float fontSize)
    {
        fontSize = 0;
        string code = PrescriptionSystemCatalog.NormalizeCode(pageHeaderIsHigh);
        string raw = ReadRaw(RushFontKeyPrefix + code);
        if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out fontSize))
        {
            return false;
        }

        return fontSize is >= 14f and <= 72f;
    }

    public static void SaveRushFontSize(string pageHeaderIsHigh, float fontSize)
    {
        string code = PrescriptionSystemCatalog.NormalizeCode(pageHeaderIsHigh);
        fontSize = Math.Clamp(fontSize, 14f, 72f);
        WriteRaw(
            RushFontKeyPrefix + code,
            fontSize.ToString(CultureInfo.InvariantCulture));
    }

    public static bool TryGetInfoNormalized(string pageHeaderIsHigh, out double x, out double y)
    {
        x = 0;
        y = 0;
        string code = PrescriptionSystemCatalog.NormalizeCode(pageHeaderIsHigh);
        string raw = ReadRaw(InfoPosKeyPrefix + code);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        string[] parts = raw.Split(',');
        if (parts.Length != 2)
        {
            return false;
        }

        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x))
        {
            return false;
        }

        if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y))
        {
            return false;
        }

        return x is >= 0 and <= 1 && y is >= 0 and <= 1;
    }

    public static void SaveInfoNormalized(string pageHeaderIsHigh, double x, double y)
    {
        string code = PrescriptionSystemCatalog.NormalizeCode(pageHeaderIsHigh);
        x = Math.Clamp(x, 0, 1);
        y = Math.Clamp(y, 0, 1);
        string value = $"{x.ToString(CultureInfo.InvariantCulture)},{y.ToString(CultureInfo.InvariantCulture)}";
        WriteRaw(InfoPosKeyPrefix + code, value);
    }

    public static bool TryGetInfoFontSize(string pageHeaderIsHigh, out float fontSize)
    {
        fontSize = 0;
        string code = PrescriptionSystemCatalog.NormalizeCode(pageHeaderIsHigh);
        string raw = ReadRaw(InfoFontKeyPrefix + code);
        if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out fontSize))
        {
            return false;
        }

        return fontSize is >= 10f and <= 72f;
    }

    public static void SaveInfoFontSize(string pageHeaderIsHigh, float fontSize)
    {
        string code = PrescriptionSystemCatalog.NormalizeCode(pageHeaderIsHigh);
        fontSize = Math.Clamp(fontSize, 10f, 72f);
        WriteRaw(
            InfoFontKeyPrefix + code,
            fontSize.ToString(CultureInfo.InvariantCulture));
    }

    private static string ReadRaw(string key)
    {
        lock (CacheGate)
        {
            if (Cache.TryGetValue(key, out string? cached))
            {
                return cached;
            }
        }

        string value = DatabaseConnection.ReadStatsSetting(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            string legacy = LocalSettingsDB.ReadLocalSetting(key);
            if (!string.IsNullOrWhiteSpace(legacy))
            {
                DatabaseConnection.WriteStatsSetting(key, legacy);
                value = legacy;
            }
        }

        lock (CacheGate)
        {
            Cache[key] = value;
        }

        return value;
    }

    private static void WriteRaw(string key, string value)
    {
        DatabaseConnection.WriteStatsSetting(key, value);
        lock (CacheGate)
        {
            Cache[key] = value;
        }
    }
}
