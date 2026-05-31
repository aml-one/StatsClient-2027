using System.IO;
using DCMViewer.Services;

Environment.SetEnvironmentVariable("DCMVIEWER_ENABLE_NATIVE_LOADER", "0");
Environment.SetEnvironmentVariable("DCMVIEWER_FORCE_3SHAPE_DECRYPT", "0");

var repoRoot = FindRepoRoot();
var cases = BuildCases(repoRoot).Where(c => File.Exists(c.Path)).ToList();

if (cases.Count == 0)
{
    Console.Error.WriteLine("No regression DCM fixtures found.");
    return 1;
}

Console.WriteLine($"Running {cases.Count} DCM mesh regression case(s)...");

var parser = new DcmParser();
var failures = 0;

foreach (var testCase in cases)
{
    Console.WriteLine();
    Console.WriteLine($"[{testCase.Name}] {Path.GetFileName(testCase.Path)}");

    try
    {
        var result = parser.ParseFile(
            testCase.Path,
            CoordinateDecodingMode.Auto,
            allowThreeShapeFallback: false,
            applySceneTransform: false,
            sceneTransformKind: SceneTransformKind.None);

        var positions = result.Mesh.Positions;
        var indices = result.Mesh.TriangleIndices;
        var longTriFraction = DcmParserDiagnostics.ComputeLongEdgeTriangleFraction(positions, indices);
        var birdNest = DcmParserDiagnostics.IsLikelyBirdNestMesh(positions, indices);

        var errors = new List<string>();

        if (testCase.ExpectedVertices > 0 && result.VertexCount != testCase.ExpectedVertices)
        {
            errors.Add($"vertex count {result.VertexCount:N0} != expected {testCase.ExpectedVertices:N0}");
        }

        if (testCase.ExpectedTriangles > 0 && result.TriangleCount != testCase.ExpectedTriangles)
        {
            errors.Add($"triangle count {result.TriangleCount:N0} != expected {testCase.ExpectedTriangles:N0}");
        }

        if (longTriFraction > testCase.MaxLongTriangleFraction)
        {
            errors.Add($"long-edge triangle fraction {longTriFraction:P2} > limit {testCase.MaxLongTriangleFraction:P2}");
        }

        if (birdNest)
        {
            errors.Add("mesh classified as bird's nest (>5% long-edge triangles)");
        }

        if (errors.Count > 0)
        {
            failures++;
            Console.WriteLine("  FAIL");
            foreach (var error in errors)
            {
                Console.WriteLine($"    - {error}");
            }
        }
        else
        {
            Console.WriteLine(
                $"  PASS  vertices={result.VertexCount:N0}, triangles={result.TriangleCount:N0}, longTri={longTriFraction:P2}");
        }
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine("  FAIL");
        Console.WriteLine($"    - {ex.Message}");
    }
}

Console.WriteLine();
Console.WriteLine(failures == 0
    ? "All regression cases passed."
    : $"{failures} regression case(s) failed.");

return failures == 0 ? 0 : 1;

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "StatsClient", "StatsClient.csproj")))
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    throw new InvalidOperationException("Could not locate repository root.");
}

static IReadOnlyList<RegressionCase> BuildCases(string repoRoot)
{
    var cases = new List<RegressionCase>
    {
        BuildDeclaredCase(repoRoot, "hpsdecode CA sample", Path.Combine("Docs", "regression", "example_ca.dcm"), 0.05),
        BuildDeclaredCase(repoRoot, "hpsdecode CE sample", Path.Combine("Docs", "regression", "example_ce.dcm"), 0.02),
        BuildDeclaredCase(repoRoot, "Raw Preparation scan", Path.Combine("Docs", "Raw Preparation scan.dcm"), 0.02),
        BuildDeclaredCase(repoRoot, "Raw Antagonist scan", Path.Combine("Docs", "Raw Antagonist scan.dcm"), 0.02),
    };

    var networkPrep = @"\\3SH-SRV-23\3Shape Dental System Orders\7160-8-15-1M2-DAVIS-ZACHARAH-CHANGE-CHANGE\Scans\Upper\MB Preparation scan.dcm";
    if (File.Exists(networkPrep))
    {
        cases.Add(BuildDeclaredCase(networkPrep, "7160 MB Preparation scan", networkPrep, 0.02, isAbsolutePath: true));
    }

    return cases;
}

static RegressionCase BuildDeclaredCase(
    string repoRoot,
    string name,
    string relativeOrAbsolutePath,
    double maxLongTriangleFraction,
    bool isAbsolutePath = false)
{
    var path = isAbsolutePath ? relativeOrAbsolutePath : Path.Combine(repoRoot, relativeOrAbsolutePath);
    var (vertices, facets) = File.Exists(path)
        ? DcmParserDiagnostics.ReadDeclaredGeometryCounts(path)
        : (0, 0);

    return new RegressionCase(name, path, vertices, facets, maxLongTriangleFraction);
}

internal sealed record RegressionCase(
    string Name,
    string Path,
    int ExpectedVertices,
    int ExpectedTriangles,
    double MaxLongTriangleFraction);
