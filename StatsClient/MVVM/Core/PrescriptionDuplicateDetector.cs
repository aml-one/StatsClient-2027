using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Bitmap = System.Drawing.Bitmap;

namespace StatsClient.MVVM.Core;

public sealed record LastPrescriptionFingerprint(
    string? SourcePdfHash,
    ulong RenderDHash,
    string? SemanticKey,
    string PanNumber,
    string SavedPngPath);

public enum DuplicateMatchTier
{
    PdfHash,
    VisualFingerprint,
    SemanticKey
}

public sealed record DuplicateMatchResult(
    DuplicateMatchTier Tier,
    string Reason,
    LastPrescriptionFingerprint LastFingerprint);

public static class PrescriptionDuplicateDetector
{
    /// <summary>
    /// Visual dHash is stored on fingerprints for diagnostics only. Same-scanner PDF templates
    /// (e.g. Medit Link) produce near-identical 9×8 hashes for different cases and must not
    /// trigger duplicate review on their own.
    /// </summary>
    public static LastPrescriptionFingerprint BuildFingerprint(
        string pdfPath,
        Bitmap renderBitmap,
        string pageHeaderIsHigh)
    {
        return new LastPrescriptionFingerprint(
            ComputePdfHash(pdfPath),
            ComputeDHash(renderBitmap),
            ExtractSemanticKey(pdfPath, pageHeaderIsHigh),
            string.Empty,
            string.Empty);
    }

    public static DuplicateMatchResult? TryMatch(
        LastPrescriptionFingerprint? last,
        string pdfPath,
        Bitmap renderBitmap,
        string pageHeaderIsHigh)
    {
        if (last is null || string.IsNullOrWhiteSpace(last.SavedPngPath) || !File.Exists(last.SavedPngPath))
        {
            return null;
        }

        var currentHash = ComputePdfHash(pdfPath);
        if (!string.IsNullOrEmpty(currentHash)
            && !string.IsNullOrEmpty(last.SourcePdfHash)
            && string.Equals(currentHash, last.SourcePdfHash, StringComparison.OrdinalIgnoreCase))
        {
            return new DuplicateMatchResult(
                DuplicateMatchTier.PdfHash,
                "Same PDF file (exact byte match)",
                last);
        }

        var currentKey = ExtractSemanticKey(pdfPath, pageHeaderIsHigh);
        if (!string.IsNullOrWhiteSpace(currentKey)
            && string.Equals(currentKey, last.SemanticKey, StringComparison.OrdinalIgnoreCase))
        {
            return new DuplicateMatchResult(
                DuplicateMatchTier.SemanticKey,
                "Same order ID and patient name",
                last);
        }

        return null;
    }

    public static string? ComputePdfHash(string pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
        {
            return null;
        }

        try
        {
            var hash = SHA256.HashData(File.ReadAllBytes(pdfPath));
            return Convert.ToHexString(hash);
        }
        catch
        {
            return null;
        }
    }

    public static ulong ComputeDHash(Bitmap bitmap)
    {
        using var resized = new Bitmap(9, 8, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(resized))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            graphics.DrawImage(bitmap, 0, 0, 9, 8);
        }

        var gray = new int[72];
        var index = 0;
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 9; x++)
            {
                var pixel = resized.GetPixel(x, y);
                gray[index++] = (pixel.R * 299 + pixel.G * 587 + pixel.B * 114) / 1000;
            }
        }

        ulong hash = 0;
        var bit = 0;
        for (var y = 0; y < 8; y++)
        {
            var rowStart = y * 9;
            for (var x = 0; x < 8; x++)
            {
                if (gray[rowStart + x] > gray[rowStart + x + 1])
                {
                    hash |= 1UL << bit;
                }

                bit++;
            }
        }

        return hash;
    }

    public static int HammingDistance(ulong a, ulong b)
    {
        var xor = a ^ b;
        var count = 0;
        while (xor != 0)
        {
            count += (int)(xor & 1);
            xor >>= 1;
        }

        return count;
    }

    public static string? ExtractSemanticKey(string pdfPath, string pageHeaderIsHigh)
    {
        var text = ExtractPdfText(pdfPath);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return pageHeaderIsHigh switch
        {
            "0" or "1" => BuildSemanticKey(
                MatchFirst(text, @"Order:\s*(\d+)"),
                MatchFirst(text, @"Patient:\s*(.+)")),
            "3" => BuildSemanticKey(
                MatchFirst(text, @"Case number:\s*(\S+)", ignoreCase: true),
                MatchFirst(text, @"Patient name:\s*(.+)", ignoreCase: true)),
            "4" => BuildSemanticKey(
                MatchFirst(text, @"Order Number:\s*(\d+)", ignoreCase: true),
                MatchFirst(text, @"Patient(?:\s*Name)?:\s*(.+)", ignoreCase: true)),
            "5" => BuildSemanticKey(
                MatchFirst(text, @"Order ID:\s*(\d+)", ignoreCase: true),
                MatchFirst(text, @"Patient Name:\s*(.+)", ignoreCase: true)
                    ?? MatchFirst(text, @"Case Name:\s*(.+)", ignoreCase: true),
                MatchFirst(text, @"Scanned Date:\s*(.+)", ignoreCase: true)),
            "6" => BuildSemanticKey(
                ExtractDsCoreOrderNumber(text),
                MatchFirst(text, @"Patient(?:\s*Name)?:\s*(.+)", ignoreCase: true)),
            "7" => BuildSemanticKey(
                MatchFirst(text, @"Order ID:\s*(\d+)", ignoreCase: true),
                MatchFirst(text, @"Patient Name:\s*(.+)", ignoreCase: true)),
            "8" => BuildSemanticKey(
                MatchFirst(text, @"CASE INFORMATION\s*\(\s*([A-Za-z]+-\d+)\s*\)", ignoreCase: true),
                DexisPatientForSemanticKey(text)),
            _ => null
        };
    }

    private static string ExtractPdfText(string pdfPath) =>
        PrescriptionPdfFieldExtractor.ExtractPdfText(pdfPath);

    private static string? ExtractDsCoreOrderNumber(string text)
    {
        if (!text.Contains("Order Number:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var start = text.IndexOf("Order Number:", StringComparison.OrdinalIgnoreCase);
        var slice = text.Substring(start, Math.Min(48, text.Length - start))
            .Replace("Order Number:", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Order Date:", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Date Due:", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        var spaceIndex = slice.IndexOf(' ');
        if (spaceIndex > 0)
        {
            slice = slice[..spaceIndex];
        }

        return string.IsNullOrWhiteSpace(slice) ? null : slice;
    }

    private static string? DexisPatientForSemanticKey(string text)
    {
        Match match = Regex.Match(text, @"^([A-Za-z][^\n\r\d]+?)\s*\r?\n\s*\d{2}/\d{2}/\d{4}\s+PWID:",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string? MatchFirst(string text, string pattern, bool ignoreCase = false)
    {
        var options = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
        var match = Regex.Match(text, pattern, options);
        if (!match.Success || match.Groups.Count < 2)
        {
            return null;
        }

        var value = match.Groups[1].Value.Trim();
        var lineBreak = value.IndexOfAny(['\r', '\n']);
        if (lineBreak >= 0)
        {
            value = value[..lineBreak].Trim();
        }

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? BuildSemanticKey(string? orderId, string? patient, string? disambiguator = null)
    {
        var normalizedOrder = NormalizeSemanticPart(orderId);
        var normalizedPatient = NormalizeSemanticPart(patient);
        if (string.IsNullOrWhiteSpace(normalizedOrder) || string.IsNullOrWhiteSpace(normalizedPatient))
        {
            return null;
        }

        var normalizedExtra = NormalizeSemanticPart(disambiguator);
        return string.IsNullOrWhiteSpace(normalizedExtra)
            ? $"{normalizedOrder}|{normalizedPatient}"
            : $"{normalizedOrder}|{normalizedPatient}|{normalizedExtra}";
    }

    private static string? NormalizeSemanticPart(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var builder = new StringBuilder(value.Length);
        var previousWasSpace = false;
        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                previousWasSpace = false;
            }
            else if (char.IsWhiteSpace(ch))
            {
                if (!previousWasSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }
            }
        }

        var normalized = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
