using System.Drawing;

namespace StatsClient.MVVM.Core;

public static class PrescriptionRushPositionResolver
{
    public static PointF ResolveOrigin(string stampHeader, float pageWidth, float pageHeight, float offsetX = 0, float offsetY = 0)
    {
        PointF baseOrigin = ResolveBaseOrigin(stampHeader, pageWidth, pageHeight);
        return new PointF(baseOrigin.X + offsetX, baseOrigin.Y + offsetY);
    }

    public static PointF ResolveBaseOrigin(string stampHeader, float pageWidth, float pageHeight)
    {
        if (PrescriptionPanPositionStore.TryGetRushNormalized(stampHeader, out double normX, out double normY))
        {
            return new PointF((float)(normX * pageWidth), (float)(normY * pageHeight));
        }

        PrescriptionStampProfile? profile = PrescriptionStampProfiles.ForPageHeaderIsHigh(stampHeader);
        if (profile is not null && profile.HasValidRush)
        {
            return profile.RushDrawOrigin(pageWidth, pageHeight);
        }

        return LegacyOrigin(stampHeader);
    }

    public static float ResolveFontSize(string stampHeader, float pageHeight, float fontSizeDelta = 0)
    {
        float baseSize;
        if (PrescriptionPanPositionStore.TryGetRushFontSize(stampHeader, out float stored))
        {
            baseSize = stored;
        }
        else
        {
            PrescriptionStampProfile? profile = PrescriptionStampProfiles.ForPageHeaderIsHigh(stampHeader);
            baseSize = profile is not null && profile.HasValidRush
                ? profile.RushFontSize(pageHeight)
                : 40f;
        }

        return Math.Clamp(baseSize + fontSizeDelta, 14f, 72f);
    }

    private static PointF LegacyOrigin(string stampHeader) =>
        stampHeader switch
        {
            "1" => new PointF(120, 219),
            "0" => new PointF(120, 201),
            "3" => new PointF(306, 22),
            "4" => new PointF(210, 92),
            "5" => new PointF(310, 92),
            "6" => new PointF(375, 30),
            "7" or "8" => new PointF(420, 120),
            PrescriptionSystemCatalog.UnknownCode => new PointF(120, 201),
            _ => new PointF(120, 201),
        };
}
