using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>
/// Closed margin loop with a local projection plane for inside/outside tests and distance-to-margin.
/// </summary>
internal sealed class StatsDesignMarginGeometry
{
    private readonly Point3D[] _loop;
    private readonly (double U, double V)[] _loopUv;

    public Point3D Centroid { get; }
    public Vector3D PlaneNormal { get; }
    public Vector3D AxisU { get; }
    public Vector3D AxisV { get; }

    private StatsDesignMarginGeometry(
        IReadOnlyList<Point3D> loop,
        Point3D centroid,
        Vector3D planeNormal,
        Vector3D axisU,
        Vector3D axisV)
    {
        _loop = loop.ToArray();
        Centroid = centroid;
        PlaneNormal = planeNormal;
        AxisU = axisU;
        AxisV = axisV;

        _loopUv = new (double U, double V)[_loop.Length];
        for (var i = 0; i < _loop.Length; i++)
        {
            _loopUv[i] = ToUv(_loop[i]);
        }
    }

    public static StatsDesignMarginGeometry Create(
        IReadOnlyList<Point3D> marginLoop,
        Vector3D? insertionAxis = null)
    {
        if (marginLoop.Count < 3)
        {
            throw new InvalidOperationException("A closed margin requires at least three points.");
        }

        var centroid = new Point3D(
            marginLoop.Average(p => p.X),
            marginLoop.Average(p => p.Y),
            marginLoop.Average(p => p.Z));

        var normalAccumulator = insertionAxis ?? StatsDesignInsertionAxis.Calculate(marginLoop);
        if (normalAccumulator.LengthSquared < 1e-12)
        {
            normalAccumulator = new Vector3D(0, 0, 1);
        }
        else
        {
            normalAccumulator.Normalize();
        }

        var axisU = Vector3D.CrossProduct(normalAccumulator, new Vector3D(0, 0, 1));
        if (axisU.LengthSquared < 1e-10)
        {
            axisU = Vector3D.CrossProduct(normalAccumulator, new Vector3D(0, 1, 0));
        }

        axisU.Normalize();
        var axisV = Vector3D.CrossProduct(normalAccumulator, axisU);
        axisV.Normalize();

        return new StatsDesignMarginGeometry(marginLoop, centroid, normalAccumulator, axisU, axisV);
    }

    public (double U, double V) ToUv(Point3D point)
    {
        var delta = point - Centroid;
        return (
            Vector3D.DotProduct(delta, AxisU),
            Vector3D.DotProduct(delta, AxisV));
    }

    public bool ContainsUv(double u, double v)
    {
        var inside = false;
        for (int i = 0, j = _loopUv.Length - 1; i < _loopUv.Length; j = i++)
        {
            var pi = _loopUv[i];
            var pj = _loopUv[j];
            var intersects = (pi.V > v) != (pj.V > v) &&
                             u < (((pj.U - pi.U) * (v - pi.V)) / (pj.V - pi.V + 1e-12)) + pi.U;
            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    public bool ContainsPoint(Point3D point) => ContainsUv(ToUv(point).U, ToUv(point).V);

    /// <summary>Shortest distance from point to the margin polyline (mm).</summary>
    public double DistanceToLoopMm(Point3D point)
    {
        var best = double.PositiveInfinity;
        for (var i = 0; i < _loop.Length; i++)
        {
            var j = (i + 1) % _loop.Length;
            var closest = ClosestPointOnSegment(point, _loop[i], _loop[j]);
            var dx = point.X - closest.X;
            var dy = point.Y - closest.Y;
            var dz = point.Z - closest.Z;
            best = Math.Min(best, Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz)));
        }

        return best;
    }

    public double NormalizedDistanceFromMargin(Point3D point, double transitionBandMm)
    {
        var band = Math.Max(transitionBandMm, 0.05);
        var dist = DistanceToLoopMm(point);
        return SmoothStep(0.0, band, dist);
    }

    public static double SmoothStep(double edge0, double edge1, double x)
    {
        if (edge1 <= edge0)
        {
            return x <= edge0 ? 0.0 : 1.0;
        }

        var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0);
        return t * t * (3.0 - (2.0 * t));
    }

    private static Point3D ClosestPointOnSegment(Point3D point, Point3D a, Point3D b)
    {
        var ab = b - a;
        var lengthSq = ab.LengthSquared;
        if (lengthSq < 1e-12)
        {
            return a;
        }

        var t = Math.Clamp(Vector3D.DotProduct(point - a, ab) / lengthSq, 0.0, 1.0);
        return a + (ab * t);
    }
}
