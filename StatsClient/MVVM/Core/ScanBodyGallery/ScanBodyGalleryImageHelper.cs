using System.IO;

using System.Windows.Media;

using System.Windows.Media.Imaging;



namespace StatsClient.MVVM.Core.ScanBodyGallery;



public static class ScanBodyGalleryImageHelper

{

    public const int ThumbnailMaxEdge = 170;

    public const int MaxImageBytes = 5 * 1024 * 1024;

    public const int MaxImagesPerEntry = 12;



    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)

    {

        ".png", ".jpg", ".jpeg"

    };



    public static bool IsAllowedImageFile(string path)

    {

        string ext = Path.GetExtension(path);

        return AllowedExtensions.Contains(ext);

    }



    public static (byte[] ImageData, byte[] ThumbnailData, string ContentType, string FileName) PrepareImage(

        byte[] sourceBytes,

        string? fileName = null)

    {

        if (sourceBytes.Length == 0)

            throw new InvalidOperationException("Image is empty.");



        if (sourceBytes.Length > MaxImageBytes)

            throw new InvalidOperationException("Image exceeds the 5 MB limit.");



        using var stream = new MemoryStream(sourceBytes);

        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

        var frame = decoder.Frames[0];

        var png = EncodePng(frame);

        var thumb = CreateThumbnail(frame, ThumbnailMaxEdge);

        var name = string.IsNullOrWhiteSpace(fileName) ? "image.png" : fileName;

        return (png, thumb, "image/png", name);

    }



    public static BitmapImage? ToBitmapImage(byte[]? bytes)

    {

        if (bytes is null || bytes.Length == 0)

            return null;



        var image = new BitmapImage();

        using var stream = new MemoryStream(bytes);

        image.BeginInit();

        image.CacheOption = BitmapCacheOption.OnLoad;

        image.StreamSource = stream;

        image.EndInit();

        image.Freeze();

        return image;

    }



    private static byte[] CreateThumbnail(BitmapSource source, int maxEdge)

    {

        double scale = Math.Min(1.0, maxEdge / (double)Math.Max(source.PixelWidth, source.PixelHeight));

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

}

