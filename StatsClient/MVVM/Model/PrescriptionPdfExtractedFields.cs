namespace StatsClient.MVVM.Model;



public enum PrescriptionFieldConfidence

{

    None = 0,

    Low = 1,

    Medium = 2,

    High = 3,

}



public enum PrescriptionFieldExtractionMethod

{

    None = 0,

    TextLayer = 1,

    RegionOcr = 2,

}



/// <summary>

/// Fields parsed from an incoming prescription PDF at Prescription Maker ingest time.

/// Populated before pan stamping; not persisted until explicitly wired up.

/// </summary>

public sealed class PrescriptionPdfExtractedFields

{

    public string PageHeaderIsHigh { get; init; } = "";



    public string ScannerDisplayName { get; init; } = "";



    public double ScannerDetectionConfidence { get; init; }



    public string ScannerDetectionMethod { get; init; } = "";



    public int RenderBitmapWidth { get; init; }



    public int RenderBitmapHeight { get; init; }



    public string? ScannerProbeDebug { get; init; }



    public string? SourcePdfPath { get; init; }



    public bool HasTextLayer { get; init; }



    public DateTime ExtractedUtc { get; init; }



    public string? OrderId { get; init; }



    public string? PatientRaw { get; init; }



    public string? PatientFirstName { get; init; }



    public string? PatientLastName { get; init; }



    public PrescriptionFieldConfidence PatientConfidence { get; init; }



    public PrescriptionFieldExtractionMethod PatientExtractionMethod { get; init; }



    public string? CustomerRaw { get; init; }



    public string? CustomerName { get; init; }



    public PrescriptionFieldConfidence CustomerConfidence { get; init; }



    public PrescriptionFieldExtractionMethod CustomerExtractionMethod { get; init; }



    /// <summary>Comma-separated universal or FDI tooth numbers when found in PDF text.</summary>

    public string? TeethNumbers { get; init; }



    public PrescriptionFieldConfidence TeethConfidence { get; init; }



    public PrescriptionFieldExtractionMethod TeethExtractionMethod { get; init; }



    public string? Shade { get; init; }



    public string? Comments { get; init; }

}

