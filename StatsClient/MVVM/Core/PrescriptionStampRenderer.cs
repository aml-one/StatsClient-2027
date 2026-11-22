using Syncfusion.Pdf.Graphics;
using System.Drawing;

namespace StatsClient.MVVM.Core;

public static class PrescriptionStampRenderer
{
    public static void DrawPanNumber(
        PdfGraphics graphics,
        PrescriptionStampProfile profile,
        float pageWidth,
        float pageHeight,
        string panNumber,
        PointF? originOverride = null)
    {
        if (string.IsNullOrWhiteSpace(panNumber))
        {
            return;
        }

        float fontSize = profile.PanFontSize(pageHeight);
        PdfFont font = CreateFont(fontSize, bold: true);
        PointF origin = originOverride ?? profile.PanDrawOrigin(pageWidth, pageHeight);
        graphics.DrawString(panNumber, font, PdfBrushes.Black, origin);
    }

    public static void DrawPanNumberAt(
        PdfGraphics graphics,
        string panNumber,
        PointF origin,
        float fontSize)
    {
        if (string.IsNullOrWhiteSpace(panNumber))
        {
            return;
        }

        PdfFont font = CreateFont(fontSize, bold: true);
        graphics.DrawString(panNumber, font, PdfBrushes.Black, origin);
    }

    public static void DrawRush(PdfGraphics graphics, PrescriptionStampProfile profile, float pageWidth, float pageHeight)
    {
        float fontSize = profile.RushFontSize(pageHeight);
        PdfFont font = CreateFont(fontSize, bold: true);
        PointF origin = profile.RushDrawOrigin(pageWidth, pageHeight);
        graphics.DrawString("RUSH", font, PdfBrushes.DarkRed, origin);
    }

    public static void DrawRushAt(PdfGraphics graphics, PointF origin, float fontSize)
    {
        PdfFont font = CreateFont(fontSize, bold: true);
        graphics.DrawString("RUSH", font, PdfBrushes.DarkRed, origin);
    }

    public static void DrawInfoText(
        PdfGraphics graphics,
        PrescriptionStampProfile profile,
        float pageWidth,
        float pageHeight,
        string text,
        PdfBrush? brush = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        float fontSize = profile.InfoFontSize(pageHeight, text.Length);
        PdfFont font = CreateFont(fontSize, bold: true);
        PointF origin = profile.InfoDrawOrigin(pageWidth, pageHeight);
        graphics.DrawString(text, font, brush ?? PdfBrushes.Black, origin);
    }

    public static void DrawInfoTextAt(PdfGraphics graphics, string text, PointF origin, float fontSize, PdfBrush? brush = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        PdfFont font = CreateFont(fontSize, bold: true);
        graphics.DrawString(text, font, brush ?? PdfBrushes.Black, origin);
    }

    private static PdfFont CreateFont(float size, bool bold) =>
        new PdfStandardFont(PdfFontFamily.Helvetica, size, bold ? PdfFontStyle.Bold : PdfFontStyle.Regular);
}
