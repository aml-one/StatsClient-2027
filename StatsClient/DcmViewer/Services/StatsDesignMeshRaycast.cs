using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

internal static class StatsDesignMeshRaycast
{
    internal readonly struct RayHit
    {
        public RayHit(Point3D point, double distanceMm, int triangleIndex)
        {
            Point = point;
            DistanceMm = distanceMm;
            TriangleIndex = triangleIndex;
        }

        public Point3D Point { get; }
        public double DistanceMm { get; }
        public int TriangleIndex { get; }
    }

    public static bool TryIntersectRayTriangle(
        Point3D origin,
        Vector3D direction,
        Point3D v0,
        Point3D v1,
        Point3D v2,
        double minDistanceMm,
        double maxDistanceMm,
        out double distanceAlongRay,
        out Point3D hitPoint)
    {
        distanceAlongRay = 0;
        hitPoint = default;

        var edge1 = v1 - v0;
        var edge2 = v2 - v0;
        var pVec = Vector3D.CrossProduct(direction, edge2);
        var det = Vector3D.DotProduct(edge1, pVec);
        if (Math.Abs(det) < 1e-12)
        {
            return false;
        }

        var invDet = 1.0 / det;
        var tVec = origin - v0;
        var u = Vector3D.DotProduct(tVec, pVec) * invDet;
        if (u < 0.0 || u > 1.0)
        {
            return false;
        }

        var qVec = Vector3D.CrossProduct(tVec, edge1);
        var v = Vector3D.DotProduct(direction, qVec) * invDet;
        if (v < 0.0 || u + v > 1.0)
        {
            return false;
        }

        var t = Vector3D.DotProduct(edge2, qVec) * invDet;
        if (t < minDistanceMm || t > maxDistanceMm)
        {
            return false;
        }

        distanceAlongRay = t;
        hitPoint = origin + (direction * t);
        return true;
    }

    public static Point3D ClosestPointOnTriangle(Point3D point, Point3D a, Point3D b, Point3D c)
    {
        var ab = b - a;
        var ac = c - a;
        var ap = point - a;

        var d1 = Vector3D.DotProduct(ab, ap);
        var d2 = Vector3D.DotProduct(ac, ap);
        if (d1 <= 0 && d2 <= 0)
        {
            return a;
        }

        var bp = point - b;
        var d3 = Vector3D.DotProduct(ab, bp);
        var d4 = Vector3D.DotProduct(ac, bp);
        if (d3 >= 0 && d4 <= d3)
        {
            return b;
        }

        var vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0)
        {
            var v = d1 / (d1 - d3);
            return a + (ab * v);
        }

        var cp = point - c;
        var d5 = Vector3D.DotProduct(ab, cp);
        var d6 = Vector3D.DotProduct(ac, cp);
        if (d6 >= 0 && d5 <= d6)
        {
            return c;
        }

        var vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0)
        {
            var w = d2 / (d2 - d6);
            return a + (ac * w);
        }

        var va = d3 * d6 - d5 * d4;
        if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0)
        {
            var w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            return b + ((c - b) * w);
        }

        var denom = 1.0 / (va + vb + vc);
        var vab = vb * denom;
        var vac = vc * denom;
        return a + (ab * vab) + (ac * vac);
    }
}
