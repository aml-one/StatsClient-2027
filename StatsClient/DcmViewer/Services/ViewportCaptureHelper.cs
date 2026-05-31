using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DCMViewer.Services;

internal static class ViewportCaptureHelper
{
    public static byte[] CapturePng(FrameworkElement element, int? width = null, int? height = null)
    {
        var w = width ?? (int)Math.Max(1, element.ActualWidth);
        var h = height ?? (int)Math.Max(1, element.ActualHeight);
        if (w < 1 || h < 1)
        {
            return [];
        }

        element.UpdateLayout();
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(element);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
