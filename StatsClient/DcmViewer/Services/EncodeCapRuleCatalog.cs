namespace DCMViewer.Services;

internal enum EncodeCapProfile
{
    Unknown,
    Legacy,
    Emergence
}

internal enum EncodeCapFamily
{
    Unknown,
    Certain,
    Tsv
}

internal static class EncodeCapRuleCatalog
{
    public static string? ResolveThreeShapeSuggestion(
        EncodeCapProfile profile,
        EncodeCapFamily family,
        double platformMm,
        int? emergenceHeightMm,
        int? legacyHeightMm,
        double diameterToleranceMm = 0.35)
    {
        if (profile == EncodeCapProfile.Unknown || family == EncodeCapFamily.Unknown)
        {
            return null;
        }

        if (!TryMatchPlatform(family, platformMm, diameterToleranceMm, out double canonicalPlatform))
        {
            return null;
        }

        return profile switch
        {
            EncodeCapProfile.Emergence => family switch
            {
                EncodeCapFamily.Certain => FormatEmergenceCertain(canonicalPlatform),
                EncodeCapFamily.Tsv => FormatEmergenceTsv(canonicalPlatform),
                _ => null
            },
            EncodeCapProfile.Legacy => family switch
            {
                EncodeCapFamily.Certain => FormatLegacyCertain(canonicalPlatform),
                EncodeCapFamily.Tsv => FormatLegacyTsv(canonicalPlatform),
                _ => null
            },
            _ => null
        };
    }

    public static int? EmergenceHeightFromRightDimples(int dimplesRight) => dimplesRight switch
    {
        0 => 3,
        1 => 5,
        2 => 7,
        _ => null
    };

    public static double? EmergencePlatformFromDimplesBelow(EncodeCapFamily family, int dimplesBelow) =>
        family switch
        {
            EncodeCapFamily.Certain => dimplesBelow switch
            {
                0 => 3.4,
                1 => 4.1,
                2 => 5.0,
                3 => 6.0,
                _ => null
            },
            EncodeCapFamily.Tsv => dimplesBelow switch
            {
                0 => 3.5,
                1 => 4.5,
                2 => 5.7,
                _ => null
            },
            _ => null
        };

    private static bool TryMatchPlatform(EncodeCapFamily family, double measuredMm, double tolerance, out double canonical)
    {
        double[] platforms = family switch
        {
            EncodeCapFamily.Certain => [3.4, 4.1, 5.0, 6.0],
            EncodeCapFamily.Tsv => [3.5, 4.5, 5.7],
            _ => []
        };

        canonical = 0;
        double bestDelta = double.MaxValue;
        foreach (double p in platforms)
        {
            double delta = Math.Abs(measuredMm - p);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                canonical = p;
            }
        }

        return bestDelta <= tolerance;
    }

    private static string FormatLegacyCertain(double platform) => platform switch
    {
        3.4 => "IEHA 3.4",
        4.1 => "IEHA 4.1",
        5.0 => "IEHA 5.0",
        6.0 => "IEHA 6.0",
        _ => $"IEHA {platform:0.0}"
    };

    private static string FormatEmergenceCertain(double platform) => platform switch
    {
        3.4 => "IEEHA 3.4",
        4.1 => "IEEHA 4.1",
        5.0 => "IEEHA 5.0",
        6.0 => "IEEHA 6.0",
        _ => $"IEEHA {platform:0.0}"
    };

    private static string FormatLegacyTsv(double platform) => platform switch
    {
        3.5 => "TEHA Ti-3.5",
        4.5 => "TEHA Ti-4.5",
        5.7 => "TEHA Ti-5.7",
        _ => $"TEHA Ti-{platform:0.0}"
    };

    private static string FormatEmergenceTsv(double platform) => platform switch
    {
        3.5 => "TEEHA Ti-3.5",
        4.5 => "TEEHA Ti-4.5",
        5.7 => "TEEHA Ti-5.7",
        _ => $"TEEHA Ti-{platform:0.0}"
    };
}
