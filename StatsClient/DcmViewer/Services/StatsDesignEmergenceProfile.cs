using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>Bezier flare in the collar zone above the margin for a natural emergence profile.</summary>
internal static class StatsDesignEmergenceProfile
{
    public sealed class Result
    {
        public required MeshSnapshot Mesh { get; init; }
        public int AdjustedVertexCount { get; init; }
    }

    public static Result Generate(
        MeshSnapshot crownMesh,
        IReadOnlyList<Point3D> marginLoop,
        Vector3D insertionAxis,
        double transitionHeightMm)
    {
        ArgumentNullException.ThrowIfNull(crownMesh);
        if (marginLoop.Count < 3)
        {
            return new Result { Mesh = crownMesh, AdjustedVertexCount = 0 };
        }

        var axis = insertionAxis;
        if (axis.LengthSquared < 1e-12)
        {
            axis = StatsDesignInsertionAxis.Calculate(marginLoop);
        }
        else
        {
            axis.Normalize();
        }

        var transition = Math.Max(transitionHeightMm, 0.4);
        var marginDense = StatsDesignMarginSpline.DensifyClosed(marginLoop, targetSpacingMm: 0.25);
        var margin = StatsDesignMarginGeometry.Create(marginLoop, axis);
        var (axisU, axisV) = BuildPlaneBasis(axis);
        var positions = crownMesh.Positions.ToArray();
        var adjusted = 0;

        for (var index = 0; index < positions.Length; index++)
        {
            var vertex = positions[index];
            if (!margin.ContainsPoint(vertex))
            {
                continue;
            }

            var closestMargin = ClosestPointOnPolyline(vertex, marginDense);
            var heightAboveMargin = Vector3D.DotProduct(vertex - closestMargin, axis);
            if (heightAboveMargin <= 0.02 || heightAboveMargin >= transition)
            {
                continue;
            }

            var delta = vertex - margin.Centroid;
            var radial = (axisU * Vector3D.DotProduct(delta, axisU)) + (axisV * Vector3D.DotProduct(delta, axisV));
            if (radial.LengthSquared < 1e-12)
            {
                radial = axisU;
            }
            else
            {
                radial.Normalize();
            }

            var t = heightAboveMargin / transition;
            var p0 = closestMargin;
            var p3 = vertex;
            var p1 = p0 + (radial * (transition * 0.35)) + (axis * (transition * 0.33));
            var p2 = p3 - (radial * (transition * 0.12)) - (axis * (transition * 0.28));
            positions[index] = EvaluateCubicBezier(t, p0, p1, p2, p3);
            adjusted++;
        }

        if (adjusted > 0)
        {
            StatsDesignMeshSmoothing.LaplacianFair(
                positions,
                crownMesh.TriangleIndices.ToArray(),
                Enumerable.Range(0, positions.Length).ToHashSet(),
                passes: 1,
                lambda: 0.22,
                expandRings: 0);
        }

        return new Result
        {
            Mesh = new MeshSnapshot(positions, crownMesh.TriangleIndices.ToArray()),
            AdjustedVertexCount = adjusted
        };
    }

    private static Point3D EvaluateCubicBezier(double t, Point3D p0, Point3D p1, Point3D p2, Point3D p3)
    {
        var u = 1.0 - t;
        var uu = u * u;
        var tt = t * t;
        var uuu = uu * u;
        var ttt = tt * t;
        return new Point3D(
            (uuu * p0.X) + (3.0 * uu * t * p1.X) + (3.0 * u * tt * p2.X) + (ttt * p3.X),
            (uuu * p0.Y) + (3.0 * uu * t * p1.Y) + (3.0 * u * tt * p2.Y) + (ttt * p3.Y),
            (uuu * p0.Z) + (3.0 * uu * t * p1.Z) + (3.0 * u * tt * p2.Z) + (ttt * p3.Z));
    }

    private static Point3D ClosestPointOnPolyline(Point3D point, IReadOnlyList<Point3D> polyline)
    {
        var best = polyline[0];
        var bestDistSq = double.PositiveInfinity;
        for (var index = 0; index < polyline.Count; index++)
        {
            var next = polyline[(index + 1) % polyline.Count];
            var current = polyline[index];
            var closest = ClosestPointOnSegment(point, current, next);
            var distSq = (closest - point).LengthSquared;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = closest;
            }
        }

        return best;
    }

    private static Point3D ClosestPointOnSegment(Point3D point, Point3D a, Point3D b)
    {
        var ab = b - a;
        var lengthSq = ab.LengthSquared;
        if (lengthSq < 1e-12)
        {
            return a;
        }

        var t = Vector3D.DotProduct(point - a, ab) / lengthSq;
        t = Math.Clamp(t, 0.0, 1.0);
        return a + (ab * t);
    }

    private static (Vector3D U, Vector3D V) BuildPlaneBasis(Vector3D axis)
    {
        var reference = Math.Abs(axis.Z) < 0.9 ? new Vector3D(0, 0, 1) : new Vector3D(0, 1, 0);
        var u = Vector3D.CrossProduct(axis, reference);
        if (u.LengthSquared < 1e-12)
        {
            u = Vector3D.CrossProduct(axis, new Vector3D(1, 0, 0));
        }

        u.Normalize();
        var v = Vector3D.CrossProduct(axis, u);
        v.Normalize();
        return (u, v);
    }
}
