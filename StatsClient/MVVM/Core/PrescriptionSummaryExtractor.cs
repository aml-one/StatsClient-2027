using StatsClient.MVVM.Model;
using System.IO;
using System.Text.RegularExpressions;

namespace StatsClient.MVVM.Core;

/// <summary>
/// Text-first extraction of simplified prescription fields per scanner system.
/// </summary>
public static class PrescriptionSummaryExtractor
{
    public static PrescriptionSummary Extract(string pdfPath, PrescriptionScannerDetection detection)
    {
        string text = PrescriptionPdfFieldExtractor.ExtractPdfText(pdfPath);
        string system = ResolveSystemName(detection, text);

        return detection.Type switch
        {
            PrescriptionScannerType.IteroLow or PrescriptionScannerType.IteroHigh => ExtractItero(text, system, pdfPath),
            PrescriptionScannerType.AsConnect => ExtractAsConnect(text, pdfPath),
            PrescriptionScannerType.Dexis => ExtractDexis(text, pdfPath),
            PrescriptionScannerType.DsCore => ExtractDsCore(text, pdfPath),
            PrescriptionScannerType.Sirona => ExtractTrios(text, pdfPath),
            PrescriptionScannerType.Medit => ExtractMedit(text, pdfPath),
            PrescriptionScannerType.Is3d => ExtractIs3d(text, system, pdfPath),
            _ => EmptySummary(system, pdfPath),
        };
    }

    private static string ResolveSystemName(PrescriptionScannerDetection detection, string text)
    {
        if (detection.Type == PrescriptionScannerType.Sirona)
        {
            return "TRIOS";
        }

        if (detection.Type == PrescriptionScannerType.Is3d
            && (text.Contains("Shining 3D", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Dental Cloud", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Case Information-Restore", StringComparison.OrdinalIgnoreCase)))
        {
            return "Shining 3D";
        }

        if (detection.Type == PrescriptionScannerType.Is3d)
        {
            return "IS3D";
        }

        if (detection.Type == PrescriptionScannerType.Medit)
        {
            return "Medit Link";
        }

        return detection.Type switch
        {
            PrescriptionScannerType.IteroLow or PrescriptionScannerType.IteroHigh => "iTero",
            PrescriptionScannerType.AsConnect => "ASConnect",
            PrescriptionScannerType.Dexis => "Dexis",
            PrescriptionScannerType.DsCore => "DS Core",
            _ => detection.DisplayName,
        };
    }

    private static PrescriptionSummary ExtractItero(string text, string system, string pdfPath) =>
        new()
        {
            System = system,
            SourcePdfPath = pdfPath,
            OrderId = MatchFirst(text, @"Order:\s*(\d+)"),
            DoctorName = CleanDoctor(MatchFirst(text, @"Doctor:\s*(.+)")),
            PatientName = MatchFirst(text, @"Patient:\s*(.+)"),
            Shade = ExtractIteroShade(text),
            TeethNumbers = ExtractIteroTreatmentTeeth(text),
            CommentsNotes = ExtractIteroNotes(text),
        };

    private static PrescriptionSummary ExtractAsConnect(string text, string pdfPath) =>
        new()
        {
            System = "ASConnect",
            SourcePdfPath = pdfPath,
            OrderId = MatchFirst(text, @"Order ID:\s*(\d+)"),
            DoctorName = MatchFirst(text, @"Dentist Name:\s*(.+)", ignoreCase: true),
            PatientName = MatchFirst(text, @"Patient Name:\s*(.+)", ignoreCase: true),
            Shade = MatchLabelValue(text, "Shade"),
            TeethNumbers = MatchFirst(text, @"Tooth No\.\s*\n\s*(\d{1,2})", ignoreCase: true)
                ?? MatchFirst(text, @"Tooth No\.\s*(\d{1,2})", ignoreCase: true),
            CommentsNotes = null,
        };

    private static PrescriptionSummary ExtractDexis(string text, string pdfPath)
    {
        string? comment = MatchBlock(text, @"Comment:\s*(.+?)(?:\n\s*Dr\.|\z)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        string? shade = null;
        if (!string.IsNullOrWhiteSpace(comment))
        {
            shade = MatchFirst(comment, @"SHADE\s+([\w/]+)", ignoreCase: true);
        }

        return new PrescriptionSummary
        {
            System = "Dexis",
            SourcePdfPath = pdfPath,
            OrderId = MatchFirst(text, @"CASE INFORMATION\s*\(\s*([A-Za-z]+-\d+)\s*\)", ignoreCase: true),
            DoctorName = MatchFirst(text, @"^Dr\.?\s*([^\n\r,]+)", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                ?? MatchFirst(text, @"^(.+?\s+DDS)\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase),
            PatientName = MatchFirst(text, @"^([A-Za-z][^\n\r\d]+?)\s*\r?\n\s*\d{2}/\d{2}/\d{4}\s+PWID:", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                ?? ExtractDexisPatientFromComment(comment),
            Shade = shade,
            TeethNumbers = ExtractHashTeeth(comment ?? text),
            CommentsNotes = CleanComment(comment),
        };
    }

    private static PrescriptionSummary ExtractDsCore(string text, string pdfPath)
    {
        string caseText = PrescriptionDsCoreParser.SliceFirstOrderBlock(text);
        string? comments = MatchBlock(caseText, @"Comments\s*\n\s*(.+?)(?:\n\s*(?:Order Number:|Case Sheet|\d+\s*$)|\z)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        return new PrescriptionSummary
        {
            System = "DS Core",
            SourcePdfPath = pdfPath,
            OrderId = ExtractDsCoreOrderNumber(caseText),
            DoctorName = PrescriptionDsCoreParser.ExtractDoctorName(caseText),
            PatientName = MatchFirst(caseText, @"Patient\s*\n\s*([^\n\r]+)", ignoreCase: true),
            Shade = PrescriptionDsCoreParser.ExtractShade(caseText, comments),
            TeethNumbers = PrescriptionDsCoreParser.ExtractTeethNumbers(caseText),
            CommentsNotes = CleanMultiline(comments),
        };
    }

    private static PrescriptionSummary ExtractTrios(string text, string pdfPath)
    {
        string? comments = MatchBlock(text, @"Comments\s*\n\s*(.+?)(?:\n\s*Restorations|\z)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        string? shade = MatchFirst(comments ?? text, @"SHADE\s+([\w/]+)", ignoreCase: true);

        return new PrescriptionSummary
        {
            System = "TRIOS",
            SourcePdfPath = pdfPath,
            DoctorName = MatchFirst(text, @"Dentist name:\s*(.+)", ignoreCase: true),
            PatientName = MatchFirst(text, @"Patient name:\s*(.+)", ignoreCase: true),
            Shade = shade ?? MatchFirst(text, @"Shade\s*\n\s*(.+)", ignoreCase: true),
            TeethNumbers = ExtractTriosTeeth(text),
            CommentsNotes = CleanMultiline(comments),
        };
    }

    private static PrescriptionSummary ExtractMedit(string text, string pdfPath)
    {
        (string? filePatient, string? fileDoctor) = TryParseMeditFileName(pdfPath);

        return new PrescriptionSummary
        {
            System = "Medit Link",
            SourcePdfPath = pdfPath,
            OrderId = MatchFirst(text, @"Order ID:\s*(\d+)", ignoreCase: true),
            DoctorName = MatchFirst(text, @"Dentist(?: Name)?:\s*(.+)", ignoreCase: true)
                ?? MatchFirst(text, @"Doctor:\s*(.+)", ignoreCase: true)
                ?? CleanDoctor(fileDoctor),
            PatientName = MatchFirst(text, @"Patient Name:\s*(.+)", ignoreCase: true)
                ?? MatchFirst(text, @"\*?\s*Patient Name\s*:\s*(.+)", ignoreCase: true)
                ?? filePatient,
            Shade = MatchFirst(text, @"(?m)^\s*([A-Z0-9][A-Z0-9/+-]*)\s*\r?\n\s*No\.", RegexOptions.IgnoreCase)
                ?? MatchLabelValue(text, "Shade"),
            TeethNumbers = ExtractCrownTeeth(text),
            CommentsNotes = MatchFirst(text, @"Notes:\s*(.+)", ignoreCase: true),
        };
    }

    private static (string? Patient, string? Doctor) TryParseMeditFileName(string pdfPath)
    {
        string fileName = Path.GetFileNameWithoutExtension(pdfPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return (null, null);
        }

        Match match = Regex.Match(fileName, @"^(.+?),\s*(Dr\.?\s*.+?)'s\s+Case", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return (null, null);
        }

        string patient = match.Groups[1].Value.Trim();
        string doctor = match.Groups[2].Value.Trim();
        return (
            string.IsNullOrWhiteSpace(patient) ? null : patient,
            string.IsNullOrWhiteSpace(doctor) ? null : doctor);
    }

    private static PrescriptionSummary ExtractIs3d(string text, string system, string pdfPath)
    {
        string? requirements = MatchBlock(text, @"Other requirements:\s*\n\s*(.+?)(?:\n\s*Due date|\n\s*Other Notes:|\z)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        string? notes = MatchFirst(text, @"Other Notes:\s*\n\s*(-|\S.*)", ignoreCase: true);
        if (notes == "-")
        {
            notes = null;
        }

        string? comments = JoinNonEmpty(requirements, notes);

        return new PrescriptionSummary
        {
            System = system,
            SourcePdfPath = pdfPath,
            DoctorName = MatchFirst(text, @"Doctor\s*\n\s*([^\n\r]+)", ignoreCase: true),
            PatientName = MatchFirst(text, @"Patient\s*\n\s*([^\n\r]+)", ignoreCase: true),
            Shade = MatchFirst(text, @"Shade:\s*(\S+)", ignoreCase: true)
                ?? MatchFirst(requirements ?? text, @"Shade:\s*(\S+)", ignoreCase: true),
            TeethNumbers = ExtractHashTeeth(requirements ?? text),
            CommentsNotes = CleanMultiline(comments),
        };
    }

    private static PrescriptionSummary EmptySummary(string system, string pdfPath) =>
        new() { System = system, SourcePdfPath = pdfPath };

    private static string? ExtractIteroShade(string text)
    {
        string? section = SliceSection(text, "Treatment Information", "Notes", "Tooth Diagram", "Procedure:");
        string? source = section ?? text;

        Match spaced = Regex.Match(source, @"-\s*/\s*([^/\n\r]+?)\s*/\s*-");
        if (spaced.Success)
        {
            return spaced.Groups[1].Value.Trim();
        }

        return MatchFirst(source, @"-/([^/\n\r]+)/-");
    }

    private static string? ExtractIteroNotes(string text)
    {
        Match match = Regex.Match(text, @"Notes\s*\r?\n(.*?)\r?\n\s*Procedure:",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        string value = match.Groups[1].Value.Trim();
        return string.IsNullOrWhiteSpace(value) || value is "--" or "—" ? null : value;
    }

    private static string? ExtractIteroTreatmentTeeth(string text) => ExtractIteroTreatmentTeethPublic(text);

    internal static string? ExtractIteroTreatmentTeethPublic(string text)
    {
        string? section = SliceSection(text, "Treatment Information", "Notes", "Tooth Diagram", "Procedure:");
        string source = section ?? text;

        var teeth = new SortedSet<int>();

        foreach (Match bridge in Regex.Matches(source, @"(?m)Bridge\s*:\s*(\d{1,2})\s*-\s*(\d{1,2})", RegexOptions.IgnoreCase))
        {
            if (!int.TryParse(bridge.Groups[1].Value, out int start)
                || !int.TryParse(bridge.Groups[2].Value, out int end))
            {
                continue;
            }

            int lo = Math.Min(start, end);
            int hi = Math.Max(start, end);
            for (int n = lo; n <= hi; n++)
            {
                if (n is >= 1 and <= 32)
                {
                    teeth.Add(n);
                }
            }
        }

        foreach (Match match in Regex.Matches(
                     source,
                     @"(?m)^\s*(\d{1,2})\s*\r?\n\s*(?:Crown|Abutment|Pontic|Inlay|Onlay|Veneer|Implant)\b",
                     RegexOptions.IgnoreCase))
        {
            if (int.TryParse(match.Groups[1].Value, out int n) && n is >= 1 and <= 32)
            {
                teeth.Add(n);
            }
        }

        if (teeth.Count > 0)
        {
            return string.Join(",", teeth);
        }

        if (string.IsNullOrWhiteSpace(section))
        {
            return ExtractCrownTeeth(text);
        }

        return ExtractCrownTeeth(section);
    }

    private static string? SliceSection(string text, string startLabel, params string[] endLabels)
    {
        int start = text.IndexOf(startLabel, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        start += startLabel.Length;
        int end = text.Length;
        foreach (string endLabel in endLabels)
        {
            int idx = text.IndexOf(endLabel, start, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && idx < end)
            {
                end = idx;
            }
        }

        string slice = text[start..end].Trim();
        return string.IsNullOrWhiteSpace(slice) ? null : slice;
    }

    private static string? ExtractDsCoreOrderNumber(string text)
    {
        if (!text.Contains("Order Number:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return MatchFirst(text, @"Order Number:\s*\n?\s*(\S+)", ignoreCase: true);
    }

    private static string? ExtractDexisPatientFromComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return null;
        }

        string? fromName = MatchFirst(comment, @"NAME\s*:\s*([^,]+)", ignoreCase: true);
        return string.IsNullOrWhiteSpace(fromName) ? null : fromName.Trim();
    }

    private static string? ExtractCrownTeeth(string text)
    {
        var teeth = new SortedSet<int>();
        foreach (Match match in Regex.Matches(text, @"(?m)^\s*(\d{1,2})\s*\r?\n\s*Crown", RegexOptions.IgnoreCase))
        {
            if (int.TryParse(match.Groups[1].Value, out int n) && n is >= 1 and <= 32)
            {
                teeth.Add(n);
            }
        }

        return teeth.Count == 0 ? null : string.Join(",", teeth);
    }

    private static string? ExtractTriosTeeth(string text)
    {
        var teeth = new SortedSet<int>();
        foreach (Match match in Regex.Matches(text, @"(?m)^\s*(\d{1,2})\s*\r?\n\s*\r?\n\s*Crown", RegexOptions.IgnoreCase))
        {
            if (int.TryParse(match.Groups[1].Value, out int n) && n is >= 1 and <= 32)
            {
                teeth.Add(n);
            }
        }

        if (teeth.Count == 0)
        {
            return ExtractHashTeeth(text);
        }

        return string.Join(",", teeth);
    }

    private static string? ExtractHashTeeth(string text)
    {
        var teeth = new SortedSet<int>();
        foreach (Match match in Regex.Matches(text, @"#\s*(\d{1,2})\b"))
        {
            if (int.TryParse(match.Groups[1].Value, out int n) && n is >= 1 and <= 32)
            {
                teeth.Add(n);
            }
        }

        return teeth.Count == 0 ? null : string.Join(",", teeth);
    }

    private static string? MatchLabelValue(string text, string label)
    {
        string? block = MatchBlock(text, $@"{Regex.Escape(label)}\s*\n\s*(.+?)(?:\n\s*[A-Z][a-z-]+(?:\s|$)|\z)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (string.IsNullOrWhiteSpace(block))
        {
            return MatchFirst(text, $@"{Regex.Escape(label)}:\s*(.+)", ignoreCase: true);
        }

        string line = block.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
        return string.IsNullOrWhiteSpace(line) || line is "--" or "—" ? null : line;
    }

    private static string? CleanDoctor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string cleaned = value.Trim();
        if (cleaned.StartsWith("Dr.", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[3..].Trim();
        }
        else if (cleaned.StartsWith("Dr ", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[2..].Trim();
        }

        return cleaned;
    }

    private static string? CleanComment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Replace('\r', ' ').Replace('\n', ' ').Trim();
    }

    private static string? CleanMultiline(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return string.Join(" ", value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Trim();
    }

    private static string? JoinNonEmpty(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a))
        {
            return b;
        }

        if (string.IsNullOrWhiteSpace(b))
        {
            return a;
        }

        return $"{a.Trim()} {b.Trim()}";
    }

    private static string? JoinTeeth(MatchCollection matches)
    {
        var teeth = new SortedSet<int>();
        foreach (Match match in matches)
        {
            if (int.TryParse(match.Groups[1].Value, out int n) && n is >= 1 and <= 32)
            {
                teeth.Add(n);
            }
        }

        return teeth.Count == 0 ? null : string.Join(",", teeth);
    }

    private static string? MatchFirst(string text, string pattern, bool ignoreCase = false) =>
        MatchFirst(text, pattern, ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);

    private static string? MatchFirst(string text, string pattern, RegexOptions options)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        Match match = Regex.Match(text, pattern, options);
        if (!match.Success || match.Groups.Count < 2)
        {
            return null;
        }

        string value = match.Groups[1].Value.Trim();
        int lineBreak = value.IndexOfAny(['\r', '\n']);
        if (lineBreak >= 0)
        {
            value = value[..lineBreak].Trim();
        }

        return string.IsNullOrWhiteSpace(value) || value is "--" or "—" ? null : value;
    }

    private static string? MatchBlock(string text, string pattern, RegexOptions options)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        Match match = Regex.Match(text, pattern, options);
        if (!match.Success || match.Groups.Count < 2)
        {
            return null;
        }

        string value = match.Groups[1].Value.Trim();
        return string.IsNullOrWhiteSpace(value) || value is "--" or "—" ? null : value;
    }
}
