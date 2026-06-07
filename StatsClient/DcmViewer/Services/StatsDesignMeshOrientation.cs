using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>
/// Rotates library meshes so their insertion direction aligns with the prep insertion axis.
/// </summary>
internal static class StatsDesignMeshOrientation
{
    public static MeshSnapshot RotateToAlign(MeshSnapshot mesh, Vector3D sourceAxis, Vector3D targetAxis)
    {
        sourceAxis.Normalize();
        targetAxis.Normalize();

        if (sourceAxis.LengthSquared < 1e-12 || targetAxis.LengthSquared < 1e-12)
        {
            return mesh;
        }

        var dot = Math.Clamp(Vector3D.DotProduct(sourceAxis, targetAxis), -1.0, 1.0);
        if (dot > 0.9995)
        {
            return mesh;
        }

        Vector3D rotationAxis;
        double angle;
        if (dot < -0.9995)
        {
            rotationAxis = Math.Abs(sourceAxis.X) < 0.9
                ? Vector3D.CrossProduct(sourceAxis, new Vector3D(1, 0, 0))
                : Vector3D.CrossProduct(sourceAxis, new Vector3D(0, 1, 0));
            rotationAxis.Normalize();
            angle = Math.PI;
        }
        else
        {
            rotationAxis = Vector3D.CrossProduct(sourceAxis, targetAxis);
            if (rotationAxis.LengthSquared < 1e-12)
            {
                return mesh;
            }

            rotationAxis.Normalize();
            angle = Math.Acos(dot);
        }

        var rotated = new Point3D[mesh.Positions.Length];
        for (var i = 0; i < mesh.Positions.Length; i++)
        {
            rotated[i] = RotatePoint(mesh.Positions[i], rotationAxis, angle);
        }

        return new MeshSnapshot(rotated, mesh.TriangleIndices);
    }

    /// <summary>
    /// Occlusal direction estimated from low/high percentiles along mesh height (library local space).
    /// </summary>
    public static Vector3D EstimateLibraryInsertionAxis(MeshSnapshot mesh)
    {
        if (mesh.Positions.Length == 0)
        {
            return new Vector3D(0, 0, 1);
        }

        var draft = new Vector3D(0, 0, 1);
        var projections = mesh.Positions
            .Select(p => Vector3D.DotProduct(new Vector3D(p.X, p.Y, p.Z), draft))
            .OrderBy(v => v)
            .ToList();

        var lowCut = projections[(int)(projections.Count * 0.08)];
        var highCut = projections[(int)(projections.Count * 0.92)];

        var bottom = new Vector3D();
        var top = new Vector3D();
        var bottomCount = 0;
        var topCount = 0;

        foreach (var p in mesh.Positions)
        {
            var t = Vector3D.DotProduct(new Vector3D(p.X, p.Y, p.Z), draft);
            if (t <= lowCut)
            {
                bottom += new Vector3D(p.X, p.Y, p.Z);
                bottomCount++;
            }

            if (t >= highCut)
            {
                top += new Vector3D(p.X, p.Y, p.Z);
                topCount++;
            }
        }

        if (bottomCount == 0 || topCount == 0)
        {
            return draft;
        }

        bottom *= 1.0 / bottomCount;
        top *= 1.0 / topCount;
        var axis = top - bottom;
        if (axis.LengthSquared < 1e-12)
        {
            return draft;
        }

        axis.Normalize();
        return axis;
    }

    public static MeshSnapshot OrientLibraryToInsertionAxis(MeshSnapshot mesh, Vector3D targetInsertionAxis)
    {
        var source = EstimateLibraryInsertionAxis(mesh);
        return RotateToAlign(mesh, source, targetInsertionAxis);
    }

    private static Point3D RotatePoint(Point3D point, Vector3D axis, double angleRadians)
    {
        var cos = Math.Cos(angleRadians);
        var sin = Math.Sin(angleRadians);
        var ux = axis.X;
        var uy = axis.Y;
        var uz = axis.Z;

        var x = point.X;
        var y = point.Y;
        var z = point.Z;

        var dot = (ux * x) + (uy * y) + (uz * z);
        var crossX = (uy * z) - (uz * y);
        var crossY = (uz * x) - (ux * z);
        var crossZ = (ux * y) - (uy * x);

        return new Point3D(
            (x * cos) + (crossX * sin) + (ux * dot * (1.0 - cos)),
            (y * cos) + (crossY * sin) + (uy * dot * (1.0 - cos)),
            (z * cos) + (crossZ * sin) + (uz * dot * (1.0 - cos)));
    }
}
