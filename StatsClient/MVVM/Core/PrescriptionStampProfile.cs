namespace StatsClient.MVVM.Core;

/// <summary>
/// Normalized stamp regions (0–1) on the prescription page.
/// Calibrated from reference PNGs with colored rectangles:
/// Red = pan number, Blue = RUSH, Green = injected info (Sent to / Missing / labels).
/// </summary>
public sealed record PrescriptionStampProfile(
    string PageHeaderIsHigh,
    string CalibrationImageName,
    int ReferenceWidth,
    int ReferenceHeight,
    FieldRegion PanNumber,
    FieldRegion Rush,
    FieldRegion InfoText)
{
    public System.Drawing.PointF PanDrawOrigin(float pageWidth, float pageHeight) =>
        RegionTopLeft(PanNumber, pageWidth, pageHeight);

    public System.Drawing.PointF RushDrawOrigin(float pageWidth, float pageHeight) =>
        RegionTopLeft(Rush, pageWidth, pageHeight);

    public System.Drawing.PointF InfoDrawOrigin(float pageWidth, float pageHeight) =>
        RegionTopLeft(InfoText, pageWidth, pageHeight);

    public float PanFontSize(float pageHeight) => FontSizeForRegion(PanNumber, pageHeight, 40f, 22f, 52f);

    public float RushFontSize(float pageHeight) => FontSizeForRegion(Rush, pageHeight, 40f, 20f, 48f);

    public float InfoFontSize(float pageHeight, int textLength) =>
        textLength > 20
            ? Math.Min(FontSizeForRegion(InfoText, pageHeight, 20f, 14f, 28f), 24f)
            : FontSizeForRegion(InfoText, pageHeight, 30f, 16f, 36f);

    public bool HasValidPan => IsValidRegion(PanNumber);
    public bool HasValidRush => IsValidRegion(Rush);
    public bool HasValidInfo => IsValidRegion(InfoText);

    private static bool IsValidRegion(FieldRegion region) =>
        region.W is >= 0.04 and <= 0.80
        && region.H is >= 0.015 and <= 0.15;

    private static System.Drawing.PointF RegionTopLeft(FieldRegion region, float pageWidth, float pageHeight) =>
        new(
            (float)(region.X * pageWidth),
            (float)(region.Y * pageHeight));

    private static float FontSizeForRegion(FieldRegion region, float pageHeight, float preferred, float min, float max)
    {
        float boxHeight = (float)(region.H * pageHeight);
        if (boxHeight <= 0)
        {
            return preferred;
        }

        return Math.Clamp(boxHeight * 0.72f, min, max);
    }
}
