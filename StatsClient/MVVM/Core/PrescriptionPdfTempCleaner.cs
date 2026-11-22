using StatsClient.MVVM.Model;
using System.IO;

namespace StatsClient.MVVM.Core;

public static class PrescriptionPdfTempCleaner
{
    private static readonly string[] PdfSuffixes = ["", "-copy", "-original", "-pan-only", "-pre-info"];

    public static void PrepareFolder(string pdfTempFolder)
    {
        Directory.CreateDirectory(pdfTempFolder);
        if (!Directory.Exists(pdfTempFolder))
        {
            return;
        }

        DateTime today = DateTime.Today;
        var handledPans = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string statePath in Directory.EnumerateFiles(pdfTempFolder, "*.rxstate.json"))
        {
            string fileName = Path.GetFileName(statePath);
            string panNumber = fileName[..^".rxstate.json".Length];
            if (string.IsNullOrWhiteSpace(panNumber))
            {
                continue;
            }

            handledPans.Add(panNumber);
            PrescriptionSavedState? state = PrescriptionSavedStateStore.TryLoad(pdfTempFolder, panNumber);
            DateTime savedDate = (state?.SavedAt ?? File.GetLastWriteTime(statePath)).Date;
            if (savedDate < today)
            {
                DeletePrescriptionTempFiles(pdfTempFolder, panNumber);
            }
        }

        foreach (string filePath in Directory.EnumerateFiles(pdfTempFolder))
        {
            if (filePath.EndsWith(".rxstate.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (File.GetLastWriteTime(filePath).Date >= today)
            {
                continue;
            }

            string? panNumber = TryExtractPanNumber(Path.GetFileName(filePath));
            if (panNumber is not null && handledPans.Contains(panNumber))
            {
                continue;
            }

            TryDelete(filePath);
        }
    }

    public static void DeletePrescriptionTempFiles(string pdfTempFolder, string panNumber)
    {
        foreach (string suffix in PdfSuffixes)
        {
            TryDelete(Path.Combine(pdfTempFolder, panNumber + suffix + ".pdf"));
        }

        TryDelete(PrescriptionSavedStateStore.StatePath(pdfTempFolder, panNumber));
    }

    private static string? TryExtractPanNumber(string fileName)
    {
        if (fileName.EndsWith(".rxstate.json", StringComparison.OrdinalIgnoreCase))
        {
            return fileName[..^".rxstate.json".Length];
        }

        if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string baseName = Path.GetFileNameWithoutExtension(fileName);
        foreach (string suffix in PdfSuffixes.Skip(1))
        {
            if (baseName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return baseName[..^suffix.Length];
            }
        }

        return baseName;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
