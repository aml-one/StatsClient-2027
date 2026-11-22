using System.Drawing;

namespace StatsClient.MVVM.Core;

public static class PrescriptionInfoPositionResolver
{
    public static PointF ResolveOrigin(string stampHeader, float pageWidth, float pageHeight, float offsetX = 0, float offsetY = 0)
    {
        PointF baseOrigin = ResolveBaseOrigin(stampHeader, pageWidth, pageHeight);
        return new PointF(baseOrigin.X + offsetX, baseOrigin.Y + offsetY);
    }

    public static PointF ResolveBaseOrigin(string stampHeader, float pageWidth, float pageHeight)
    {
        if (PrescriptionPanPositionStore.TryGetInfoNormalized(stampHeader, out double normX, out double normY))
        {
            return new PointF((float)(normX * pageWidth), (float)(normY * pageHeight));
        }

        PrescriptionStampProfile? profile = PrescriptionStampProfiles.ForPageHeaderIsHigh(stampHeader);
        if (profile is not null && profile.HasValidInfo)
        {
            return profile.InfoDrawOrigin(pageWidth, pageHeight);
        }

        return LegacyOrigin(stampHeader);
    }

    public static float ResolveFontSize(string stampHeader, float pageHeight, string text, float fontSizeDelta = 0)
    {
        float baseSize;
        if (PrescriptionPanPositionStore.TryGetInfoFontSize(stampHeader, out float stored))
        {
            baseSize = stored;
        }
        else
        {
            PrescriptionStampProfile? profile = PrescriptionStampProfiles.ForPageHeaderIsHigh(stampHeader);
            baseSize = profile is not null && profile.HasValidInfo
                ? profile.InfoFontSize(pageHeight, text.Length)
                : text.Length > 20 ? 20f : 30f;
        }

        return Math.Clamp(baseSize + fontSizeDelta, 10f, 72f);
    }

    private static PointF LegacyOrigin(string stampHeader) =>
        stampHeader switch
        {
            "1" => new PointF(220, 225),
            "0" => new PointF(220, 205),
            "3" => new PointF(120, 383),
            "4" => new PointF(180, 312),
            "5" => new PointF(190, 235),
            "6" => new PointF(190, 205),
            "7" or "8" => new PointF(35, 635),
            PrescriptionSystemCatalog.UnknownCode => new PointF(220, 205),
            _ => new PointF(220, 205),
        };
}
