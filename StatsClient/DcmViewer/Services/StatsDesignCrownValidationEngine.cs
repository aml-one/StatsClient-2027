using System.Text;
using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>Pre-export inspection: watertight shell, minimum thickness, seat-path undercuts.</summary>
internal static class StatsDesignCrownValidationEngine
{
    public sealed class ValidationReport
    {
        public bool IsWatertight { get; init; }
        public bool HasSafeThickness { get; init; }
        public bool IsSeatPathUndercutFree { get; init; }
        public int OpenBoundaryEdgeCount { get; init; }
        public int ThinVertexCount { get; init; }
        public int SeatPathViolationCount { get; init; }
        public double MinimumObservedThicknessMm { get; init; }

        public bool PassedAllChecks => IsWatertight && HasSafeThickness && IsSeatPathUndercutFree;

        public string Summary
        {
            get
            {
                if (PassedAllChecks)
                {
                    return "Validation passed — crown is ready for manufacturing export.";
                }

                var builder = new StringBuilder("Validation issues:");
                if (!IsWatertight)
                {
                    builder.Append($" {OpenBoundaryEdgeCount} open boundary edge(s);");
                }

                if (!HasSafeThickness)
                {
                    builder.Append($" {ThinVertexCount} thin vertex(es) (min {MinimumObservedThicknessMm:F2} mm);");
                }

                if (!IsSeatPathUndercutFree)
                {
                    builder.Append($" {SeatPathViolationCount} seat-path undercut(s);");
                }

                return builder.ToString().TrimEnd(';', ' ') + ".";
            }
        }
    }

    public static ValidationReport Inspect(
        MeshSnapshot crownMesh,
        MeshSnapshot preparationMesh,
        IReadOnlyList<Point3D> marginLoop,
        Vector3D insertionAxis,
        double minimumThicknessMm)
    {
        ArgumentNullException.ThrowIfNull(crownMesh);
        ArgumentNullException.ThrowIfNull(preparationMesh);

        var openEdges = CountOpenBoundaryEdges(crownMesh);
        var thickness = AuditThickness(crownMesh, preparationMesh, minimumThicknessMm);
        var seatViolations = marginLoop.Count >= 3
            ? CountSeatPathViolations(crownMesh, marginLoop, insertionAxis)
            : 0;

        return new ValidationReport
        {
            IsWatertight = openEdges == 0,
            HasSafeThickness = thickness.ThinCount == 0,
            IsSeatPathUndercutFree = seatViolations == 0,
            OpenBoundaryEdgeCount = openEdges,
            ThinVertexCount = thickness.ThinCount,
            SeatPathViolationCount = seatViolations,
            MinimumObservedThicknessMm = thickness.MinObservedMm
        };
    }

    private static int CountOpenBoundaryEdges(MeshSnapshot mesh)
    {
        var edgeUse = new Dictionary<(int A, int B), int>();
        foreach (var (i0, i1, i2) in VoxelUnionGrid.EnumerateTriangleIndices(mesh))
        {
            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= mesh.Positions.Length || i1 >= mesh.Positions.Length || i2 >= mesh.Positions.Length)
            {
                continue;
            }

            AccumulateEdge(edgeUse, i0, i1);
            AccumulateEdge(edgeUse, i1, i2);
            AccumulateEdge(edgeUse, i2, i0);
        }

        return edgeUse.Values.Count(count => count == 1);
    }

    private static void AccumulateEdge(Dictionary<(int A, int B), int> edgeUse, int a, int b)
    {
        var key = a < b ? (a, b) : (b, a);
        edgeUse.TryGetValue(key, out var count);
        edgeUse[key] = count + 1;
    }

    private static (int ThinCount, double MinObservedMm) AuditThickness(
        MeshSnapshot crownMesh,
        MeshSnapshot preparationMesh,
        double minimumThicknessMm)
    {
        var prepIndex = new StatsDesignMeshSpatialIndex(preparationMesh);
        var minThickness = Math.Max(minimumThicknessMm, 0.1);
        var thinCount = 0;
        var minObserved = double.PositiveInfinity;

        foreach (var vertex in crownMesh.Positions)
        {
            var thickness = prepIndex.ClosestPointOnMesh(vertex).DistanceMm;
            minObserved = Math.Min(minObserved, thickness);
            if (thickness < minThickness)
            {
                thinCount++;
            }
        }

        return (thinCount, double.IsPositiveInfinity(minObserved) ? 0 : minObserved);
    }

    private static int CountSeatPathViolations(
        MeshSnapshot crownMesh,
        IReadOnlyList<Point3D> marginLoop,
        Vector3D insertionAxis)
    {
        var axis = insertionAxis;
        if (axis.LengthSquared < 1e-12)
        {
            axis = StatsDesignInsertionAxis.Calculate(marginLoop);
        }
        else
        {
            axis.Normalize();
        }

        var margin = StatsDesignMarginGeometry.Create(marginLoop, axis);
        var spatial = new StatsDesignMeshSpatialIndex(crownMesh);
        var violations = 0;

        for (var index = 0; index < crownMesh.Positions.Length; index++)
        {
            var vertex = crownMesh.Positions[index];
            if (!margin.ContainsPoint(vertex))
            {
                continue;
            }

            var rayOrigin = vertex + (axis * 0.02);
            if (spatial.TryRaycast(rayOrigin, axis, maxDistanceMm: 18.0, out _, skipVertexIndex: index))
            {
                violations++;
            }
        }

        return violations;
    }
}
