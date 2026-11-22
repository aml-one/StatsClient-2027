using Syncfusion.PdfToImageConverter;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;

namespace StatsClient.MVVM.Core;

public static class PrescriptionPdfPageRenderer
{
    public const int RenderDpi = 200;

    public static Bitmap RenderFirstPage(string pdfPath)
    {
        using var converter = new PdfToImageConverter();
        using FileStream stream = File.OpenRead(pdfPath);
        converter.Load(stream);
        using Stream pageStream = converter.Convert(0, false, false);
        return new Bitmap(pageStream);
    }

    public static BitmapImage ToBitmapImage(Bitmap bitmap)
    {
        using MemoryStream memory = new();
        bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
        memory.Position = 0;

        BitmapImage image = new();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = memory;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
