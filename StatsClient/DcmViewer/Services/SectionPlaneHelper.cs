using System.Numerics;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf.SharpDX;

namespace DCMViewer.Services;

internal static class SectionPlaneHelper
{
    public static void ApplyCrossSection(CrossSectionMeshGeometryModel3D model, Point3D planePoint, Vector3D planeNormal, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!enabled)
        {
            model.EnablePlane1 = false;
            return;
        }

        model.Plane1 = CreatePlaneParameters(planePoint, planeNormal);
        model.EnablePlane1 = true;
    }

    public static Plane CreatePlaneParameters(Point3D planePoint, Vector3D planeNormal)
    {
        var normal = planeNormal;
        if (normal.LengthSquared < 1e-9)
        {
            normal = new Vector3D(0, 0, 1);
        }

        normal.Normalize();
        var distance = -(normal.X * planePoint.X + normal.Y * planePoint.Y + normal.Z * planePoint.Z);
        return new Plane(new Vector3((float)normal.X, (float)normal.Y, (float)normal.Z), (float)distance);
    }
}
