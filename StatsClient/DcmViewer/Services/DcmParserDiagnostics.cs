using System.IO;
using System.Windows.Media.Media3D;
using System.Xml.Linq;

namespace DCMViewer.Services;

/// <summary>Shared mesh metrics for diagnostics and sanitization.</summary>
internal static class DcmParserDiagnostics
{
    public static double ComputeLongEdgeTriangleRatio(IReadOnlyList<Point3D> positions, IReadOnlyList<int> triangleIndices)
    {
        var triangleCount = triangleIndices.Count / 3;
        if (triangleCount == 0 || positions.Count == 0)
        {
            return 0.0;
        }

        var bounds = ComputeBounds(positions);
        if (bounds == Rect3D.Empty)
        {
            return 0.0;
        }

        var diagonal = Math.Sqrt((bounds.SizeX * bounds.SizeX) + (bounds.SizeY * bounds.SizeY) + (bounds.SizeZ * bounds.SizeZ));
        if (!double.IsFinite(diagonal) || diagonal <= 0)
        {
            return 0.0;
        }

        var longEdgeThreshold = diagonal * 0.25;
        var sampleCount = Math.Min(triangleCount, 8000);
        var stride = Math.Max(1, triangleCount / sampleCount);
        var longEdges = 0;
        var considered = 0;

        for (var triangleIndex = 0; triangleIndex < triangleCount; triangleIndex += stride)
        {
            var i0 = triangleIndices[(triangleIndex * 3) + 0];
            var i1 = triangleIndices[(triangleIndex * 3) + 1];
            var i2 = triangleIndices[(triangleIndex * 3) + 2];

            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= positions.Count || i1 >= positions.Count || i2 >= positions.Count)
            {
                continue;
            }

            var p0 = positions[i0];
            var p1 = positions[i1];
            var p2 = positions[i2];

            if ((p0 - p1).Length > longEdgeThreshold)
            {
                longEdges++;
            }

            if ((p1 - p2).Length > longEdgeThreshold)
            {
                longEdges++;
            }

            if ((p2 - p0).Length > longEdgeThreshold)
            {
                longEdges++;
            }

            considered++;
        }

        if (considered == 0)
        {
            return 0.0;
        }

        return (double)longEdges / (considered * 3.0);
    }

    public static double ComputeNeedleTriangleRatio(IReadOnlyList<Point3D> positions, IReadOnlyList<int> triangleIndices)
        => DcmParserSanitizer.ComputeNeedleTriangleRatio(positions, triangleIndices);

    public static IEnumerable<string> DescribeFacetDecodeCandidates(string filePath)
    {
        var document = XDocument.Load(filePath, LoadOptions.None);
        var verticesElement = document.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("Vertices", StringComparison.OrdinalIgnoreCase));
        var facetsElement = document.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("Facets", StringComparison.OrdinalIgnoreCase));
        if (verticesElement is null || facetsElement is null)
        {
            yield return "Facet candidates: missing geometry nodes.";
            yield break;
        }

        var vertexCount = int.Parse(verticesElement.Attribute("vertex_count")?.Value ?? "0");
        var faceCount = int.Parse(facetsElement.Attribute("facet_count")?.Value ?? "0");
        var tryReadVertices = typeof(DcmParser).GetMethod("TryReadVerticesAsPlainFloatOrBase64", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var tryReadFacets = typeof(DcmParser).GetMethod("TryReadFacetsAsPlainIntOrBase64", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var decodeVertices = typeof(DcmParser).GetMethod("DecodeVertices", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var vertexBytes = ((byte[], bool))tryReadVertices.Invoke(null, [verticesElement.Value, vertexCount])!;
        var facetBytes = (byte[])tryReadFacets.Invoke(null, [facetsElement.Value, faceCount])!;
        var schema = document.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("Schema", StringComparison.OrdinalIgnoreCase))?.Value ?? "CE";
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var vertices = (List<Point3D>)decodeVertices.Invoke(null, [vertexBytes.Item1, vertexCount, null, schema, properties, CoordinateDecodingMode.Auto, vertexBytes.Item2])!;

        yield return $"Facet decode variants for {Path.GetFileName(filePath)}:";
        foreach (var line in DcmParser.DescribeFacetDecodeCandidates(facetBytes, faceCount, vertexCount, vertices))
        {
            yield return line;
        }
    }

    private static Rect3D ComputeBounds(IReadOnlyList<Point3D> positions)
    {
        if (positions.Count == 0)
        {
            return Rect3D.Empty;
        }

        var minX = positions[0].X;
        var minY = positions[0].Y;
        var minZ = positions[0].Z;
        var maxX = minX;
        var maxY = minY;
        var maxZ = minZ;

        for (var i = 1; i < positions.Count; i++)
        {
            var point = positions[i];
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            minZ = Math.Min(minZ, point.Z);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
            maxZ = Math.Max(maxZ, point.Z);
        }

        return new Rect3D(minX, minY, minZ, maxX - minX, maxY - minY, maxZ - minZ);
    }
}
