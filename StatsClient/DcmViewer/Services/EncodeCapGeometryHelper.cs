using System.Windows;
using System.Windows.Media.Media3D;
using DCMViewer.ViewModels;
using HelixToolkit.Wpf.SharpDX;
using HelixProjectionCamera = HelixToolkit.Wpf.SharpDX.ProjectionCamera;

namespace DCMViewer.Services;

internal static class EncodeCapGeometryHelper
{
    public static bool TryBuildCapFrame(
        Point3D p0,
        Point3D p1,
        Point3D p2,
        out Point3D center,
        out Vector3D capAxis,
        out Vector3D axisX,
        out Vector3D axisY,
        out double pickRadius)
    {
        center = default;
        capAxis = default;
        axisX = default;
        axisY = default;
        pickRadius = 0;

        var v1 = p1 - p0;
        var v2 = p2 - p0;
        capAxis = Vector3D.CrossProduct(v1, v2);
        if (capAxis.LengthSquared < 1e-12)
        {
            return false;
        }

        capAxis.Normalize();
        center = new Point3D(
            (p0.X + p1.X + p2.X) / 3.0,
            (p0.Y + p1.Y + p2.Y) / 3.0,
            (p0.Z + p1.Z + p2.Z) / 3.0);

        axisX = v1;
        if (axisX.LengthSquared < 1e-12)
        {
            axisX = p2 - p0;
        }

        axisX.Normalize();
        axisY = Vector3D.CrossProduct(capAxis, axisX);
        if (axisY.LengthSquared < 1e-12)
        {
            axisY = Vector3D.CrossProduct(capAxis, new Vector3D(0, 1, 0));
        }

        axisY.Normalize();
        axisX = Vector3D.CrossProduct(axisY, capAxis);
        axisX.Normalize();

        pickRadius = Math.Max(
            (p0 - center).Length,
            Math.Max((p1 - center).Length, (p2 - center).Length));
        pickRadius = Math.Max(pickRadius * 1.35, 1.5);
        return true;
    }

    public static Vector3D GetDiameterSectionNormal(Vector3D capAxis, Vector3D inPlaneReference, double rotationDegrees)
    {
        capAxis.Normalize();
        inPlaneReference.Normalize();
        var perpendicular = Vector3D.CrossProduct(capAxis, inPlaneReference);
        if (perpendicular.LengthSquared < 1e-12)
        {
            perpendicular = Vector3D.CrossProduct(capAxis, new Vector3D(0, 1, 0));
        }

        perpendicular.Normalize();
        return RotateAroundAxis(perpendicular, capAxis, rotationDegrees);
    }

    public static void FrameCameraOnCap(Viewport3DX viewport, Point3D center, Vector3D capAxis, double radius)
    {
        if (viewport.Camera is not HelixProjectionCamera camera)
        {
            return;
        }

        capAxis.Normalize();
        var distance = Math.Max(radius * 5.5, 8.0);

        var up = AxisYFromCapAxis(capAxis);
        camera.UpDirection = up;
        camera.Position = center + (capAxis * distance);
        camera.LookDirection = center - camera.Position;
    }

    public static bool TryMeasureCapDiameterMm(
        IEnumerable<LoadedMeshItemViewModel> loadedFiles,
        Point3D planePoint,
        Vector3D sectionPlaneNormal,
        Vector3D profileUpDirection,
        double roiRadiusMm,
        out double diameterMm,
        out Point sectionStart,
        out Point sectionEnd)
    {
        diameterMm = 0;
        sectionStart = default;
        sectionEnd = default;

        sectionPlaneNormal.Normalize();
        SectionGeometryService.GetSectionProfileAxes(
            sectionPlaneNormal,
            profileUpDirection,
            out var axisX,
            out var axisY);

        var nearbyMeshes = SelectMeshesNearPoint(loadedFiles, planePoint, roiRadiusMm + 4.0);
        var segments = SectionGeometryService.BuildSectionSegments2D(nearbyMeshes, planePoint, sectionPlaneNormal, axisX, axisY);
        if (segments.Count == 0)
        {
            return false;
        }

        var points = new List<Point>(segments.Count * 2);
        foreach (var segment in segments)
        {
            points.Add(segment.A);
            points.Add(segment.B);
        }

        double roi = Math.Max(roiRadiusMm, 2.0);
        var roiPoints = points
            .Where(p => (p.X * p.X) + (p.Y * p.Y) <= roi * roi)
            .ToList();

        if (roiPoints.Count < 4)
        {
            roiPoints = points;
        }

        if (!TryFitCapRimDiameter(roiPoints, roi, out diameterMm, out sectionStart, out sectionEnd))
        {
            return false;
        }

        SnapMeasurementToProfile(points, ref sectionStart, ref sectionEnd);
        diameterMm = Distance(sectionStart, sectionEnd);
        return diameterMm >= 2.5;
    }

    private static void SnapMeasurementToProfile(IReadOnlyList<Point> profilePoints, ref Point start, ref Point end)
    {
        if (profilePoints.Count == 0)
        {
            return;
        }

        start = SnapToNearestProfilePoint(profilePoints, start);
        end = SnapToNearestProfilePoint(profilePoints, end);
    }

    private static Point SnapToNearestProfilePoint(IReadOnlyList<Point> profilePoints, Point target)
    {
        var best = target;
        var bestDistSq = double.MaxValue;
        foreach (var p in profilePoints)
        {
            var dx = p.X - target.X;
            var dy = p.Y - target.Y;
            var distSq = (dx * dx) + (dy * dy);
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = p;
            }
        }

        return best;
    }

    private static double Distance(Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>
    /// Healing-cap diameter = widest horizontal span in the upper profile (vertical section through cap).
    /// </summary>
    private static bool TryFitCapRimDiameter(
        IReadOnlyList<Point> points,
        double roiRadiusMm,
        out double diameterMm,
        out Point sectionStart,
        out Point sectionEnd)
    {
        diameterMm = 0;
        sectionStart = default;
        sectionEnd = default;

        if (points.Count < 4)
        {
            return false;
        }

        var cx = points.Average(p => p.X);
        var cy = points.Average(p => p.Y);
        var roi = Math.Max(roiRadiusMm, 2.0);
        var nearCenter = points
            .Where(p =>
            {
                var dx = p.X - cx;
                var dy = p.Y - cy;
                return (dx * dx) + (dy * dy) <= roi * roi;
            })
            .ToList();

        if (nearCenter.Count < 4)
        {
            nearCenter = points.ToList();
        }

        var yMin = nearCenter.Min(p => p.Y);
        var yMax = nearCenter.Max(p => p.Y);
        var ySpan = yMax - yMin;
        if (ySpan < 0.2)
        {
            return false;
        }

        // Upper profile: cap rim is the longest chord in healing-cap diameter range (rotation-invariant).
        var yCutoff = yMin + (ySpan * 0.45);
        var topPoints = nearCenter.Where(p => p.Y >= yCutoff).ToList();
        if (topPoints.Count < 3)
        {
            topPoints = nearCenter;
        }

        if (TryFindMaxChordDiameter(topPoints, out diameterMm, out sectionStart, out sectionEnd))
        {
            return true;
        }

        if (TryFindMaxChordDiameter(nearCenter, out diameterMm, out sectionStart, out sectionEnd))
        {
            return true;
        }

        if (!TryFitCircleDiameter(nearCenter, out diameterMm, out var fitCx, out var fitCy, out var fitR))
        {
            return false;
        }

        sectionStart = new Point(fitCx - fitR, fitCy);
        sectionEnd = new Point(fitCx + fitR, fitCy);
        return true;
    }

    private static bool TryFindMaxChordDiameter(
        IReadOnlyList<Point> points,
        out double diameterMm,
        out Point sectionStart,
        out Point sectionEnd)
    {
        diameterMm = 0;
        sectionStart = default;
        sectionEnd = default;

        if (points.Count < 2)
        {
            return false;
        }

        double best = 0;
        Point bestA = default;
        Point bestB = default;

        for (var i = 0; i < points.Count; i++)
        {
            var pi = points[i];
            for (var j = i + 1; j < points.Count; j++)
            {
                var pj = points[j];
                var dx = pj.X - pi.X;
                var dy = pj.Y - pi.Y;
                var dist = Math.Sqrt((dx * dx) + (dy * dy));
                if (dist < 2.8 || dist > 10.5 || dist <= best)
                {
                    continue;
                }

                best = dist;
                bestA = pi;
                bestB = pj;
            }
        }

        if (best < 2.5)
        {
            return false;
        }

        diameterMm = best;
        sectionStart = bestA;
        sectionEnd = bestB;
        return true;
    }

    private static List<LoadedMeshItemViewModel> SelectMeshesNearPoint(
        IEnumerable<LoadedMeshItemViewModel> loadedFiles,
        Point3D center,
        double radiusMm)
    {
        var nearby = loadedFiles
            .Where(f => f.IsVisible && !f.IsLoadFailed && f.MeshSnapshot is not null)
            .Where(f => BoundsIntersectsSphere(f.Bounds, center, radiusMm))
            .ToList();

        var scanMeshes = nearby.Where(f => f.Category == MeshCategory.Scan).ToList();
        if (scanMeshes.Count > 0)
        {
            return scanMeshes;
        }

        if (nearby.Count > 0)
        {
            return nearby;
        }

        return loadedFiles
            .Where(f => f.IsVisible && !f.IsLoadFailed && f.MeshSnapshot is not null && f.Category == MeshCategory.Scan)
            .ToList();
    }

    private static bool BoundsIntersectsSphere(Rect3D bounds, Point3D center, double radiusMm)
    {
        if (bounds.IsEmpty)
        {
            return false;
        }

        var closestX = Math.Clamp(center.X, bounds.X, bounds.X + bounds.SizeX);
        var closestY = Math.Clamp(center.Y, bounds.Y, bounds.Y + bounds.SizeY);
        var closestZ = Math.Clamp(center.Z, bounds.Z, bounds.Z + bounds.SizeZ);
        var dx = center.X - closestX;
        var dy = center.Y - closestY;
        var dz = center.Z - closestZ;
        return (dx * dx) + (dy * dy) + (dz * dz) <= radiusMm * radiusMm;
    }

    private static bool TryFitCircleDiameter(
        IReadOnlyList<Point> points,
        out double diameterMm,
        out double centerX,
        out double centerY,
        out double radiusMm)
    {
        diameterMm = 0;
        centerX = 0;
        centerY = 0;
        radiusMm = 0;

        if (points.Count < 4)
        {
            return false;
        }

        var avgX = points.Average(p => p.X);
        var avgY = points.Average(p => p.Y);
        centerX = avgX;
        centerY = avgY;

        var radii = points
            .Select(p =>
            {
                var dx = p.X - avgX;
                var dy = p.Y - avgY;
                return Math.Sqrt((dx * dx) + (dy * dy));
            })
            .Where(r => r > 0.2 && r < 6.0)
            .OrderBy(r => r)
            .ToList();

        if (radii.Count < 4)
        {
            return false;
        }

        int mid = radii.Count / 2;
        radiusMm = radii.Count % 2 == 1
            ? radii[mid]
            : (radii[mid - 1] + radii[mid]) * 0.5;

        if (radiusMm < 0.35 || radiusMm > 5.5)
        {
            return false;
        }

        diameterMm = radiusMm * 2.0;
        return true;
    }

    private static Vector3D RotateAroundAxis(Vector3D vector, Vector3D axis, double angleDegrees)
    {
        axis.Normalize();
        vector.Normalize();
        var radians = angleDegrees * Math.PI / 180.0;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var dot = Vector3D.DotProduct(axis, vector);
        var cross = Vector3D.CrossProduct(axis, vector);
        return (vector * cos) + (cross * sin) + (axis * (dot * (1.0 - cos)));
    }

    private static Vector3D AxisYFromCapAxis(Vector3D capAxis)
    {
        var up = new Vector3D(0, 1, 0);
        var right = Vector3D.CrossProduct(capAxis, up);
        if (right.LengthSquared < 1e-9)
        {
            up = new Vector3D(0, 0, 1);
            right = Vector3D.CrossProduct(capAxis, up);
        }

        right.Normalize();
        var viewUp = Vector3D.CrossProduct(right, capAxis);
        viewUp.Normalize();
        return viewUp;
    }
}
