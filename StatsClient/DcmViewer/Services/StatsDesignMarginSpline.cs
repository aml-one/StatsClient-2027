using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>
/// Smooths user-clicked margin control points into a dense closed loop (Catmull-Rom).
/// </summary>
internal static class StatsDesignMarginSpline
{
    private const int DefaultSamplesPerSegment = 20;

    /// <summary>
    /// Dense closed loop for shell generation, distance tests, and gap bridges.
    /// </summary>
    public static List<Point3D> DensifyClosed(
        IReadOnlyList<Point3D> controlPoints,
        double targetSpacingMm = 0.3,
        int samplesPerSegment = DefaultSamplesPerSegment)
    {
        if (controlPoints.Count < 2)
        {
            return controlPoints.ToList();
        }

        if (controlPoints.Count == 2)
        {
            return ResampleSegment(controlPoints[0], controlPoints[1], samplesPerSegment);
        }

        var segments = controlPoints.Count;
        var dense = new List<Point3D>();
        for (var i = 0; i < segments; i++)
        {
            var p0 = controlPoints[WrapIndex(i - 1, segments)];
            var p1 = controlPoints[i];
            var p2 = controlPoints[WrapIndex(i + 1, segments)];
            var p3 = controlPoints[WrapIndex(i + 2, segments)];

            var edgeLength = (p2 - p1).Length;
            var sampleCount = Math.Max(
                2,
                Math.Min(samplesPerSegment, (int)Math.Ceiling(edgeLength / Math.Max(targetSpacingMm, 0.05))));

            for (var s = 0; s < sampleCount; s++)
            {
                var t = s / (double)sampleCount;
                dense.Add(EvaluateCatmullRom(p0, p1, p2, p3, t));
            }
        }

        return dense;
    }

    /// <summary>
    /// Open polyline for in-progress margin visualization (not yet closed).
    /// </summary>
    public static List<Point3D> DensifyOpen(
        IReadOnlyList<Point3D> controlPoints,
        int samplesPerSegment = DefaultSamplesPerSegment)
    {
        if (controlPoints.Count < 2)
        {
            return controlPoints.ToList();
        }

        var dense = new List<Point3D> { controlPoints[0] };
        for (var i = 0; i < controlPoints.Count - 1; i++)
        {
            var p0 = i == 0 ? controlPoints[0] : controlPoints[i - 1];
            var p1 = controlPoints[i];
            var p2 = controlPoints[i + 1];
            var p3 = i + 2 < controlPoints.Count ? controlPoints[i + 2] : p2;

            for (var s = 1; s <= samplesPerSegment; s++)
            {
                var t = s / (double)samplesPerSegment;
                dense.Add(EvaluateCatmullRom(p0, p1, p2, p3, t));
            }
        }

        return dense;
    }

    private static List<Point3D> ResampleSegment(Point3D a, Point3D b, int count)
    {
        var list = new List<Point3D>(count);
        for (var i = 0; i < count; i++)
        {
            var t = i / (double)count;
            list.Add(a + ((b - a) * t));
        }

        return list;
    }

    private static Point3D EvaluateCatmullRom(Point3D p0, Point3D p1, Point3D p2, Point3D p3, double t)
    {
        var t2 = t * t;
        var t3 = t2 * t;

        static double Blend(double a, double b, double c, double d, double u, double u2, double u3) =>
            (0.5 * ((2.0 * b) + (-a + c) * u + (2.0 * a - 5.0 * b + 4.0 * c - d) * u2 + (-a + 3.0 * b - 3.0 * c + d) * u3));

        return new Point3D(
            Blend(p0.X, p1.X, p2.X, p3.X, t, t2, t3),
            Blend(p0.Y, p1.Y, p2.Y, p3.Y, t, t2, t3),
            Blend(p0.Z, p1.Z, p2.Z, p3.Z, t, t2, t3));
    }

    private static int WrapIndex(int index, int count)
    {
        var wrapped = index % count;
        return wrapped < 0 ? wrapped + count : wrapped;
    }
}
