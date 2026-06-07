using System.Numerics;
using System.Windows.Media.Media3D;
using HelixToolkit.SharpDX;

namespace DCMViewer.Services;

/// <summary>
/// Builds offset margin polylines so the line stays readable when it cuts through the prep scan.
/// </summary>
internal static class StatsDesignMarginLineVisualBuilder
{
    private const double RibbonOffsetMm = 0.22;
    private const double CoreLiftMm = 0.38;

    public sealed record RibbonGeometries(
        LineGeometry3D? HaloInner,
        LineGeometry3D? HaloOuter,
        LineGeometry3D? Core);

    /// <summary>Lightweight polyline for interactive margin placement (no per-point mesh queries).</summary>
    public static LineGeometry3D? BuildFastCore(IReadOnlyList<Point3D> polyline)
    {
        if (polyline.Count < 2)
        {
            return null;
        }

        var points = new List<Vector3>(polyline.Count);
        foreach (var point in polyline)
        {
            points.Add(new Vector3((float)point.X, (float)point.Y, (float)point.Z));
        }

        return SharpDxMeshFactory.CreateLineGeometry(points);
    }

    public static RibbonGeometries Build(
        IReadOnlyList<Point3D> polyline,
        IReadOnlyList<MeshSnapshot> prepMeshes,
        Vector3D insertionAxis,
        IReadOnlyList<StatsDesignMeshSpatialIndex>? cachedIndices = null)
    {
        if (polyline.Count < 2)
        {
            return new RibbonGeometries(null, null, null);
        }

        var indices = cachedIndices;
        if (indices is null && prepMeshes.Count > 0)
        {
            indices = prepMeshes.Select(mesh => new StatsDesignMeshSpatialIndex(mesh)).ToList();
        }

        var haloInner = new List<Vector3>(polyline.Count);
        var haloOuter = new List<Vector3>(polyline.Count);
        var core = new List<Vector3>(polyline.Count);

        for (var i = 0; i < polyline.Count; i++)
        {
            var point = polyline[i];
            var normal = ResolveOutwardNormal(point, polyline, i, prepMeshes, indices, insertionAxis);
            haloInner.Add(ToVector3(OffsetAlongNormal(point, normal, -RibbonOffsetMm)));
            haloOuter.Add(ToVector3(OffsetAlongNormal(point, normal, RibbonOffsetMm)));
            core.Add(ToVector3(OffsetAlongNormal(point, normal, CoreLiftMm)));
        }

        return new RibbonGeometries(
            SharpDxMeshFactory.CreateLineGeometry(haloInner),
            SharpDxMeshFactory.CreateLineGeometry(haloOuter),
            SharpDxMeshFactory.CreateLineGeometry(core));
    }

    private static Vector3D ResolveOutwardNormal(
        Point3D point,
        IReadOnlyList<Point3D> polyline,
        int index,
        IReadOnlyList<MeshSnapshot> prepMeshes,
        IReadOnlyList<StatsDesignMeshSpatialIndex>? indices,
        Vector3D insertionAxis)
    {
        Vector3D normal;
        Point3D anchor;

        if (prepMeshes.Count > 0)
        {
            var closest = StatsDesignMeshProximity.ClosestPointAndNormalOnMeshes(point, prepMeshes, indices);
            normal = closest.Normal;
            anchor = closest.Point;
        }
        else
        {
            normal = EstimatePolylineNormal(polyline, index, insertionAxis);
            anchor = point;
        }

        var outward = point - anchor;
        if (outward.LengthSquared > 1e-10)
        {
            outward.Normalize();
            if (Vector3D.DotProduct(normal, outward) < 0)
            {
                normal = -normal;
            }
        }
        else if (Vector3D.DotProduct(normal, insertionAxis) < 0)
        {
            normal = -normal;
        }

        if (normal.LengthSquared < 1e-12)
        {
            normal = EstimatePolylineNormal(polyline, index, insertionAxis);
        }
        else
        {
            normal.Normalize();
        }

        return normal;
    }

    private static Vector3D EstimatePolylineNormal(
        IReadOnlyList<Point3D> polyline,
        int index,
        Vector3D insertionAxis)
    {
        var tangent = ResolveTangent(polyline, index);
        if (tangent.LengthSquared < 1e-12)
        {
            return new Vector3D(0, 0, 1);
        }

        tangent.Normalize();
        var axis = insertionAxis;
        if (axis.LengthSquared < 1e-12)
        {
            axis = new Vector3D(0, 0, 1);
        }
        else
        {
            axis.Normalize();
        }

        var normal = Vector3D.CrossProduct(tangent, axis);
        if (normal.LengthSquared < 1e-12)
        {
            normal = Vector3D.CrossProduct(tangent, new Vector3D(0, 1, 0));
        }

        if (normal.LengthSquared < 1e-12)
        {
            return new Vector3D(0, 0, 1);
        }

        normal.Normalize();
        return normal;
    }

    private static Vector3D ResolveTangent(IReadOnlyList<Point3D> polyline, int index)
    {
        if (polyline.Count < 2)
        {
            return new Vector3D(1, 0, 0);
        }

        if (index <= 0)
        {
            return polyline[1] - polyline[0];
        }

        if (index >= polyline.Count - 1)
        {
            return polyline[^1] - polyline[^2];
        }

        return polyline[index + 1] - polyline[index - 1];
    }

    private static Point3D OffsetAlongNormal(Point3D point, Vector3D normal, double offsetMm) =>
        new(
            point.X + (normal.X * offsetMm),
            point.Y + (normal.Y * offsetMm),
            point.Z + (normal.Z * offsetMm));

    private static Vector3 ToVector3(Point3D point) =>
        new((float)point.X, (float)point.Y, (float)point.Z);
}
