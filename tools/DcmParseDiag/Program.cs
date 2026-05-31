using System.IO;
using System.Windows.Media.Media3D;
using DCMViewer.Services;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: DcmParseDiag <path-to.dcm> [--no-transform]");
    return 1;
}

var path = Path.GetFullPath(args[0]);
var noTransform = args.Any(a => string.Equals(a, "--no-transform", StringComparison.OrdinalIgnoreCase));
var parser = new DcmParser();
var result = parser.ParseFile(
    path,
    CoordinateDecodingMode.Auto,
    applySceneTransform: !noTransform,
    sceneTransformKind: SceneTransformKind.Scan);
var mesh = result.Mesh;
var positions = mesh.Positions.ToList();
var indices = mesh.TriangleIndices.ToList();

var triangleCount = indices.Count / 3;
var longEdgeRatio = DcmParserDiagnostics.ComputeLongEdgeTriangleRatio(positions, indices);
var needleRatio = DcmParserDiagnostics.ComputeNeedleTriangleRatio(positions, indices);
var topologyHealth = DcmParserDiagnostics.ScoreMeshTopologyHealth(positions, indices);
var healthy = DcmParserDiagnostics.IsMeshTopologyHealthy(positions, indices);
var bounds = mesh.Bounds;
var center = new Point3D(
    bounds.X + (bounds.SizeX * 0.5),
    bounds.Y + (bounds.SizeY * 0.5),
    bounds.Z + (bounds.SizeZ * 0.5));

Console.WriteLine($"File: {Path.GetFileName(path)}");
Console.WriteLine($"Vertices: {result.VertexCount:N0}");
Console.WriteLine($"Triangles: {result.TriangleCount:N0}");
Console.WriteLine($"Bounds center: ({center.X:F2}, {center.Y:F2}, {center.Z:F2})");
Console.WriteLine($"Bounds size: ({bounds.SizeX:F2}, {bounds.SizeY:F2}, {bounds.SizeZ:F2})");
Console.WriteLine($"Bounds diagonal: {Diagonal(bounds):F3}");
Console.WriteLine($"Topology health: {topologyHealth:F3} (healthy={healthy})");
Console.WriteLine($"Long-edge ratio (sampled): {longEdgeRatio:P2}");
Console.WriteLine($"Long-edge triangle fraction (sampled): {DcmParserDiagnostics.ComputeLongEdgeTriangleFraction(positions, indices):P2}");
Console.WriteLine($"Bird's nest: {DcmParserDiagnostics.IsLikelyBirdNestMesh(positions, indices)}");
Console.WriteLine($"Needle-triangle ratio (sampled): {needleRatio:P2}");
foreach (var line in DcmParserDiagnostics.DescribeFacetDecodeCandidates(path))
{
    Console.WriteLine(line);
}

return 0;

static double Diagonal(Rect3D bounds)
{
    if (bounds == Rect3D.Empty)
    {
        return 0;
    }

    return Math.Sqrt((bounds.SizeX * bounds.SizeX) + (bounds.SizeY * bounds.SizeY) + (bounds.SizeZ * bounds.SizeZ));
}
