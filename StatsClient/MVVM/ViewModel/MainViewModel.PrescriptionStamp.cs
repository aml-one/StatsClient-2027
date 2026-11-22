using StatsClient.MVVM.Core;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Parsing;
using System.Drawing;

namespace StatsClient.MVVM.ViewModel;

public partial class MainViewModel
{
    private static void DrawPrescriptionPanNumber(
        PdfGraphics graphics,
        PdfLoadedPage page,
        string stampHeader,
        string panNumber,
        float offsetX = 0,
        float offsetY = 0,
        float fontSizeDelta = 0)
    {
        SizeF pageSize = page.Graphics.ClientSize;
        PointF origin = PrescriptionPanPositionResolver.ResolveOrigin(
            stampHeader,
            pageSize.Width,
            pageSize.Height,
            offsetX,
            offsetY);
        float fontSize = PrescriptionPanPositionResolver.ResolveFontSize(stampHeader, pageSize.Height, fontSizeDelta);

        bool useCustomDraw = offsetX != 0
            || offsetY != 0
            || fontSizeDelta != 0
            || PrescriptionPanPositionStore.TryGetNormalized(stampHeader, out _, out _)
            || PrescriptionPanPositionStore.TryGetFontSize(stampHeader, out _);

        PrescriptionStampProfile? profile = PrescriptionStampProfiles.ForPageHeaderIsHigh(stampHeader);
        if (!useCustomDraw && profile is not null && profile.HasValidPan)
        {
            PrescriptionStampRenderer.DrawPanNumber(graphics, profile, pageSize.Width, pageSize.Height, panNumber);
            return;
        }

        PrescriptionStampRenderer.DrawPanNumberAt(graphics, panNumber, origin, fontSize);
    }

    private static void DrawPrescriptionRush(
        PdfGraphics graphics,
        PdfLoadedPage page,
        string stampHeader,
        float offsetX = 0,
        float offsetY = 0,
        float fontSizeDelta = 0)
    {
        SizeF pageSize = page.Graphics.ClientSize;
        PointF origin = PrescriptionRushPositionResolver.ResolveOrigin(
            stampHeader,
            pageSize.Width,
            pageSize.Height,
            offsetX,
            offsetY);
        float fontSize = PrescriptionRushPositionResolver.ResolveFontSize(stampHeader, pageSize.Height, fontSizeDelta);

        bool useCustomDraw = offsetX != 0
            || offsetY != 0
            || fontSizeDelta != 0
            || PrescriptionPanPositionStore.TryGetRushNormalized(stampHeader, out _, out _)
            || PrescriptionPanPositionStore.TryGetRushFontSize(stampHeader, out _);

        PrescriptionStampProfile? profile = PrescriptionStampProfiles.ForPageHeaderIsHigh(stampHeader);
        if (!useCustomDraw && profile is not null && profile.HasValidRush)
        {
            PrescriptionStampRenderer.DrawRush(graphics, profile, pageSize.Width, pageSize.Height);
            return;
        }

        PrescriptionStampRenderer.DrawRushAt(graphics, origin, fontSize);
    }

    private static void DrawPrescriptionInfoText(
        PdfGraphics graphics,
        PdfLoadedPage page,
        string stampHeader,
        string text,
        float offsetX = 0,
        float offsetY = 0,
        float fontSizeDelta = 0)
    {
        SizeF pageSize = page.Graphics.ClientSize;
        PointF origin = PrescriptionInfoPositionResolver.ResolveOrigin(
            stampHeader,
            pageSize.Width,
            pageSize.Height,
            offsetX,
            offsetY);
        float fontSize = PrescriptionInfoPositionResolver.ResolveFontSize(stampHeader, pageSize.Height, text, fontSizeDelta);

        bool useCustomDraw = offsetX != 0
            || offsetY != 0
            || fontSizeDelta != 0
            || PrescriptionPanPositionStore.TryGetInfoNormalized(stampHeader, out _, out _)
            || PrescriptionPanPositionStore.TryGetInfoFontSize(stampHeader, out _);

        PrescriptionStampProfile? profile = PrescriptionStampProfiles.ForPageHeaderIsHigh(stampHeader);
        if (!useCustomDraw && profile is not null && profile.HasValidInfo)
        {
            PrescriptionStampRenderer.DrawInfoText(graphics, profile, pageSize.Width, pageSize.Height, text);
            return;
        }

        PrescriptionStampRenderer.DrawInfoTextAt(graphics, text, origin, fontSize);
    }
}
