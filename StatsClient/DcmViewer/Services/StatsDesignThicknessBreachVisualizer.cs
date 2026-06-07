using HelixToolkit.Maths;
using System.Windows.Media.Media3D;
using Color4 = HelixToolkit.Maths.Color4;

namespace DCMViewer.Services;

/// <summary>Live vertex overlay — magenta where wall thickness to prep falls below minimum.</summary>
internal static class StatsDesignThicknessBreachVisualizer
{
    private static readonly Color4 NeutralColor = new(0.85f, 0.80f, 0.75f, 1f);
    private static readonly Color4 BreachColor = new(1.0f, 0.0f, 0.5f, 1f);

    public sealed class Result
    {
        public required Color4[] VertexColors { get; init; }
        public int BreachVertexCount { get; init; }
        public double MinimumObservedThicknessMm { get; init; }
    }

    public static Result BuildBreachOverlay(
        MeshSnapshot crownMesh,
        MeshSnapshot preparationMesh,
        double minimumThicknessMm)
    {
        ArgumentNullException.ThrowIfNull(crownMesh);
        ArgumentNullException.ThrowIfNull(preparationMesh);

        var minThickness = Math.Max(minimumThicknessMm, 0.1);
        var prepIndex = new StatsDesignMeshSpatialIndex(preparationMesh);
        var colors = new Color4[crownMesh.Positions.Length];
        var breachCount = 0;
        var minObserved = double.PositiveInfinity;

        for (var index = 0; index < crownMesh.Positions.Length; index++)
        {
            var thickness = prepIndex.ClosestPointOnMesh(crownMesh.Positions[index]).DistanceMm;
            minObserved = Math.Min(minObserved, thickness);
            if (thickness < minThickness)
            {
                colors[index] = BreachColor;
                breachCount++;
            }
            else
            {
                colors[index] = NeutralColor;
            }
        }

        return new Result
        {
            VertexColors = colors,
            BreachVertexCount = breachCount,
            MinimumObservedThicknessMm = double.IsPositiveInfinity(minObserved) ? 0 : minObserved
        };
    }

    public static string Summarize(Result result, double minimumThicknessMm)
    {
        if (result.BreachVertexCount == 0)
        {
            return $"Wall thickness OK — minimum {result.MinimumObservedThicknessMm:F2} mm (limit {minimumThicknessMm:F2} mm).";
        }

        var pct = result.VertexColors.Length > 0
            ? (result.BreachVertexCount * 100.0) / result.VertexColors.Length
            : 0;
        return $"Structural breach: {result.BreachVertexCount} vertices ({pct:F0}%) below {minimumThicknessMm:F2} mm — shown magenta.";
    }
}
