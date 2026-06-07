using System.IO;
using System.Windows.Media.Media3D;
using System.Xml.Linq;

namespace DCMViewer.Services;

/// <summary>
/// Reads the library tooth "Margin" spline from 3Shape Smile Library DCM files.
/// </summary>
internal static class StatsDesignLibraryMarginReader
{
    private const int ControlPointStrideBytes = 16;
    private const double MinPlausibleCoordinate = -250.0;
    private const double MaxPlausibleCoordinate = 250.0;

    public static IReadOnlyList<Point3D> ReadMarginLoop(string dcmFilePath)
    {
        if (!File.Exists(dcmFilePath))
        {
            throw new FileNotFoundException("Library DCM was not found.", dcmFilePath);
        }

        var document = XDocument.Load(dcmFilePath, LoadOptions.None);
        var marginObject = document
            .Descendants()
            .FirstOrDefault(IsMarginSplineObject);

        if (marginObject is null)
        {
            throw new InvalidOperationException(
                $"No Margin spline was found in {Path.GetFileName(dcmFilePath)}.");
        }

        var packed = marginObject
            .Descendants()
            .FirstOrDefault(element =>
                element.Name.LocalName.Equals("ControlPointsPacked", StringComparison.OrdinalIgnoreCase));

        if (packed is null || string.IsNullOrWhiteSpace(packed.Value))
        {
            throw new InvalidOperationException(
                $"Margin spline in {Path.GetFileName(dcmFilePath)} has no control points.");
        }

        var controlPoints = DecodeControlPointsPacked(Convert.FromBase64String(packed.Value.Trim()));
        if (controlPoints.Count < 3)
        {
            throw new InvalidOperationException(
                $"Margin spline in {Path.GetFileName(dcmFilePath)} has too few control points.");
        }

        return DensifyClosedLoop(controlPoints, targetSpacingMm: 0.35);
    }

    private static bool IsMarginSplineObject(XElement element)
    {
        if (!element.Name.LocalName.Equals("Object", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return element
            .Descendants()
            .Any(child =>
                child.Name.LocalName.Equals("Property", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(child.Attribute("name")?.Value, "Name", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(child.Attribute("value")?.Value, "Margin", StringComparison.OrdinalIgnoreCase));
    }

    private static List<Point3D> DecodeControlPointsPacked(byte[] bytes)
    {
        var points = new List<Point3D>();
        for (var offset = 0; offset + 12 <= bytes.Length; offset += ControlPointStrideBytes)
        {
            var x = BitConverter.ToSingle(bytes, offset);
            var y = BitConverter.ToSingle(bytes, offset + 4);
            var z = BitConverter.ToSingle(bytes, offset + 8);
            if (!IsPlausible(x, y, z))
            {
                continue;
            }

            points.Add(new Point3D(x, y, z));
        }

        return points;
    }

    private static bool IsPlausible(double x, double y, double z) =>
        x is >= MinPlausibleCoordinate and <= MaxPlausibleCoordinate &&
        y is >= MinPlausibleCoordinate and <= MaxPlausibleCoordinate &&
        z is >= MinPlausibleCoordinate and <= MaxPlausibleCoordinate &&
        !(Math.Abs(x) < 1e-6 && Math.Abs(y) < 1e-6 && Math.Abs(z) < 1e-6);

    private static List<Point3D> DensifyClosedLoop(IReadOnlyList<Point3D> controlPoints, double targetSpacingMm)
    {
        var spacing = Math.Max(targetSpacingMm, 0.1);
        var dense = new List<Point3D>();
        var count = controlPoints.Count;

        for (var i = 0; i < count; i++)
        {
            var a = controlPoints[i];
            var b = controlPoints[(i + 1) % count];
            var edge = b - a;
            var length = edge.Length;
            if (length < 1e-9)
            {
                continue;
            }

            var segments = Math.Max(1, (int)Math.Ceiling(length / spacing));
            for (var s = 0; s < segments; s++)
            {
                var t = s / (double)segments;
                dense.Add(a + (edge * t));
            }
        }

        return dense;
    }
}
