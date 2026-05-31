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

    public static SharpDxMeshGeometry3D CreateSphereGeometry(Point3D center, double radius, int slices = 12, int stacks = 12)
    {
        var positions = new Vector3Collection();
        var indices = new IntCollection();

        for (var stack = 0; stack <= stacks; stack++)
        {
            var phi = Math.PI * stack / stacks;
            var sinPhi = Math.Sin(phi);
            var cosPhi = Math.Cos(phi);

            for (var slice = 0; slice <= slices; slice++)
            {
                var theta = Math.PI * 2.0 * slice / slices;
                var x = center.X + (radius * sinPhi * Math.Cos(theta));
                var y = center.Y + (radius * cosPhi);
                var z = center.Z + (radius * sinPhi * Math.Sin(theta));
                positions.Add(new Vector3((float)x, (float)y, (float)z));
            }
        }

        var rowSize = slices + 1;
        for (var stack = 0; stack < stacks; stack++)
        {
            for (var slice = 0; slice < slices; slice++)
            {
                var a = (stack * rowSize) + slice;
                var b = a + rowSize;
                indices.Add(a);
                indices.Add(b);
                indices.Add(a + 1);
                indices.Add(a + 1);
                indices.Add(b);
                indices.Add(b + 1);
            }
        }

        var normals = new Vector3Collection(MeshGeometryHelper.CalculateNormals(positions, indices));
        return new SharpDxMeshGeometry3D
        {
            Positions = positions,
            Indices = indices,
            Normals = normals
        };
    }

    public static LineGeometry3D CreateClosedLineGeometry(IReadOnlyList<Point3D> points)
    {
        if (points.Count == 0)
        {
            return new LineGeometry3D();
        }

        var vectors = new Vector3[points.Count];
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            vectors[index] = new Vector3((float)point.X, (float)point.Y, (float)point.Z);
        }

        return CreateLineGeometry(vectors);
    }

    public static LineGeometry3D CreateCircleLineGeometry(
        Point3D center,
        Vector3D normal,
        double radius,
        int segments = 48,
        double normalOffset = 0.05)
    {
        var axis = normal;
        if (axis.LengthSquared < 1e-12)
        {
            axis = new Vector3D(0, 0, 1);
        }

        axis.Normalize();

        var axisX = Vector3D.CrossProduct(axis, Math.Abs(axis.Z) < 0.9 ? new Vector3D(0, 0, 1) : new Vector3D(0, 1, 0));
        if (axisX.LengthSquared < 1e-12)
        {
            axisX = Vector3D.CrossProduct(axis, new Vector3D(1, 0, 0));
        }

        axisX.Normalize();
        var axisY = Vector3D.CrossProduct(axisX, axis);
        axisY.Normalize();

        var liftedCenter = center + (axis * normalOffset);
        var clampedRadius = Math.Max(radius, 0.1);
        var points = new Vector3[segments + 1];
        for (var index = 0; index <= segments; index++)
        {
            var angle = (Math.PI * 2.0 * index) / segments;
            var point = liftedCenter +
                        (axisX * (Math.Cos(angle) * clampedRadius)) +
                        (axisY * (Math.Sin(angle) * clampedRadius));
            points[index] = new Vector3((float)point.X, (float)point.Y, (float)point.Z);
        }

        return CreateLineGeometry(points);
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
