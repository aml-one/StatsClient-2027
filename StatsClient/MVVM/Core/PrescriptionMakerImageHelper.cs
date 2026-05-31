using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StatsClient.MVVM.Core;

internal static class PrescriptionMakerImageHelper
{
    private static readonly string[] ImageExtensions =
    [
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tif", ".tiff"
    ];

    public static bool IsImageFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        return ImageExtensions.Any(ext => ext.Equals(extension, StringComparison.OrdinalIgnoreCase));
    }

    public static bool TryLoadPngBytesFromFile(string path, out byte[] pngBytes)
    {
        pngBytes = [];
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            using var stream = File.OpenRead(path);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
            {
                return false;
            }

            pngBytes = EncodePng(decoder.Frames[0]);
            return pngBytes.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryGetClipboardImagePng(out byte[] pngBytes)
    {
        pngBytes = [];
        try
        {
            if (!Clipboard.ContainsImage())
            {
                return false;
            }

            if (Clipboard.GetImage() is not BitmapSource bitmapSource)
            {
                return false;
            }

            pngBytes = EncodePng(bitmapSource);
            return pngBytes.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public static string? GetFirstImagePathFromDrop(IDataObject data)
    {
        if (!data.GetDataPresent(DataFormats.FileDrop))
        {
            return null;
        }

        if (data.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return null;
        }

        return files.FirstOrDefault(IsImageFile);
    }

    private static byte[] EncodePng(BitmapSource source)
    {
        var converted = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(converted));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
