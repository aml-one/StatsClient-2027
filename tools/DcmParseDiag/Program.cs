using System.IO;
using System.Windows.Media.Media3D;
using DCMViewer.Services;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: DcmParseDiag <path-to.dcm>");
    return 1;
}

var path = Path.GetFullPath(args[0]);
var parser = new DcmParser();
var result = parser.ParseFile(path, CoordinateDecodingMode.Auto);
var mesh = result.Mesh;
var positions = mesh.Positions.ToList();
var indices = mesh.TriangleIndices.ToList();

var triangleCount = indices.Count / 3;
var longEdgeRatio = DcmParserDiagnostics.ComputeLongEdgeTriangleRatio(positions, indices);
var needleRatio = DcmParserDiagnostics.ComputeNeedleTriangleRatio(positions, indices);

Console.WriteLine($"File: {Path.GetFileName(path)}");
Console.WriteLine($"Vertices: {result.VertexCount:N0}");
Console.WriteLine($"Triangles: {result.TriangleCount:N0}");
Console.WriteLine($"Bounds diagonal: {Diagonal(mesh.Bounds):F3}");
Console.WriteLine($"Long-edge ratio (sampled): {longEdgeRatio:P2}");
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
