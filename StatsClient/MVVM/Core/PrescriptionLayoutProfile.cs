namespace StatsClient.MVVM.Core;



/// <summary>

/// Normalized rectangle (0–1) on the rendered prescription page for region OCR fallback.

/// Coordinates are approximate defaults; refine with sample PDF calibration.

/// </summary>

public sealed record FieldRegion(string Name, double X, double Y, double W, double H);



/// <summary>

/// Per-scanner crop regions for region OCR fallback. Refine coordinates using

/// <see cref="PrescriptionScannerClassifier.SampleProbesForCalibration"/> and Extracted Info debug output.

/// To add code "9": add a profile here, pixel rule in PrescriptionScannerClassifier, and EditPDF stamp coords.

/// </summary>

public sealed record PrescriptionLayoutProfile(

    PrescriptionScannerType Scanner,

    FieldRegion PatientName,

    FieldRegion CustomerName,

    FieldRegion Teeth)

{

    public static PrescriptionLayoutProfile? ForPageHeaderIsHigh(string pageHeaderIsHigh) =>

        pageHeaderIsHigh switch

        {

            "0" => IteroLow,

            "1" => IteroHigh,

            "3" => Is3d,

            "4" => Sirona,

            "5" => Medit,

            "6" => DsCore,

            "7" => AsConnect,

            "8" => Dexis,

            PrescriptionSystemCatalog.UnknownCode => Unclassified,

            _ => Unclassified,

        };



    public static PrescriptionLayoutProfile? ForScannerType(PrescriptionScannerType type) =>

        type switch

        {

            PrescriptionScannerType.IteroLow => IteroLow,

            PrescriptionScannerType.IteroHigh => IteroHigh,

            PrescriptionScannerType.Is3d => Is3d,

            PrescriptionScannerType.Sirona => Sirona,

            PrescriptionScannerType.Medit => Medit,

            PrescriptionScannerType.DsCore => DsCore,

            PrescriptionScannerType.AsConnect => AsConnect,

            PrescriptionScannerType.Dexis => Dexis,

            PrescriptionScannerType.Unclassified => Unclassified,

            _ => null,

        };



    private static readonly PrescriptionLayoutProfile IteroLow = new(

        PrescriptionScannerType.IteroLow,

        new FieldRegion("Patient", 0.52, 0.04, 0.42, 0.06),

        new FieldRegion("Customer", 0.04, 0.04, 0.44, 0.10),

        new FieldRegion("Teeth", 0.04, 0.22, 0.92, 0.35));



    private static readonly PrescriptionLayoutProfile IteroHigh = new(

        PrescriptionScannerType.IteroHigh,

        new FieldRegion("Patient", 0.52, 0.06, 0.42, 0.06),

        new FieldRegion("Customer", 0.04, 0.06, 0.44, 0.10),

        new FieldRegion("Teeth", 0.04, 0.24, 0.92, 0.35));



    private static readonly PrescriptionLayoutProfile Is3d = new(

        PrescriptionScannerType.Is3d,

        new FieldRegion("Patient", 0.55, 0.05, 0.40, 0.06),

        new FieldRegion("Customer", 0.04, 0.05, 0.48, 0.08),

        new FieldRegion("Teeth", 0.04, 0.18, 0.92, 0.40));



    private static readonly PrescriptionLayoutProfile Sirona = new(

        PrescriptionScannerType.Sirona,

        new FieldRegion("Patient", 0.04, 0.12, 0.45, 0.06),

        new FieldRegion("Customer", 0.04, 0.05, 0.55, 0.06),

        new FieldRegion("Teeth", 0.04, 0.22, 0.92, 0.38));



    private static readonly PrescriptionLayoutProfile Medit = new(

        PrescriptionScannerType.Medit,

        new FieldRegion("Patient", 0.55, 0.06, 0.40, 0.06),

        new FieldRegion("Customer", 0.04, 0.06, 0.48, 0.08),

        new FieldRegion("Teeth", 0.04, 0.20, 0.92, 0.38));



    private static readonly PrescriptionLayoutProfile DsCore = new(

        PrescriptionScannerType.DsCore,

        new FieldRegion("Patient", 0.04, 0.14, 0.45, 0.06),

        new FieldRegion("Customer", 0.04, 0.06, 0.55, 0.07),

        new FieldRegion("Teeth", 0.04, 0.24, 0.92, 0.36));



    private static readonly PrescriptionLayoutProfile AsConnect = new(

        PrescriptionScannerType.AsConnect,

        new FieldRegion("Patient", 0.50, 0.10, 0.45, 0.06),

        new FieldRegion("Customer", 0.04, 0.06, 0.45, 0.08),

        new FieldRegion("Teeth", 0.04, 0.20, 0.92, 0.40));



    private static readonly PrescriptionLayoutProfile Dexis = new(

        PrescriptionScannerType.Dexis,

        new FieldRegion("Patient", 0.04, 0.14, 0.45, 0.08),

        new FieldRegion("Customer", 0.04, 0.04, 0.45, 0.08),

        new FieldRegion("Teeth", 0.04, 0.20, 0.92, 0.40));



    private static readonly PrescriptionLayoutProfile Unclassified = new(

        PrescriptionScannerType.Unclassified,

        new FieldRegion("Patient", 0.52, 0.06, 0.42, 0.06),

        new FieldRegion("Customer", 0.04, 0.06, 0.44, 0.10),

        new FieldRegion("Teeth", 0.04, 0.24, 0.92, 0.35));

}


