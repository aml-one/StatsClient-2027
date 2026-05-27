using HelixToolkit;
using HelixToolkit.Geometry;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using System.Numerics;
using System.Windows.Media.Media3D;
using SharpDxMeshGeometry3D = HelixToolkit.SharpDX.MeshGeometry3D;

namespace DCMViewer.Services;

internal static class SharpDxMeshFactory
{
    public static SharpDxMeshGeometry3D CreateGeometry(MeshSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var positions = new Vector3Collection(snapshot.Positions.Length);
        foreach (var point in snapshot.Positions)
        {
            positions.Add(new Vector3((float)point.X, (float)point.Y, (float)point.Z));
        }

        var indices = new IntCollection(snapshot.TriangleIndices.Length);
        foreach (var index in snapshot.TriangleIndices)
        {
            indices.Add(index);
        }

        var normals = new Vector3Collection(MeshGeometryHelper.CalculateNormals(positions, indices));

        return new SharpDxMeshGeometry3D
        {
            Positions = positions,
            Indices = indices,
            Normals = normals
        };
    }

    public static LineGeometry3D CreateLineGeometry(IReadOnlyList<Vector3> points)
    {
        var positions = new Vector3Collection(points.Count);
        foreach (var point in points)
        {
            positions.Add(point);
        }

        var indices = new IntCollection(Math.Max(0, points.Count - 1) * 2);
        for (var i = 0; i + 1 < points.Count; i++)
        {
            indices.Add(i);
            indices.Add(i + 1);
        }

        return new LineGeometry3D
        {
            Positions = positions,
            Indices = indices
        };
    }

    public static SharpDxMeshGeometry3D CreateDiskGeometry(
        Point3D center,
        Vector3D axisX,
        Vector3D axisY,
        double radius,
        int segments)
    {
        var positions = new Vector3Collection(segments + 1);
        positions.Add(new Vector3((float)center.X, (float)center.Y, (float)center.Z));

        for (var i = 0; i < segments; i++)
        {
            var angle = (Math.PI * 2.0 * i) / segments;
            var point = center + (axisX * (Math.Cos(angle) * radius)) + (axisY * (Math.Sin(angle) * radius));
            positions.Add(new Vector3((float)point.X, (float)point.Y, (float)point.Z));
        }

        var indices = new IntCollection(segments * 3);
        for (var i = 0; i < segments; i++)
        {
            indices.Add(0);
            indices.Add(1 + i);
            indices.Add(1 + ((i + 1) % segments));
        }

        var normals = new Vector3Collection(MeshGeometryHelper.CalculateNormals(positions, indices));
        return new SharpDxMeshGeometry3D
        {
            Positions = positions,
            Indices = indices,
            Normals = normals
        };
    }

    public static LineGeometry3D CreateSegmentLineGeometry(IReadOnlyList<Point3D> segmentEndPoints)
    {
        var positions = new Vector3Collection(segmentEndPoints.Count);
        foreach (var point in segmentEndPoints)
        {
            positions.Add(new Vector3((float)point.X, (float)point.Y, (float)point.Z));
        }

        var indices = new IntCollection(segmentEndPoints.Count);
        for (var i = 0; i < segmentEndPoints.Count; i++)
        {
            indices.Add(i);
        }

        return new LineGeometry3D
        {
            Positions = positions,
            Indices = indices
        };
    }
}
