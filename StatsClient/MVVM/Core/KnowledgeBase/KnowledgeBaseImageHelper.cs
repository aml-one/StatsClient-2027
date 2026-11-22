using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StatsClient.MVVM.Core.KnowledgeBase;

public static class KnowledgeBaseImageHelper
{
    public const int ThumbnailMaxEdge = 256;

    public static (byte[] ImageData, byte[] ThumbnailData, string ContentType, string FileName) PrepareFile(
        byte[] sourceBytes,
        string? fileName = null)
    {
        if (IsPdfFileName(fileName))
        {
            return PreparePdf(sourceBytes, fileName);
        }

        return PrepareImage(sourceBytes, fileName);
    }

    public static (byte[] ImageData, byte[] ThumbnailData, string ContentType, string FileName) PrepareImage(
        byte[] sourceBytes,
        string? fileName = null)
    {
        if (sourceBytes.Length == 0)
        {
            throw new InvalidOperationException("Image is empty.");
        }

        using var stream = new MemoryStream(sourceBytes);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        var png = EncodePng(frame);
        var thumb = CreateThumbnail(frame);
        var name = string.IsNullOrWhiteSpace(fileName) ? "image.png" : fileName;
        return (png, thumb, "image/png", name);
    }

    public static (byte[] ImageData, byte[] ThumbnailData, string ContentType, string FileName) PreparePdf(
        byte[] sourceBytes,
        string? fileName = null)
    {
        if (sourceBytes.Length == 0)
        {
            throw new InvalidOperationException("PDF is empty.");
        }

        var name = string.IsNullOrWhiteSpace(fileName) ? "document.pdf" : fileName;
        return (sourceBytes, CreatePdfPlaceholderThumbnail(), "application/pdf", name);
    }

    public static bool IsPdfFileName(string? fileName) =>
        Path.GetExtension(fileName ?? string.Empty).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    public static BitmapImage? ToBitmapImage(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        var image = new BitmapImage();
        using var stream = new MemoryStream(bytes);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static byte[] CreateThumbnail(BitmapSource source)
    {
        double scale = Math.Min(1.0, ThumbnailMaxEdge / Math.Max(source.PixelWidth, source.PixelHeight));
        var width = Math.Max(1, (int)(source.PixelWidth * scale));
        var height = Math.Max(1, (int)(source.PixelHeight * scale));
        var transformed = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        return EncodePng(transformed);
    }

    private static byte[] EncodePng(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    private static byte[] CreatePdfPlaceholderThumbnail()
    {
        const int size = 128;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(
                new SolidColorBrush(Color.FromRgb(0x56, 0x6C, 0x82)),
                new Pen(new SolidColorBrush(Color.FromRgb(0x8F, 0xA4, 0xB8)), 1),
                new Rect(0, 0, size, size));
            var text = new FormattedText(
                "PDF",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI Semibold"),
                22,
                Brushes.White,
                1.0);
            dc.DrawText(text, new Point((size - text.Width) / 2, (size - text.Height) / 2));
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return EncodePng(bitmap);
    }
}
