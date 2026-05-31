using System.Windows;
using System.Windows.Media.Media3D;
using DCMViewer.ViewModels;

namespace DCMViewer.Services;

public static class SectionGeometryService
{
    public static List<SectionSegment2D> BuildSectionSegments2D(
        IEnumerable<LoadedMeshItemViewModel> loadedFiles,
        Point3D planePoint,
        Vector3D normal,
        Vector3D axisX,
        Vector3D axisY)
    {
        var segments = new List<SectionSegment2D>();
        const double epsilon = 1e-7;

        foreach (var item in loadedFiles)
        {
            if (!item.IsVisible || item.MeshSnapshot is null)
            {
                continue;
            }

            var mesh = item.MeshSnapshot;
            var positions = mesh.Positions;
            var indices = mesh.TriangleIndices;
            if (positions.Length == 0 || indices.Length < 3)
            {
                continue;
            }

            for (var i = 0; i + 2 < indices.Length; i += 3)
            {
                var a = positions[indices[i]];
                var b = positions[indices[i + 1]];
                var c = positions[indices[i + 2]];

                var da = Vector3D.DotProduct(a - planePoint, normal);
                var db = Vector3D.DotProduct(b - planePoint, normal);
                var dc = Vector3D.DotProduct(c - planePoint, normal);

                if ((da > epsilon && db > epsilon && dc > epsilon) || (da < -epsilon && db < -epsilon && dc < -epsilon))
                {
                    continue;
                }

                var points = new List<Point3D>(3);
                TryAddIntersectionPoint(a, da, b, db, epsilon, points);
                TryAddIntersectionPoint(b, db, c, dc, epsilon, points);
                TryAddIntersectionPoint(c, dc, a, da, epsilon, points);

                if (points.Count < 2)
                {
                    continue;
                }

                var p0 = points[0];
                var p1 = points[1];
                var a2 = new Point(Vector3D.DotProduct(p0 - planePoint, axisX), Vector3D.DotProduct(p0 - planePoint, axisY));
                var b2 = new Point(Vector3D.DotProduct(p1 - planePoint, axisX), Vector3D.DotProduct(p1 - planePoint, axisY));
                segments.Add(new SectionSegment2D(a2, b2, item.Category));
            }
        }

        return segments;
    }

    private static void TryAddIntersectionPoint(
        Point3D p1,
        double d1,
        Point3D p2,
        double d2,
        double epsilon,
        List<Point3D> result)
    {
        if (Math.Abs(d1) <= epsilon && Math.Abs(d2) <= epsilon)
        {
            return;
        }

        if ((d1 > epsilon && d2 > epsilon) || (d1 < -epsilon && d2 < -epsilon))
        {
            return;
        }

        if (Math.Abs(d1 - d2) <= epsilon)
        {
            return;
        }

        var t = d1 / (d1 - d2);
        t = Math.Clamp(t, 0.0, 1.0);
        var point = p1 + ((p2 - p1) * t);

        foreach (var existing in result)
        {
            if ((existing - point).LengthSquared <= 1e-10)
            {
                return;
            }
        }

        result.Add(point);
    }

    /// <summary>
    /// 2D profile axes used by the section graph (must match <see cref="BuildSectionSegments2D"/> callers).
    /// </summary>
    public static void GetSectionProfileAxes(
        Vector3D planeNormal,
        Vector3D preferredUp,
        out Vector3D axisX,
        out Vector3D axisY)
    {
        var normal = planeNormal;
        if (normal.LengthSquared < 1e-9)
        {
            normal = new Vector3D(0, 0, 1);
        }

        normal.Normalize();

        axisY = preferredUp;
        if (axisY.LengthSquared < 1e-9)
        {
            axisY = new Vector3D(0, 1, 0);
        }

        axisY.Normalize();

        axisX = Vector3D.CrossProduct(normal, axisY);
        if (axisX.LengthSquared < 1e-9)
        {
            axisX = Vector3D.CrossProduct(normal, new Vector3D(1, 0, 0));
        }

        axisX.Normalize();
        axisY = Vector3D.CrossProduct(axisX, normal);
        axisY.Normalize();
    }
}

public readonly record struct SectionSegment2D(Point A, Point B, MeshCategory Category);
