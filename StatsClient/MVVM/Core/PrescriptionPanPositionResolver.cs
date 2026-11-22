using System.Drawing;

namespace StatsClient.MVVM.Core;

public static class PrescriptionPanPositionResolver
{
    public static PointF ResolveOrigin(string stampHeader, float pageWidth, float pageHeight, float offsetX = 0, float offsetY = 0)
    {
        PointF baseOrigin = ResolveBaseOrigin(stampHeader, pageWidth, pageHeight);
        return new PointF(baseOrigin.X + offsetX, baseOrigin.Y + offsetY);
    }

    public static PointF ResolveBaseOrigin(string stampHeader, float pageWidth, float pageHeight)
    {
        if (PrescriptionPanPositionStore.TryGetNormalized(stampHeader, out double normX, out double normY))
        {
            return new PointF((float)(normX * pageWidth), (float)(normY * pageHeight));
        }

        PrescriptionStampProfile? profile = PrescriptionStampProfiles.ForPageHeaderIsHigh(stampHeader);
        if (profile is not null && profile.HasValidPan)
        {
            return profile.PanDrawOrigin(pageWidth, pageHeight);
        }

        return LegacyOrigin(stampHeader);
    }

    public static float ResolveFontSize(string stampHeader, float pageHeight, float fontSizeDelta = 0)
    {
        float baseSize;
        if (PrescriptionPanPositionStore.TryGetFontSize(stampHeader, out float stored))
        {
            baseSize = stored;
        }
        else
        {
            PrescriptionStampProfile? profile = PrescriptionStampProfiles.ForPageHeaderIsHigh(stampHeader);
            baseSize = profile is not null && profile.HasValidPan
                ? profile.PanFontSize(pageHeight)
                : 40f;
        }

        return Math.Clamp(baseSize + fontSizeDelta, 14f, 72f);
    }

    private static PointF LegacyOrigin(string stampHeader) =>
        stampHeader switch
        {
            "1" => new PointF(500, 55),
            "0" => new PointF(500, 38),
            "3" => new PointF(130, 22),
            "4" => new PointF(210, 40),
            "5" => new PointF(310, 25),
            "6" => new PointF(250, 30),
            "7" or "8" => new PointF(470, 60),
            PrescriptionSystemCatalog.UnknownCode => new PointF(500, 40),
            _ => new PointF(500, 40),
        };
}
