using System.Drawing;
using System.IO;
using TesseractOCR;
using TesseractOCR.Enums;
using static StatsClient.MVVM.Core.LocalSettingsDB;
using Bitmap = System.Drawing.Bitmap;

namespace StatsClient.MVVM.Core;

public static class PrescriptionRegionOcr
{
    public static string? ReadFullPage(Bitmap source) =>
        ReadRegion(source, new FieldRegion("FullPage", 0, 0, 1, 1));

    public static string? ReadRegion(Bitmap source, FieldRegion region)
    {
        if (source is null)
            return null;

        try
        {
            EnsureTessDataReady();
            using Bitmap crop = CropRegion(source, region);
            string tessDataPath = DataBaseFolder;
            if (!File.Exists(Path.Combine(tessDataPath, "eng.traineddata")))
                return null;

            string tempPath = Path.Combine(DataBaseFolder, "Temp", $"rx_ocr_{Guid.NewGuid():N}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);
            try
            {
                crop.Save(tempPath, System.Drawing.Imaging.ImageFormat.Png);
                using var engine = new Engine(tessDataPath, Language.English, EngineMode.Default);
                using var pix = TesseractOCR.Pix.Image.LoadFromFile(tempPath);
                using var page = engine.Process(pix);
                string text = page.Text?.Trim() ?? "";
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch
                {
                    // Best-effort temp cleanup.
                }
            }
        }
        catch
        {
            return null;
        }
    }

    public static Bitmap CropRegion(Bitmap source, FieldRegion region)
    {
        int x = Clamp((int)Math.Round(region.X * source.Width), source.Width);
        int y = Clamp((int)Math.Round(region.Y * source.Height), source.Height);
        int w = Math.Max(1, (int)Math.Round(region.W * source.Width));
        int h = Math.Max(1, (int)Math.Round(region.H * source.Height));

        if (x + w > source.Width)
            w = source.Width - x;
        if (y + h > source.Height)
            h = source.Height - y;

        w = Math.Max(1, w);
        h = Math.Max(1, h);

        var rect = new Rectangle(x, y, w, h);
        return source.Clone(rect, source.PixelFormat);
    }

    private static int Clamp(int value, int max) =>
        max <= 0 ? 0 : Math.Clamp(value, 0, max - 1);

    private static void EnsureTessDataReady()
    {
        string dest = Path.Combine(DataBaseFolder, "eng.traineddata");
        if (File.Exists(dest))
        {
            return;
        }

        Directory.CreateDirectory(DataBaseFolder);
        var assembly = typeof(PrescriptionRegionOcr).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream("StatsClient.eng.traineddata")
            ?? assembly.GetManifestResourceStream("eng.traineddata");
        if (stream is null)
        {
            return;
        }

        using var output = File.Create(dest);
        stream.CopyTo(output);
    }
}
