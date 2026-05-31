using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StatsClient.MVVM.Core;

/// <summary>
/// Prepares clipboard/camera photos for NVIDIA Gemma vision (inline base64 limit ~180 KB).
/// </summary>
public static class PanStackVisionImageHelper
{
    public const int NvidiaInlineImageMaxBytes = 170 * 1024;
    public const int MaxEdgePixels = 1280;

    /// <summary>Default overlap between adjacent vertical crops (fraction of full width).</summary>
    public const double DefaultSectionOverlap = 0.07;

    /// <summary>
    /// Trims shelf, floor, and counter margins so vision coords match the displayed stack photo.
    /// </summary>
    public static PanStackVisionCropResult CropToPanStackRegion(byte[] imagePng, double paddingFraction = 0.025)
    {
        if (imagePng.Length == 0)
        {
            return new PanStackVisionCropResult { ImagePng = imagePng };
        }

        var source = DecodeBitmap(imagePng);
        var width = source.PixelWidth;
        var height = source.PixelHeight;
        if (width < 32 || height < 32)
        {
            return new PanStackVisionCropResult { ImagePng = imagePng };
        }

        var formatted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        formatted.Freeze();

        var stride = width * 4;
        var pixels = new byte[stride * height];
        formatted.CopyPixels(pixels, stride, 0);

        var step = Math.Max(1, Math.Min(width, height) / 500);
        var minX = width;
        var maxX = -1;
        var minY = height;
        var maxY = -1;
        var hits = 0;

        for (var y = 0; y < height; y += step)
        {
            for (var x = 0; x < width; x += step)
            {
                if (!IsPanBoxPixel(pixels, x, y, stride))
                {
                    continue;
                }

                hits++;
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
        }

        if (hits < 40 || maxX <= minX || maxY <= minY)
        {
            return new PanStackVisionCropResult { ImagePng = imagePng };
        }

        var padX = Math.Max(8, (int)((maxX - minX) * paddingFraction));
        var padY = Math.Max(8, (int)((maxY - minY) * paddingFraction));

        var px0 = Math.Clamp(minX - padX, 0, width - 2);
        var py0 = Math.Clamp(minY - padY, 0, height - 2);
        var px1 = Math.Clamp(maxX + padX + 1, px0 + 2, width);
        var py1 = Math.Clamp(maxY + padY + 1, py0 + 2, height);

        var cropWidth = px1 - px0;
        var cropHeight = py1 - py0;
        if (cropWidth < width * 0.25 || cropHeight < height * 0.20)
        {
            return new PanStackVisionCropResult { ImagePng = imagePng };
        }

        var crop = new CroppedBitmap(source, new Int32Rect(px0, py0, cropWidth, cropHeight));
        crop.Freeze();

        return new PanStackVisionCropResult
        {
            ImagePng = EncodePng(crop),
            WasCropped = true,
            OriginalX0 = px0 / (double)width,
            OriginalX1 = px1 / (double)width,
            OriginalY0 = py0 / (double)height,
            OriginalY1 = py1 / (double)height
        };
    }

    private static bool IsPanBoxPixel(byte[] pixels, int x, int y, int stride)
    {
        var i = y * stride + x * 4;
        var b = pixels[i];
        var g = pixels[i + 1];
        var r = pixels[i + 2];

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        if (max - min < 30)
        {
            return false;
        }

        if (g >= 60 && g > r + 18 && g > b + 12)
        {
            return true;
        }

        return b >= 60 && b > r + 12 && b >= g - 8;
    }

    /// <summary>
    /// Splits a pan-stack photo into vertical strips for separate vision calls.
    /// </summary>
    public static IReadOnlyList<PanStackVisionSection> SplitIntoVerticalSections(
        byte[] imagePng,
        int sectionCount,
        double overlapFraction = DefaultSectionOverlap)
    {
        if (sectionCount <= 1 || imagePng.Length == 0)
        {
            return [new PanStackVisionSection { Index = 0, Count = 1, ImagePng = imagePng, X0 = 0, X1 = 1 }];
        }

        var source = DecodeBitmap(imagePng);
        var width = source.PixelWidth;
        var height = source.PixelHeight;
        if (width < 2 || height < 2)
        {
            return [new PanStackVisionSection { Index = 0, Count = 1, ImagePng = imagePng, X0 = 0, X1 = 1 }];
        }

        overlapFraction = Math.Clamp(overlapFraction, 0, 0.2);
        var sections = new List<PanStackVisionSection>(sectionCount);

        for (var i = 0; i < sectionCount; i++)
        {
            var x0 = i / (double)sectionCount - overlapFraction * 0.5;
            var x1 = (i + 1) / (double)sectionCount + overlapFraction * 0.5;
            x0 = Math.Clamp(x0, 0, 1);
            x1 = Math.Clamp(x1, x0 + 0.02, 1);

            var px0 = (int)Math.Floor(x0 * width);
            var px1 = (int)Math.Ceiling(x1 * width);
            var cropWidth = Math.Clamp(px1 - px0, 1, width - px0);

            var crop = new CroppedBitmap(source, new Int32Rect(px0, 0, cropWidth, height));
            crop.Freeze();

            sections.Add(new PanStackVisionSection
            {
                Index = i,
                Count = sectionCount,
                ImagePng = EncodePng(crop),
                X0 = x0,
                X1 = x1
            });
        }

        return sections;
    }

    public static byte[] PrepareForVisionApi(byte[] imageBytes, string mimeType = "image/png")
    {
        if (imageBytes.Length == 0)
        {
            return imageBytes;
        }

        if (imageBytes.Length <= NvidiaInlineImageMaxBytes && !NeedsDownscale(imageBytes))
        {
            return imageBytes;
        }

        return EncodeJpeg(ScaleDownIfNeeded(imageBytes), quality: 82);
    }

    private static bool NeedsDownscale(byte[] imageBytes)
    {
        try
        {
            using var stream = new MemoryStream(imageBytes);
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnLoad);
            return frame.PixelWidth > MaxEdgePixels || frame.PixelHeight > MaxEdgePixels;
        }
        catch
        {
            return false;
        }
    }

    private static BitmapSource DecodeBitmap(byte[] imageBytes)
    {
        using var stream = new MemoryStream(imageBytes);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }

    private static byte[] EncodePng(BitmapSource source)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(source));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    private static BitmapSource ScaleDownIfNeeded(byte[] imageBytes)
    {
        using var stream = new MemoryStream(imageBytes);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];

        var maxEdge = Math.Max(frame.PixelWidth, frame.PixelHeight);
        if (maxEdge <= MaxEdgePixels)
        {
            return frame;
        }

        var scale = MaxEdgePixels / (double)maxEdge;
        var scaled = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
        scaled.Freeze();
        return scaled;
    }

    private static byte[] EncodeJpeg(BitmapSource source, int quality)
    {
        var encoder = new JpegBitmapEncoder { QualityLevel = Math.Clamp(quality, 40, 95) };
        encoder.Frames.Add(BitmapFrame.Create(source));

        byte[]? result = null;
        for (var q = quality; q >= 50; q -= 8)
        {
            encoder = new JpegBitmapEncoder { QualityLevel = q };
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            result = ms.ToArray();
            if (result.Length <= NvidiaInlineImageMaxBytes)
            {
                return result;
            }
        }

        return result ?? EncodePngFallback(source);

        static byte[] EncodePngFallback(BitmapSource src)
        {
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(src));
            using var ms = new MemoryStream();
            enc.Save(ms);
            return ms.ToArray();
        }
    }
}
