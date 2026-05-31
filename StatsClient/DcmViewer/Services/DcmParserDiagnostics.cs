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

    /// <summary>Fraction of sampled triangles with any edge longer than 25% of the mesh diagonal.</summary>
    public static double ComputeLongEdgeTriangleFraction(IReadOnlyList<Point3D> positions, IReadOnlyList<int> triangleIndices)
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
        var longTriangles = 0;
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

            if ((p0 - p1).Length > longEdgeThreshold ||
                (p1 - p2).Length > longEdgeThreshold ||
                (p2 - p0).Length > longEdgeThreshold)
            {
                longTriangles++;
            }

            considered++;
        }

        return considered == 0 ? 0.0 : (double)longTriangles / considered;
    }

    /// <summary>Bird's-nest meshes have many triangles spanning the model due to wrong facet connectivity.</summary>
    public static bool IsLikelyBirdNestMesh(IReadOnlyList<Point3D> positions, IReadOnlyList<int> triangleIndices)
        => ComputeLongEdgeTriangleFraction(positions, triangleIndices) > 0.05;

    public static (int VertexCount, int FacetCount) ReadDeclaredGeometryCounts(string filePath)
    {
        var document = System.Xml.Linq.XDocument.Load(filePath, System.Xml.Linq.LoadOptions.None);
        var verticesElement = document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName.Equals("Vertices", StringComparison.OrdinalIgnoreCase));
        var facetsElement = document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName.Equals("Facets", StringComparison.OrdinalIgnoreCase));

        static int ReadCount(System.Xml.Linq.XElement? element, string primary, string fallback)
        {
            if (element is null)
            {
                return 0;
            }

            var value = element.Attribute(primary)?.Value ?? element.Attribute(fallback)?.Value;
            return value is not null && int.TryParse(value, out var count) ? count : 0;
        }

        return (
            ReadCount(verticesElement, "vertex_count", "Count"),
            ReadCount(facetsElement, "facet_count", "Count"));
    }

    public static double ComputeNeedleTriangleRatio(IReadOnlyList<Point3D> positions, IReadOnlyList<int> triangleIndices)
        => DcmParserSanitizer.ComputeNeedleTriangleRatio(positions, triangleIndices);

    /// <summary>0 = healthy topology, higher = more bridge/needle artifacts.</summary>
    public static double ScoreMeshTopologyHealth(IReadOnlyList<Point3D> positions, IReadOnlyList<int> triangleIndices)
    {
        if (triangleIndices.Count < 9 || positions.Count == 0)
        {
            return 0.0;
        }

        var needleRatio = ComputeNeedleTriangleRatio(positions, triangleIndices);
        var longEdgeRatio = ComputeLongEdgeTriangleRatio(positions, triangleIndices);
        return (needleRatio * 2.5) + (longEdgeRatio * 1.5);
    }

    public static bool IsMeshTopologyHealthy(IReadOnlyList<Point3D> positions, IReadOnlyList<int> triangleIndices)
        => ScoreMeshTopologyHealth(positions, triangleIndices) < 0.12;

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
        var readMetadata = typeof(DcmParser).GetMethod("ReadMetadata", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null, [typeof(string)], null)!;
        var metadata = readMetadata.Invoke(null, [filePath]);
        var properties = (IReadOnlyDictionary<string, string>)metadata!.GetType().GetProperty("Properties")!.GetGetMethod()!.Invoke(metadata, null)!;
        var schema = metadata.GetType().GetProperty("Schema")!.GetGetMethod()!.Invoke(metadata, null)?.ToString() ?? "CE";
        var parentName = verticesElement.Parent?.Name?.LocalName ?? string.Empty;
        var effectiveSchema = parentName.Equals("CE", StringComparison.OrdinalIgnoreCase) ? "CE" : schema;
        var checkValueAttr = verticesElement.Attribute("check_value")?.Value;
        uint? checkValue = checkValueAttr is not null && uint.TryParse(checkValueAttr, out var cv) ? cv : null;
        var tryReadVertices = typeof(DcmParser).GetMethod("TryReadVerticesAsPlainFloatOrBase64", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var tryReadFacets = typeof(DcmParser).GetMethod("TryReadFacetsAsPlainIntOrBase64", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var decodeVertices = typeof(DcmParser).GetMethod("DecodeVertices", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var vertexBytes = ((byte[], bool))tryReadVertices.Invoke(null, [verticesElement.Value, vertexCount])!;
        var facetBytes = (byte[])tryReadFacets.Invoke(null, [facetsElement.Value, faceCount])!;
        var vertices = (List<Point3D>)decodeVertices.Invoke(null, [vertexBytes.Item1, vertexCount, checkValue, effectiveSchema, new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase), CoordinateDecodingMode.Auto, vertexBytes.Item2])!;

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
