using System.Drawing;
using System.IO;
using Bitmap = System.Drawing.Bitmap;

namespace StatsClient.MVVM.Core;

public enum PrescriptionScannerType
{
    Unknown = 0,
    IteroLow = 1,
    IteroHigh = 2,
    Is3d = 3,
    Sirona = 4,
    Medit = 5,
    DsCore = 6,
    AsConnect = 7,
    Dexis = 8,
    Unclassified = 9,
}

public enum PrescriptionScannerDetectionMethod
{
    None = 0,
    Filename = 1,
    TextSignature = 2,
    PixelProbe = 3,
    Heuristic = 4,
}

public sealed record ProbeDebugInfo(
    string Name,
    int PixelX,
    int PixelY,
    int ActualR,
    int ActualG,
    int ActualB,
    int? ExpectedR,
    int? ExpectedG,
    int? ExpectedB,
    int ColorDistance,
    bool Matched);

public sealed record PrescriptionScannerDetection(
    PrescriptionScannerType Type,
    string PageHeaderIsHigh,
    string DisplayName,
    double Confidence,
    PrescriptionScannerDetectionMethod Method,
    int BitmapWidth,
    int BitmapHeight,
    IReadOnlyList<ProbeDebugInfo> Probes)
{
    public string FormatProbeDebug() =>
        Probes.Count == 0
            ? ""
            : string.Join(Environment.NewLine, Probes.Select(p =>
                $"{p.Name} @ ({p.PixelX},{p.PixelY}) RGB({p.ActualR},{p.ActualG},{p.ActualB})"
                + (p.ExpectedR is null ? "" : $" expected({p.ExpectedR},{p.ExpectedG},{p.ExpectedB}) dist={p.ColorDistance} match={p.Matched}")));
}

/// <summary>
/// Identifies incoming prescription PDF scanner/layout type using filename, text, and pixel probes.
/// Probe normalized coordinates are derived from legacy fixed-pixel calibration (765×1100 reference render).
/// </summary>
public static class PrescriptionScannerClassifier
{
    private const int ColorTolerance = 25;
    private const int WhiteChannelMin = 250;

    private sealed record ColorProbe(
        string Name,
        double NormalizedX,
        double NormalizedY,
        byte R,
        byte G,
        byte B);

    private sealed record PixelRule(
        PrescriptionScannerType Type,
        string PageHeaderIsHigh,
        string DisplayName,
        ColorProbe[] Probes,
        bool MatchAll);

    private static readonly PixelRule[] PixelRules =
    [
        new(PrescriptionScannerType.Medit, "5", "Medit",
        [
            new("Medit", 687d / 765, 32d / 1100, 107, 154, 240),
        ], MatchAll: true),
        new(PrescriptionScannerType.Sirona, "4", "TRIOS",
        [
            new("Trios", 750d / 765, 40d / 1100, 244, 16, 71),
        ], MatchAll: true),
        new(PrescriptionScannerType.Is3d, "3", "IS3D / Intelliscan",
        [
            new("IS3D", 2d / 765, 30d / 1100, 85, 135, 255),
        ], MatchAll: true),
        new(PrescriptionScannerType.DsCore, "6", "DS Core",
        [
            new("DSCore", 12d / 765, 12d / 1100, 85, 87, 89),
        ], MatchAll: true),
        new(PrescriptionScannerType.AsConnect, "7", "ASConnect",
        [
            new("ASConnect", 400d / 765, 50d / 1100, 0, 67, 153),
        ], MatchAll: true),
    ];

    private static readonly ColorProbe IteroLowProbe = new("iTeroLow", 10d / 765, 56d / 1100, 255, 255, 255);

    private static readonly FieldRegion[] MeditBrandingRegions =
    [
        new("MeditFooter", 0.0, 0.88, 1.0, 0.12),
        new("MeditLinkBand", 0.0, 0.68, 1.0, 0.28),
    ];

    public static PrescriptionScannerDetection Detect(Bitmap bitmap, string? pdfPath)
    {
        if (bitmap is null)
            return UnknownDetection(0, 0, []);

        string fileName = string.IsNullOrWhiteSpace(pdfPath) ? "" : Path.GetFileName(pdfPath);
        string text = PrescriptionPdfFieldExtractor.ExtractPdfText(pdfPath ?? "");
        var probes = SampleAllProbes(bitmap);

        if (fileName.Contains("Workticket_", StringComparison.OrdinalIgnoreCase))
        {
            return Build(PrescriptionScannerType.Sirona, "4", "TRIOS", 0.98, PrescriptionScannerDetectionMethod.Filename,
                bitmap.Width, bitmap.Height, probes);
        }

        PrescriptionScannerDetection? textMatch = TryTextSignature(text, bitmap, bitmap.Width, bitmap.Height, probes);
        if (textMatch is not null)
            return textMatch;

        if (TryDetectMeditLinkBranding(bitmap))
        {
            return Build(PrescriptionScannerType.Medit, "5", "Medit Link", 0.82,
                PrescriptionScannerDetectionMethod.Heuristic, bitmap.Width, bitmap.Height, probes);
        }

        foreach (PixelRule rule in PixelRules)
        {
            if (TryMatchPixelRule(bitmap, rule, probes, out _))
            {
                return Build(rule.Type, rule.PageHeaderIsHigh, rule.DisplayName, 0.92, PrescriptionScannerDetectionMethod.PixelProbe,
                    bitmap.Width, bitmap.Height, probes);
            }
        }

        if (IsIteroLowWhite(probes, text))
        {
            return Build(PrescriptionScannerType.IteroLow, "0", "iTero (low header)", 0.88,
                PrescriptionScannerDetectionMethod.PixelProbe, bitmap.Width, bitmap.Height, probes);
        }

        if (ContainsIteroTextSignature(text))
        {
            return Build(PrescriptionScannerType.IteroHigh, "1", "iTero (high header)", 0.75,
                PrescriptionScannerDetectionMethod.TextSignature, bitmap.Width, bitmap.Height, probes);
        }

        if (ContainsIteroHeuristic(text))
        {
            return Build(PrescriptionScannerType.IteroHigh, "1", "iTero (high header)", 0.55,
                PrescriptionScannerDetectionMethod.Heuristic, bitmap.Width, bitmap.Height, probes);
        }

        return UnknownDetection(bitmap.Width, bitmap.Height, probes);
    }

    private static PrescriptionScannerDetection? TryTextSignature(
        string text,
        Bitmap? bitmap,
        int width,
        int height,
        IReadOnlyList<ProbeDebugInfo> probes)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return TryDetectMeditLinkBranding(bitmap)
                ? Build(PrescriptionScannerType.Medit, "5", "Medit Link", 0.82,
                    PrescriptionScannerDetectionMethod.Heuristic, width, height, probes)
                : null;
        }

        if (ContainsMeditLinkBranding(text))
        {
            return Build(PrescriptionScannerType.Medit, "5", "Medit Link", 0.92,
                PrescriptionScannerDetectionMethod.TextSignature, width, height, probes);
        }

        if (ContainsIteroTextSignature(text))
        {
            return Build(PrescriptionScannerType.IteroHigh, "1", "iTero (high header)", 0.95,
                PrescriptionScannerDetectionMethod.TextSignature, width, height, probes);
        }

        if (text.Contains("Connect Case Center", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Creation date:", StringComparison.OrdinalIgnoreCase))
        {
            return Build(PrescriptionScannerType.Sirona, "4", "TRIOS", 0.95, PrescriptionScannerDetectionMethod.TextSignature,
                width, height, probes);
        }

        if (text.Contains("Dentist name:", StringComparison.OrdinalIgnoreCase)
            && text.Contains("Patient name:", StringComparison.OrdinalIgnoreCase)
            && text.Contains("Case number:", StringComparison.OrdinalIgnoreCase))
        {
            return Build(PrescriptionScannerType.Sirona, "4", "TRIOS", 0.93,
                PrescriptionScannerDetectionMethod.TextSignature, width, height, probes);
        }

        if (text.Contains("Order Number:", StringComparison.OrdinalIgnoreCase)
            && text.Contains("Date Due:", StringComparison.OrdinalIgnoreCase))
        {
            return Build(PrescriptionScannerType.DsCore, "6", "DS Core", 0.93, PrescriptionScannerDetectionMethod.TextSignature,
                width, height, probes);
        }

        if (text.Contains("Case Sheet", StringComparison.OrdinalIgnoreCase)
            && text.Contains("CORE", StringComparison.OrdinalIgnoreCase))
        {
            return Build(PrescriptionScannerType.DsCore, "6", "DS Core", 0.95, PrescriptionScannerDetectionMethod.TextSignature,
                width, height, probes);
        }

        if (text.Contains("CASE INFORMATION (", StringComparison.OrdinalIgnoreCase))
        {
            return Build(PrescriptionScannerType.Dexis, "8", "Dexis", 0.95,
                PrescriptionScannerDetectionMethod.TextSignature, width, height, probes);
        }

        if (text.Contains("Treatment Report", StringComparison.OrdinalIgnoreCase))
        {
            return Build(PrescriptionScannerType.AsConnect, "7", "ASConnect", 0.95,
                PrescriptionScannerDetectionMethod.TextSignature, width, height, probes);
        }

        if (text.Contains("Shining 3D", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Case Information-Restore", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Dental Cloud", StringComparison.OrdinalIgnoreCase))
        {
            return Build(PrescriptionScannerType.Is3d, "3", "IS3D / Shining 3D", 0.90,
                PrescriptionScannerDetectionMethod.TextSignature, width, height, probes);
        }

        if (text.Contains("Case number:", StringComparison.OrdinalIgnoreCase)
            && text.Contains("Patient name:", StringComparison.OrdinalIgnoreCase))
        {
            return Build(PrescriptionScannerType.Is3d, "3", "IS3D / Intelliscan", 0.90,
                PrescriptionScannerDetectionMethod.TextSignature, width, height, probes);
        }

        if (text.Contains("Order ID:", StringComparison.OrdinalIgnoreCase)
            && text.Contains("Patient Name:", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("Treatment Report", StringComparison.OrdinalIgnoreCase))
        {
            return Build(PrescriptionScannerType.Medit, "5", "Medit", 0.88, PrescriptionScannerDetectionMethod.TextSignature,
                width, height, probes);
        }

        return null;
    }

    private static bool ContainsMeditLinkBranding(string? text) =>
        !string.IsNullOrWhiteSpace(text)
        && (text.Contains("meditlink", StringComparison.OrdinalIgnoreCase)
            || text.Contains("medit link", StringComparison.OrdinalIgnoreCase)
            || text.Contains("copyright medit corp", StringComparison.OrdinalIgnoreCase));

    private static bool TryDetectMeditLinkBranding(Bitmap? bitmap)
    {
        if (bitmap is null)
        {
            return false;
        }

        foreach (FieldRegion region in MeditBrandingRegions)
        {
            string? regionText = PrescriptionRegionOcr.ReadRegion(bitmap, region);
            if (ContainsMeditLinkBranding(regionText))
            {
                return true;
            }
        }

        string? fullPageText = PrescriptionRegionOcr.ReadFullPage(bitmap);
        return ContainsMeditLinkBranding(fullPageText);
    }

    private static bool ContainsIteroTextSignature(string text) =>
        !string.IsNullOrWhiteSpace(text)
        && text.Contains("Order:", StringComparison.OrdinalIgnoreCase)
        && text.Contains("Patient:", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsIteroHeuristic(string text) =>
        !string.IsNullOrWhiteSpace(text)
        && (text.Contains("iTero", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Align Technology", StringComparison.OrdinalIgnoreCase));

    private static bool TryMatchPixelRule(
        Bitmap bitmap,
        PixelRule rule,
        IReadOnlyList<ProbeDebugInfo> probes,
        out int bestDistance)
    {
        bestDistance = int.MaxValue;

        if (rule.MatchAll)
        {
            foreach (ColorProbe probe in rule.Probes)
            {
                if (!TryReadProbe(bitmap, probe, probes, out int dist) || dist > ColorTolerance)
                    return false;

                bestDistance = Math.Min(bestDistance, dist);
            }

            return true;
        }

        foreach (ColorProbe probe in rule.Probes)
        {
            if (TryReadProbe(bitmap, probe, probes, out int dist) && dist <= ColorTolerance)
            {
                bestDistance = dist;
                return true;
            }
        }

        return false;
    }

    private static bool IsIteroLowWhite(IReadOnlyList<ProbeDebugInfo> probes, string text)
    {
        ProbeDebugInfo? itero = probes.FirstOrDefault(p => p.Name == IteroLowProbe.Name);
        if (itero is null
            || itero.ActualR < WhiteChannelMin
            || itero.ActualG < WhiteChannelMin
            || itero.ActualB < WhiteChannelMin)
        {
            return false;
        }

        return ContainsIteroTextSignature(text) || ContainsIteroHeuristic(text);
    }

    private static bool TryReadProbe(
        Bitmap bitmap,
        ColorProbe probe,
        IReadOnlyList<ProbeDebugInfo> probes,
        out int distance)
    {
        distance = int.MaxValue;
        if (!TryReadProbePixel(bitmap, probe, out _, out _, out Color color))
            return false;

        distance = ColorDistance(color.R, color.G, color.B, probe.R, probe.G, probe.B);
        return true;
    }

    private static bool TryReadProbePixel(Bitmap bitmap, ColorProbe probe, out int x, out int y, out Color color)
    {
        x = ClampPixel((int)Math.Round(probe.NormalizedX * bitmap.Width), bitmap.Width);
        y = ClampPixel((int)Math.Round(probe.NormalizedY * bitmap.Height), bitmap.Height);
        color = bitmap.GetPixel(x, y);
        return true;
    }

    private static int ClampPixel(int value, int max) =>
        max <= 0 ? 0 : Math.Clamp(value, 0, max - 1);

    private static int ColorDistance(int r1, int g1, int b1, int r2, int g2, int b2) =>
        Math.Max(Math.Abs(r1 - r2), Math.Max(Math.Abs(g1 - g2), Math.Abs(b1 - b2)));

    private static List<ProbeDebugInfo> SampleAllProbes(Bitmap bitmap)
    {
        var list = new List<ProbeDebugInfo>();

        void Add(ColorProbe probe, byte? er = null, byte? eg = null, byte? eb = null, bool whiteMatch = false)
        {
            int x = ClampPixel((int)Math.Round(probe.NormalizedX * bitmap.Width), bitmap.Width);
            int y = ClampPixel((int)Math.Round(probe.NormalizedY * bitmap.Height), bitmap.Height);
            Color c = bitmap.GetPixel(x, y);
            int dist = er is null ? 0 : ColorDistance(c.R, c.G, c.B, er.Value, eg!.Value, eb!.Value);
            bool matched = whiteMatch
                ? c.R >= WhiteChannelMin && c.G >= WhiteChannelMin && c.B >= WhiteChannelMin
                : er is not null && dist <= ColorTolerance;
            list.Add(new ProbeDebugInfo(probe.Name, x, y, c.R, c.G, c.B, er, eg, eb, dist, matched));
        }

        Add(IteroLowProbe, whiteMatch: true);

        foreach (PixelRule rule in PixelRules)
        {
            foreach (ColorProbe probe in rule.Probes)
                Add(probe, probe.R, probe.G, probe.B);
        }

        return list;
    }

    private static PrescriptionScannerDetection UnknownDetection(
        int width,
        int height,
        IReadOnlyList<ProbeDebugInfo> probes) =>
        Build(
            PrescriptionScannerType.Unclassified,
            PrescriptionSystemCatalog.UnknownCode,
            PrescriptionSystemCatalog.DisplayName(PrescriptionSystemCatalog.UnknownCode),
            0.0,
            PrescriptionScannerDetectionMethod.None,
            width,
            height,
            probes);

    private static PrescriptionScannerDetection Build(
        PrescriptionScannerType type,
        string pageHeaderIsHigh,
        string displayName,
        double confidence,
        PrescriptionScannerDetectionMethod method,
        int width,
        int height,
        IReadOnlyList<ProbeDebugInfo> probes) =>
        new(type, pageHeaderIsHigh, displayName, confidence, method, width, height, probes);

    /// <summary>
    /// Add new scanner systems here: assign the next PageHeaderIsHigh code (e.g. "10"),
    /// before using code "9" (<see cref="PrescriptionSystemCatalog.UnknownCode"/>) for unclassified layouts.
    /// add a <see cref="PixelRule"/>, text signature in <see cref="TryTextSignature"/>,
    /// layout profile in <see cref="PrescriptionLayoutProfile"/>, and stamp coordinates in EditPDF.
    /// </summary>
    public static IReadOnlyList<ProbeDebugInfo> SampleProbesForCalibration(Bitmap bitmap) =>
        SampleAllProbes(bitmap);

    public static string DescribeMethod(PrescriptionScannerDetectionMethod method) =>
        method switch
        {
            PrescriptionScannerDetectionMethod.Filename => "Filename",
            PrescriptionScannerDetectionMethod.TextSignature => "Text signature",
            PrescriptionScannerDetectionMethod.PixelProbe => "Pixel probe",
            PrescriptionScannerDetectionMethod.Heuristic => "Heuristic",
            _ => "None",
        };
}

