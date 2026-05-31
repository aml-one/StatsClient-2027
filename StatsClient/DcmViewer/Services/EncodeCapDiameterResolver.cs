namespace DCMViewer.Services;

/// <summary>
/// Combines three diameter measurements (120° apart) into one platform value.
/// </summary>
internal static class EncodeCapDiameterResolver
{
    private static readonly double[] CanonicalPlatformDiametersMm =
    [
        3.4, 3.5, 4.1, 4.5, 5.0, 5.7, 6.0
    ];

    private const double PairCloseToleranceMm = 0.45;
    private const double AllFarMaxSpreadMm = 1.2;

    public static double Resolve(IReadOnlyList<double> measurements, out string summary)
    {
        summary = string.Empty;
        if (measurements.Count == 0)
        {
            return 0;
        }

        if (measurements.Count == 1)
        {
            var single = SnapToCanonical(measurements[0]);
            summary = $"Single cut Ø {measurements[0]:F2} mm → {single:F2} mm";
            return single;
        }

        var values = measurements.Where(v => v > 0.5 && v < 10).ToList();
        if (values.Count == 0)
        {
            return 0;
        }

        if (values.Count == 1)
        {
            var single = SnapToCanonical(values[0]);
            summary = $"Single valid cut Ø {values[0]:F2} mm → {single:F2} mm";
            return single;
        }

        if (values.Count == 2)
        {
            var avg = (values[0] + values[1]) * 0.5;
            var resolved = SnapToCanonical(avg);
            summary = $"Two cuts ({values[0]:F2}, {values[1]:F2} mm) → {resolved:F2} mm";
            return resolved;
        }

        double d0 = values[0];
        double d1 = values[1];
        double d2 = values[2];

        bool close01 = Math.Abs(d0 - d1) <= PairCloseToleranceMm;
        bool close12 = Math.Abs(d1 - d2) <= PairCloseToleranceMm;
        bool close02 = Math.Abs(d0 - d2) <= PairCloseToleranceMm;

        if (close01 && close12)
        {
            var avg = (d0 + d1 + d2) / 3.0;
            var resolved = SnapToCanonical(avg);
            summary = $"All 3 cuts agree ({d0:F2}, {d1:F2}, {d2:F2} mm) → {resolved:F2} mm";
            return resolved;
        }

        if (close01)
        {
            var avg = (d0 + d1) * 0.5;
            var resolved = SnapToCanonical(avg);
            summary = $"Cuts 1+2 agree ({d0:F2}, {d1:F2} mm), ignored {d2:F2} → {resolved:F2} mm";
            return resolved;
        }

        if (close12)
        {
            var avg = (d1 + d2) * 0.5;
            var resolved = SnapToCanonical(avg);
            summary = $"Cuts 2+3 agree ({d1:F2}, {d2:F2} mm), ignored {d0:F2} → {resolved:F2} mm";
            return resolved;
        }

        if (close02)
        {
            var avg = (d0 + d2) * 0.5;
            var resolved = SnapToCanonical(avg);
            summary = $"Cuts 1+3 agree ({d0:F2}, {d2:F2} mm), ignored {d1:F2} → {resolved:F2} mm";
            return resolved;
        }

        var spread = Math.Max(d0, Math.Max(d1, d2)) - Math.Min(d0, Math.Min(d1, d2));
        var pick = PickClosestCanonical(d0, d1, d2);
        summary = spread > AllFarMaxSpreadMm
            ? $"Cuts disagree ({d0:F2}, {d1:F2}, {d2:F2} mm); using closest platform {pick:F2} mm"
            : $"Cuts varied ({d0:F2}, {d1:F2}, {d2:F2} mm); using closest platform {pick:F2} mm";
        return pick;
    }

    private static double PickClosestCanonical(double d0, double d1, double d2)
    {
        var avg = (d0 + d1 + d2) / 3.0;
        return SnapToCanonical(avg);
    }

    private static double SnapToCanonical(double measuredMm)
    {
        double best = CanonicalPlatformDiametersMm[0];
        double bestDelta = Math.Abs(measuredMm - best);
        foreach (double candidate in CanonicalPlatformDiametersMm)
        {
            double delta = Math.Abs(measuredMm - candidate);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = candidate;
            }
        }

        return best;
    }
}
