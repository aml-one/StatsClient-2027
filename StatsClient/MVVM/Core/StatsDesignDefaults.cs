using DCMViewer.Services;

namespace StatsClient.MVVM.Core;

/// <summary>
/// Default values for the Stats Design workflow UI (library placement, cement, etc.).
/// Adjust here instead of hunting through XAML and view models.
/// </summary>
public static class StatsDesignDefaults
{
    /// <summary>Increment when defaults in this file change so existing cases pick them up once.</summary>
    public const int CurrentUiDefaultsVersion = 1;

    public const double LibraryOffsetMm = 0.02;
    public const double LibraryUniformScale = 1.0;
    public const double LibraryScaleX = 1.0;
    public const double LibraryScaleY = 1.0;
    public const double LibraryScaleZ = 1.0;

    public const double CementGapMm = 0.02;
    public const double ExtraCementGapMm = 0.08;
    public const double SmoothDistanceMm = 0.2;
    public const double MinimumThicknessMm = 0.65;
    public const double OffsetFromMarginMm = 1.0;

    public const double NumericStepMm = 0.01;
    public const int NumericDecimals = 3;

    public static void ApplyToNewManifest(StatsDesignManifest manifest)
    {
        manifest.CementGapMm = CementGapMm;
        manifest.ExtraCementGapMm = ExtraCementGapMm;
        manifest.SmoothDistanceMm = SmoothDistanceMm;
        manifest.MinimumThicknessMm = MinimumThicknessMm;
        manifest.OffsetFromMarginMm = OffsetFromMarginMm;
        manifest.LibraryScaleX = LibraryScaleX;
        manifest.LibraryScaleY = LibraryScaleY;
        manifest.LibraryScaleZ = LibraryScaleZ;
        manifest.LibraryUniformScale = LibraryUniformScale;
    }

    /// <summary>
    /// Applies <see cref="StatsDesignDefaults"/> to cases opened before defaults existed or after you bump <see cref="CurrentUiDefaultsVersion"/>.
    /// </summary>
    public static bool ApplyToExistingManifestIfNeeded(StatsDesignManifest manifest)
    {
        if (manifest.UiDefaultsVersion >= CurrentUiDefaultsVersion)
        {
            return false;
        }

        ApplyToNewManifest(manifest);
        ApplyLibraryPlacementDefaults(manifest);
        manifest.UiDefaultsVersion = CurrentUiDefaultsVersion;
        return true;
    }

    /// <summary>Shown in the Shape step before a library tooth is placed.</summary>
    public static void ApplyLibraryPlacementDefaults(StatsDesignManifest manifest)
    {
        if (!HasLibraryPlacementBeenConfigured(manifest))
        {
            manifest.LibraryOffsetXmm = LibraryOffsetMm;
            manifest.LibraryOffsetYmm = LibraryOffsetMm;
            manifest.LibraryOffsetZmm = LibraryOffsetMm;
            manifest.LibraryScaleX = LibraryScaleX;
            manifest.LibraryScaleY = LibraryScaleY;
            manifest.LibraryScaleZ = LibraryScaleZ;
            manifest.LibraryUniformScale = LibraryUniformScale;
        }
    }

    public static bool HasLibraryPlacementBeenConfigured(StatsDesignManifest manifest) =>
        !string.IsNullOrWhiteSpace(manifest.LibraryToothFileName);
}