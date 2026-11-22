using StatsClient.MVVM.Model;
using Syncfusion.Pdf.Parsing;
using System.IO;
using System.Text.RegularExpressions;
using Bitmap = System.Drawing.Bitmap;

namespace StatsClient.MVVM.Core;

/// <summary>
/// Extracts patient, customer, and teeth fields from incoming prescription PDFs.
/// Scanner layouts (PageHeaderIsHigh):
///   0/1 iTero, 3 IS3D/Intelliscan, 4 TRIOS, 5 Medit, 6 DS Core, 7 ASConnect, 8 Dexis.
/// Patterns mirror <see cref="PrescriptionDuplicateDetector"/> and extend with customer/teeth labels.
/// </summary>
public static class PrescriptionPdfFieldExtractor
{
    public static PrescriptionPdfExtractedFields Extract(string pdfPath, string pageHeaderIsHigh) =>
        Extract(pdfPath, pageHeaderIsHigh, renderBitmap: null, detection: null);

    public static PrescriptionPdfExtractedFields Extract(
        string pdfPath,
        string pageHeaderIsHigh,
        Bitmap? renderBitmap,
        PrescriptionScannerDetection? detection)
    {
        string stampType = ResolveStampType(pageHeaderIsHigh, detection);
        string text = ExtractPdfText(pdfPath);
        if (stampType == "6")
        {
            text = PrescriptionDsCoreParser.SliceFirstOrderBlock(text);
        }

        bool hasText = !string.IsNullOrWhiteSpace(text);

        string? orderId = ExtractOrderId(text, stampType);
        string? patientRaw = ExtractPatientRaw(text, stampType);
        var patientMethod = PrescriptionFieldExtractionMethod.TextLayer;
        (string? first, string? last) = SplitPatientName(patientRaw);
        var patientConfidence = RatePatientConfidence(hasText, patientRaw, first, last);

        string? customerRaw = ExtractCustomerRaw(text, stampType);
        string? customerName = CleanCustomerDisplay(customerRaw);
        if (!PrescriptionDsCoreParser.IsPlausiblePersonName(customerName))
        {
            customerName = null;
        }
        var customerMethod = PrescriptionFieldExtractionMethod.TextLayer;
        var customerConfidence = RateFieldConfidence(hasText, customerName);

        string? teeth = ExtractTeethNumbers(text, stampType);
        var teethMethod = PrescriptionFieldExtractionMethod.TextLayer;
        var teethConfidence = RateTeethConfidence(hasText, teeth);

        string? shade = null;
        string? comments = null;
        PrescriptionScannerDetection effectiveDetection = detection
            ?? CreateFallbackDetection(stampType, renderBitmap?.Width ?? 0, renderBitmap?.Height ?? 0);
        if (effectiveDetection.Type != PrescriptionScannerType.Unknown
            && effectiveDetection.Type != PrescriptionScannerType.Unclassified)
        {
            PrescriptionSummary summary = PrescriptionSummaryExtractor.Extract(pdfPath, effectiveDetection);
            PreferSummaryFields(
                summary,
                ref orderId,
                ref customerName,
                ref first,
                ref last,
                ref teeth,
                ref shade,
                ref comments);

            patientConfidence = RatePatientConfidence(hasText, patientRaw, first, last);
            customerConfidence = RateFieldConfidence(hasText, customerName);
            teethConfidence = RateTeethConfidence(hasText, teeth);
        }

        PrescriptionLayoutProfile? layout = PrescriptionLayoutProfile.ForPageHeaderIsHigh(stampType);
        if (renderBitmap is not null && layout is not null)
        {
            if (ShouldUseRegionOcr(patientConfidence, hasText)
                && string.IsNullOrWhiteSpace(first)
                && string.IsNullOrWhiteSpace(last))
            {
                string? ocrPatient = PrescriptionRegionOcr.ReadRegion(renderBitmap, layout.PatientName);
                if (!string.IsNullOrWhiteSpace(ocrPatient))
                {
                    patientRaw = CleanOcrLine(ocrPatient);
                    (first, last) = SplitPatientName(patientRaw);
                    patientConfidence = RatePatientConfidence(true, patientRaw, first, last);
                    patientMethod = PrescriptionFieldExtractionMethod.RegionOcr;
                }
            }

            if (ShouldUseRegionOcr(customerConfidence, hasText) && string.IsNullOrWhiteSpace(customerName))
            {
                string? ocrCustomer = PrescriptionRegionOcr.ReadRegion(renderBitmap, layout.CustomerName);
                if (!string.IsNullOrWhiteSpace(ocrCustomer))
                {
                    customerRaw = CleanOcrLine(ocrCustomer);
                    customerName = CleanCustomerDisplay(customerRaw);
                    if (!PrescriptionDsCoreParser.IsPlausiblePersonName(customerName))
                    {
                        customerName = null;
                        customerRaw = null;
                    }
                    else
                    {
                        customerConfidence = RateFieldConfidence(true, customerName);
                        customerMethod = PrescriptionFieldExtractionMethod.RegionOcr;
                    }
                }
            }

            if (ShouldUseRegionOcr(teethConfidence, hasText) && string.IsNullOrWhiteSpace(teeth))
            {
                string? ocrTeethRegion = PrescriptionRegionOcr.ReadRegion(renderBitmap, layout.Teeth);
                if (!string.IsNullOrWhiteSpace(ocrTeethRegion))
                {
                    string? ocrTeeth = ExtractTeethNumbers(ocrTeethRegion, stampType);
                    if (!string.IsNullOrWhiteSpace(ocrTeeth))
                    {
                        teeth = ocrTeeth;
                        teethConfidence = RateTeethConfidence(true, teeth);
                        teethMethod = PrescriptionFieldExtractionMethod.RegionOcr;
                    }
                }
            }
        }

        return new PrescriptionPdfExtractedFields
        {
            PageHeaderIsHigh = detection?.PageHeaderIsHigh ?? pageHeaderIsHigh ?? "",
            ScannerDisplayName = detection?.DisplayName ?? DescribeScannerTypeFallback(stampType),
            ScannerDetectionConfidence = detection?.Confidence ?? 0,
            ScannerDetectionMethod = detection is null
                ? ""
                : PrescriptionScannerClassifier.DescribeMethod(detection.Method),
            RenderBitmapWidth = detection?.BitmapWidth ?? renderBitmap?.Width ?? 0,
            RenderBitmapHeight = detection?.BitmapHeight ?? renderBitmap?.Height ?? 0,
            ScannerProbeDebug = detection?.FormatProbeDebug(),
            SourcePdfPath = pdfPath,
            HasTextLayer = hasText,
            ExtractedUtc = DateTime.UtcNow,
            OrderId = orderId,
            PatientRaw = patientRaw,
            PatientFirstName = first,
            PatientLastName = last,
            PatientConfidence = patientConfidence,
            PatientExtractionMethod = patientMethod,
            CustomerRaw = customerRaw,
            CustomerName = customerName,
            CustomerConfidence = customerConfidence,
            CustomerExtractionMethod = customerMethod,
            TeethNumbers = teeth,
            TeethConfidence = teethConfidence,
            TeethExtractionMethod = teethMethod,
            Shade = shade,
            Comments = comments,
        };
    }

    private static PrescriptionScannerDetection CreateFallbackDetection(string stampType, int width, int height)
    {
        PrescriptionScannerType type = stampType switch
        {
            "0" => PrescriptionScannerType.IteroLow,
            "1" => PrescriptionScannerType.IteroHigh,
            "3" => PrescriptionScannerType.Is3d,
            "4" => PrescriptionScannerType.Sirona,
            "5" => PrescriptionScannerType.Medit,
            "6" => PrescriptionScannerType.DsCore,
            "7" => PrescriptionScannerType.AsConnect,
            "8" => PrescriptionScannerType.Dexis,
            _ => PrescriptionScannerType.Unknown,
        };

        return new PrescriptionScannerDetection(
            type,
            stampType,
            PrescriptionSystemCatalog.DisplayName(stampType),
            0,
            PrescriptionScannerDetectionMethod.None,
            width,
            height,
            Array.Empty<ProbeDebugInfo>());
    }

    private static void PreferSummaryFields(
        PrescriptionSummary summary,
        ref string? orderId,
        ref string? customerName,
        ref string? first,
        ref string? last,
        ref string? teeth,
        ref string? shade,
        ref string? comments)
    {
        if (!string.IsNullOrWhiteSpace(summary.OrderId))
        {
            orderId = summary.OrderId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(summary.DoctorName)
            && PrescriptionDsCoreParser.IsPlausiblePersonName(summary.DoctorName))
        {
            customerName = summary.DoctorName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(summary.PatientName))
        {
            (first, last) = SplitPatientName(summary.PatientName);
        }

        if (!string.IsNullOrWhiteSpace(summary.TeethNumbers))
        {
            teeth = PreferRicherTeethList(teeth, summary.TeethNumbers);
        }

        if (!string.IsNullOrWhiteSpace(summary.Shade))
        {
            shade = summary.Shade.Trim();
        }

        if (!string.IsNullOrWhiteSpace(summary.CommentsNotes))
        {
            comments = summary.CommentsNotes.Trim();
        }
    }

    private static string PreferRicherTeethList(string? current, string summaryTeeth)
    {
        if (string.IsNullOrWhiteSpace(current))
        {
            return summaryTeeth.Trim();
        }

        return CountTeeth(summaryTeeth) >= CountTeeth(current)
            ? summaryTeeth.Trim()
            : current.Trim();
    }

    private static int CountTeeth(string? teeth) =>
        string.IsNullOrWhiteSpace(teeth)
            ? 0
            : teeth.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;

    private static string ResolveStampType(string pageHeaderIsHigh, PrescriptionScannerDetection? detection)
    {
        if (!string.IsNullOrWhiteSpace(pageHeaderIsHigh))
            return pageHeaderIsHigh;

        if (!string.IsNullOrWhiteSpace(detection?.PageHeaderIsHigh))
            return detection.PageHeaderIsHigh;

        return "1";
    }

    private static bool ShouldUseRegionOcr(PrescriptionFieldConfidence confidence, bool hasText) =>
        confidence <= PrescriptionFieldConfidence.Low || !hasText;

    private static string? CleanOcrLine(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        string line = raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
        return string.IsNullOrWhiteSpace(line) ? null : line;
    }

    private static string DescribeScannerTypeFallback(string pageHeaderIsHigh) =>
        pageHeaderIsHigh switch
        {
            "0" => "iTero (low header)",
            "1" => "iTero (high header)",
            "3" => "IS3D / Intelliscan",
            "4" => "TRIOS",
            "5" => "Medit",
            "6" => "DS Core",
            "7" => "ASConnect",
            "8" => "Dexis",
            _ => string.IsNullOrWhiteSpace(pageHeaderIsHigh) ? "Unknown" : pageHeaderIsHigh,
        };

    public static string ExtractPdfText(string pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            return string.Empty;

        try
        {
            using var document = new PdfLoadedDocument(pdfPath);
            if (document.Pages.Count == 0)
                return string.Empty;

            return document.Pages[0].ExtractText() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string? ExtractOrderId(string text, string pageHeaderIsHigh) =>
        pageHeaderIsHigh switch
        {
            "0" or "1" => MatchFirst(text, @"Order:\s*(\d+)"),
            "3" => MatchFirst(text, @"Case number:\s*(\S+)", ignoreCase: true),
            "4" => MatchFirst(text, @"Order Number:\s*(\d+)", ignoreCase: true),
            "5" => MatchFirst(text, @"Order ID:\s*(\d+)", ignoreCase: true),
            "6" => ExtractDsCoreOrderNumber(text),
            "7" => MatchFirst(text, @"Order ID:\s*(\d+)", ignoreCase: true),
            "8" => MatchFirst(text, @"CASE INFORMATION\s*\(\s*([A-Za-z]+-\d+)\s*\)", ignoreCase: true),
            _ => null,
        };

    private static string? ExtractPatientRaw(string text, string pageHeaderIsHigh) =>
        pageHeaderIsHigh switch
        {
            "0" or "1" => MatchFirst(text, @"Patient:\s*(.+)"),
            "3" => MatchFirst(text, @"Patient\s*\n\s*([^\n\r]+)", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                ?? MatchFirst(text, @"Patient name:\s*(.+)", ignoreCase: true),
            "4" => MatchFirst(text, @"Patient(?:\s*Name)?:\s*(.+)", ignoreCase: true),
            "5" => MatchFirst(text, @"\*?\s*Patient Name\s*:\s*(.+)", ignoreCase: true)
                ?? MatchFirst(text, @"Patient Name:\s*(.+)", ignoreCase: true),
            "6" => MatchFirst(text, @"Patient\s*\n\s*([^\n\r]+)", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                ?? MatchFirst(text, @"Patient(?:\s*Name)?:\s*(.+)", ignoreCase: true),
            "7" => MatchFirst(text, @"Patient Name:\s*(.+)", ignoreCase: true),
            "8" => ExtractDexisPatient(text)
                ?? MatchFirst(text, @"Patient(?:\s*Name)?:\s*(.+)", ignoreCase: true),
            _ => null,
        };

    private static string? ExtractCustomerRaw(string text, string pageHeaderIsHigh)
    {
        bool multiline = pageHeaderIsHigh is "3" or "6" or "8";
        foreach (string pattern in GetCustomerPatterns(pageHeaderIsHigh))
        {
            string? value = multiline
                ? MatchFirst(text, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase)
                : MatchFirst(text, pattern, ignoreCase: true);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static IEnumerable<string> GetCustomerPatterns(string pageHeaderIsHigh)
    {
        foreach (string pattern in GetCustomerPatternsCore(pageHeaderIsHigh))
        {
            yield return pattern;
        }
    }

    private static IEnumerable<string> GetCustomerPatternsCore(string pageHeaderIsHigh) =>
        pageHeaderIsHigh switch
        {
            "0" or "1" =>
            [
                @"Doctor:\s*(.+)",
                @"Dentist:\s*(.+)",
                @"Practice:\s*(.+)",
                @"Dental Office:\s*(.+)",
            ],
            "3" =>
            [
                @"Doctor\s*\n\s*([^\n\r]+)",
                @"Doctor:\s*(.+)",
                @"Clinic:\s*(.+)",
                @"Customer:\s*(.+)",
                @"Dental office:\s*(.+)",
            ],
            "4" =>
            [
                @"Practice(?:\s*Name)?:\s*(.+)",
                @"Dental practice:\s*(.+)",
                @"Clinic:\s*(.+)",
            ],
            "5" =>
            [
                @"Dentist(?: Name)?:\s*(.+)",
                @"Doctor:\s*(.+)",
                @"Clinic:\s*(.+)",
                @"Dental Clinic:\s*(.+)",
                @"Practice:\s*(.+)",
            ],
            "6" =>
            [
                @"Dr\s+([^\n\r]+)",
                @"Practice:\s*(.+)",
                @"Dental practice:\s*(.+)",
                @"Clinic:\s*(.+)",
            ],
            "7" =>
            [
                @"Dentist Name:\s*(.+)",
                @"Doctor:\s*(.+)",
            ],
            "8" =>
            [
                @"^Dr\.?\s*([^\n\r,]+)",
                @"^(.+?\s+DDS)\s*$",
            ],
            _ => [],
        };

    private static string? ExtractTeethNumbers(string text, string pageHeaderIsHigh)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (pageHeaderIsHigh == "6")
        {
            return PrescriptionDsCoreParser.ExtractTeethNumbers(text);
        }

        if (pageHeaderIsHigh is "0" or "1")
        {
            string? iteroTeeth = PrescriptionSummaryExtractor.ExtractIteroTreatmentTeethPublic(text);
            if (!string.IsNullOrWhiteSpace(iteroTeeth))
                return iteroTeeth;
        }

        var teeth = new SortedSet<int>();

        foreach (Match match in Regex.Matches(text, @"#\s*(\d{1,2})\b"))
        {
            if (int.TryParse(match.Groups[1].Value, out int n))
                TryAddTooth(teeth, n);
        }

        foreach (string pattern in GetToothLabelPatterns(pageHeaderIsHigh))
        {
            foreach (Match match in Regex.Matches(text, pattern, RegexOptions.IgnoreCase))
            {
                if (match.Groups.Count < 2)
                    continue;

                string segment = match.Groups[1].Value.Trim();
                AddTeethFromSegment(teeth, segment);
            }
        }

        foreach (Match match in Regex.Matches(text, @"\bTooth(?:\s*(?:Number|No\.?|#))?\s*[:#]?\s*(\d{1,2})\b", RegexOptions.IgnoreCase))
        {
            if (int.TryParse(match.Groups[1].Value, out int n))
                TryAddTooth(teeth, n);
        }

        if (teeth.Count == 0)
            return null;

        return string.Join(",", teeth);
    }

    private static IEnumerable<string> GetToothLabelPatterns(string pageHeaderIsHigh) =>
        pageHeaderIsHigh switch
        {
            "0" or "1" =>
            [
                @"Tooth\s*#?\s*(\d{1,2}(?:\s*[-,]\s*\d{1,2})*)",
                @"Teeth:\s*(.+)",
            ],
            "3" =>
            [
                @"Tooth number:\s*(.+)",
                @"Teeth:\s*(.+)",
            ],
            "4" or "5" or "6" or "7" or "8" =>
            [
                @"Tooth(?:\s*number)?:\s*(.+)",
                @"Teeth:\s*(.+)",
            ],
            _ => [@"Teeth:\s*(.+)"],
        };

    private static void AddTeethFromSegment(SortedSet<int> teeth, string segment)
    {
        foreach (Match rangeMatch in Regex.Matches(segment, @"(\d{1,2})\s*-\s*(\d{1,2})"))
        {
            if (!int.TryParse(rangeMatch.Groups[1].Value, out int start)
                || !int.TryParse(rangeMatch.Groups[2].Value, out int end))
                continue;

            if (start > end)
                (start, end) = (end, start);

            for (int i = start; i <= end && i <= start + 15; i++)
                TryAddTooth(teeth, i);
        }

        foreach (Match num in Regex.Matches(segment, @"\b(\d{1,2})\b"))
        {
            if (int.TryParse(num.Groups[1].Value, out int n))
                TryAddTooth(teeth, n);
        }
    }

    private static void TryAddTooth(SortedSet<int> teeth, int n)
    {
        if (n is >= 1 and <= 32)
            teeth.Add(n);
        else if (n is >= 11 and <= 48)
            teeth.Add(n);
    }

    private static (string? First, string? Last) SplitPatientName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return (null, null);

        string cleaned = Regex.Replace(raw.Trim(), @"\([^)]*\)", "").Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
            return (null, null);

        if (cleaned.Contains(','))
        {
            string[] commaParts = cleaned.Split(',', 2, StringSplitOptions.TrimEntries);
            if (commaParts.Length == 2)
                return (commaParts[1], commaParts[0]);
        }

        string[] parts = cleaned.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return (null, null);
        if (parts.Length == 1)
            return (parts[0], null);

        string last = parts[^1];
        string first = string.Join(' ', parts.Take(parts.Length - 1));
        return (first, last);
    }

    private static string? CleanCustomerDisplay(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        string value = raw.Trim();
        int lineBreak = value.IndexOfAny(['\r', '\n']);
        if (lineBreak >= 0)
            value = value[..lineBreak].Trim();

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static PrescriptionFieldConfidence RatePatientConfidence(
        bool hasText,
        string? raw,
        string? first,
        string? last)
    {
        if (!hasText || string.IsNullOrWhiteSpace(raw))
            return PrescriptionFieldConfidence.None;

        if (!string.IsNullOrWhiteSpace(first) && !string.IsNullOrWhiteSpace(last))
            return PrescriptionFieldConfidence.High;

        if (!string.IsNullOrWhiteSpace(first) || !string.IsNullOrWhiteSpace(last))
            return PrescriptionFieldConfidence.Medium;

        return PrescriptionFieldConfidence.Low;
    }

    private static PrescriptionFieldConfidence RateFieldConfidence(bool hasText, string? value)
    {
        if (!hasText || string.IsNullOrWhiteSpace(value))
            return PrescriptionFieldConfidence.None;

        return value.Length >= 2
            ? PrescriptionFieldConfidence.High
            : PrescriptionFieldConfidence.Low;
    }

    private static PrescriptionFieldConfidence RateTeethConfidence(bool hasText, string? teeth)
    {
        if (!hasText || string.IsNullOrWhiteSpace(teeth))
            return PrescriptionFieldConfidence.None;

        int count = teeth.Split(',', StringSplitOptions.RemoveEmptyEntries).Length;
        return count switch
        {
            0 => PrescriptionFieldConfidence.None,
            1 => PrescriptionFieldConfidence.Medium,
            _ => PrescriptionFieldConfidence.High,
        };
    }

    private static string? ExtractDexisPatient(string text)
    {
        Match match = Regex.Match(text, @"^([A-Za-z][^\n\r\d]+?)\s*\r?\n\s*\d{2}/\d{2}/\d{4}\s+PWID:",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        string value = match.Groups[1].Value.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? ExtractDsCoreOrderNumber(string text)
    {
        if (!text.Contains("Order Number:", StringComparison.OrdinalIgnoreCase))
            return null;

        int start = text.IndexOf("Order Number:", StringComparison.OrdinalIgnoreCase);
        string slice = text.Substring(start, Math.Min(48, text.Length - start))
            .Replace("Order Number:", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Order Date:", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Date Due:", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        int spaceIndex = slice.IndexOf(' ');
        if (spaceIndex > 0)
            slice = slice[..spaceIndex];

        return string.IsNullOrWhiteSpace(slice) ? null : slice;
    }

    private static string? MatchFirst(string text, string pattern, bool ignoreCase = false) =>
        MatchFirst(text, pattern, ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);

    private static string? MatchFirst(string text, string pattern, RegexOptions options)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        Match match = Regex.Match(text, pattern, options);
        if (!match.Success || match.Groups.Count < 2)
            return null;

        string value = match.Groups[1].Value.Trim();
        int lineBreak = value.IndexOfAny(['\r', '\n']);
        if (lineBreak >= 0)
            value = value[..lineBreak].Trim();

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
