namespace DCMViewer.Services;

public sealed class StatsDesignManifest
{
    public int Version { get; set; } = 1;

    /// <summary>Bumped when design UI defaults are applied from StatsDesignDefaults.</summary>
    public int UiDefaultsVersion { get; set; }

    public string OrderId { get; set; } = string.Empty;

    public string DesignName { get; set; } = "design";

    public double CementGapMm { get; set; } = 0.03;

    public double ExtraCementGapMm { get; set; } = 0.05;

    public double SmoothDistanceMm { get; set; } = 0.20;

    public double MinimumThicknessMm { get; set; } = 0.60;

    public double OffsetFromMarginMm { get; set; } = 1.00;

    /// <summary>Clearance kept between crown occlusal surface and opposing-arch scan (mm).</summary>
    public double OcclusalClearanceMm { get; set; } = 0.02;

    /// <summary>Height of the emergence-profile transition above the margin (mm).</summary>
    public double EmergenceTransitionMm { get; set; } = 1.50;

    public string? ExportedStlRelativePath { get; set; }

    public DateTime? LastExportedUtc { get; set; }

    public bool IsMarginClosed { get; set; }

    public List<StatsDesignMarginPoint> MarginPoints { get; set; } = [];

    /// <summary>Scan file the margin was drawn on (full path when saved).</summary>
    public string? MarginHostFilePath { get; set; }

    /// <summary>Unit insertion axis (crown draw direction), computed when the margin is closed.</summary>
    public double InsertionAxisX { get; set; }
    public double InsertionAxisY { get; set; }
    public double InsertionAxisZ { get; set; } = 1.0;

    /// <summary>Full paths of design meshes the user chose to load (CAD / StatsCAD), not scans.</summary>
    public List<string> LoadedDesignFilePaths { get; set; } = [];

    /// <summary>Relative path under StatsDesign for the open inner shell STL.</summary>
    public string? InnerShellRelativePath { get; set; }

    /// <summary>Relative path under StatsDesign for the closed crown STL.</summary>
    public string? CrownRelativePath { get; set; }

    public string? LibraryToothFileName { get; set; }

    public double LibraryOffsetXmm { get; set; }
    public double LibraryOffsetYmm { get; set; }
    public double LibraryOffsetZmm { get; set; }
    public double LibraryScaleX { get; set; } = 1.0;
    public double LibraryScaleY { get; set; } = 1.0;
    public double LibraryScaleZ { get; set; } = 1.0;
    public double LibraryUniformScale { get; set; } = 1.0;
}
