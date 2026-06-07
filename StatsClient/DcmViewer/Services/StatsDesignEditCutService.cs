using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>
/// Cuts a mesh by a plane, removes one side, and seals the open boundary with a flat cap.
/// </summary>
internal static class StatsDesignEditCutService
{
    private const double PlaneEpsilon = 1e-4;
    private const double LoopOnPlaneTolerance = 5e-4;

    public static MeshSnapshot CutAndCap(
        MeshSnapshot mesh,
        Point3D planePoint,
        Vector3D planeNormal,
        bool keepPositiveSide)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var normal = planeNormal;
        if (normal.LengthSquared < 1e-12)
        {
            normal = new Vector3D(0, 0, 1);
        }
        else
        {
            normal.Normalize();
        }

        if (!keepPositiveSide)
        {
            normal = new Vector3D(-normal.X, -normal.Y, -normal.Z);
        }

        var positions = new List<Point3D>(mesh.Positions);
        var indices = new List<int>();

        foreach (var (i0, i1, i2) in EnumerateTriangles(mesh))
        {
            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= mesh.Positions.Length || i1 >= mesh.Positions.Length || i2 >= mesh.Positions.Length)
            {
                continue;
            }

            var p0 = mesh.Positions[i0];
            var p1 = mesh.Positions[i1];
            var p2 = mesh.Positions[i2];
            var d0 = SignedDistance(p0, planePoint, normal);
            var d1 = SignedDistance(p1, planePoint, normal);
            var d2 = SignedDistance(p2, planePoint, normal);

            if (d0 >= -PlaneEpsilon && d1 >= -PlaneEpsilon && d2 >= -PlaneEpsilon)
            {
                indices.Add(i0);
                indices.Add(i1);
                indices.Add(i2);
                continue;
            }

            if (d0 <= PlaneEpsilon && d1 <= PlaneEpsilon && d2 <= PlaneEpsilon)
            {
                continue;
            }

            var clipped = ClipTriangle(p0, p1, p2, d0, d1, d2, planePoint, normal);
            if (clipped.Count < 3)
            {
                continue;
            }

            var clippedIndices = new List<int>(clipped.Count);
            foreach (var point in clipped)
            {
                clippedIndices.Add(GetOrAddVertex(positions, point));
            }

            for (var index = 1; index + 1 < clippedIndices.Count; index++)
            {
                indices.Add(clippedIndices[0]);
                indices.Add(clippedIndices[index]);
                indices.Add(clippedIndices[index + 1]);
            }
        }

        if (indices.Count < 3)
        {
            throw new InvalidOperationException("Cut plane removed all restoration geometry.");
        }

        AddPlanarCap(positions, indices, planePoint, normal);
        RemoveDegenerateTriangles(positions, indices);
        return new MeshSnapshot(positions.ToArray(), indices.ToArray());
    }

    public static HelixToolkit.Maths.Color4[] BuildCutPreviewColors(
        EditableMeshState mesh,
        Point3D planePoint,
        Vector3D planeNormal,
        bool removePositiveSide,
        HelixToolkit.Maths.Color4 keepDiffuse)
    {
        var normal = NormalizePlaneNormal(planeNormal);
        var positions = mesh.CapturePositions();
        var colors = new HelixToolkit.Maths.Color4[positions.Length];
        var removalTint = new HelixToolkit.Maths.Color4(0.95f, 0.12f, 0.12f, 1f);
        const float removalMix = 0.72f;
        for (var index = 0; index < positions.Length; index++)
        {
            var signed = SignedDistance(positions[index], planePoint, normal);
            var markedForRemoval = removePositiveSide ? signed >= -PlaneEpsilon : signed <= PlaneEpsilon;
            colors[index] = markedForRemoval
                ? LerpColor4(keepDiffuse, removalTint, removalMix)
                : keepDiffuse;
        }

        return colors;
    }

    private static HelixToolkit.Maths.Color4 LerpColor4(
        HelixToolkit.Maths.Color4 a,
        HelixToolkit.Maths.Color4 b,
        float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new HelixToolkit.Maths.Color4(
            a.Red + ((b.Red - a.Red) * t),
            a.Green + ((b.Green - a.Green) * t),
            a.Blue + ((b.Blue - a.Blue) * t),
            a.Alpha + ((b.Alpha - a.Alpha) * t));
    }

    public static List<(Point3D A, Point3D B)> BuildPlaneIntersectionSegments(
        MeshSnapshot mesh,
        Point3D planePoint,
        Vector3D planeNormal)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var normal = NormalizePlaneNormal(planeNormal);
        var segments = new List<(Point3D A, Point3D B)>();
        const double epsilon = 1e-7;

        foreach (var (i0, i1, i2) in EnumerateTriangles(mesh))
        {
            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= mesh.Positions.Length || i1 >= mesh.Positions.Length || i2 >= mesh.Positions.Length)
            {
                continue;
            }

            var a = mesh.Positions[i0];
            var b = mesh.Positions[i1];
            var c = mesh.Positions[i2];
            var da = SignedDistance(a, planePoint, normal);
            var db = SignedDistance(b, planePoint, normal);
            var dc = SignedDistance(c, planePoint, normal);

            if ((da > epsilon && db > epsilon && dc > epsilon) ||
                (da < -epsilon && db < -epsilon && dc < -epsilon))
            {
                continue;
            }

            var points = new List<Point3D>(3);
            TryAddIntersectionPoint(a, da, b, db, points);
            TryAddIntersectionPoint(b, db, c, dc, points);
            TryAddIntersectionPoint(c, dc, a, da, points);

            if (points.Count >= 2)
            {
                segments.Add((points[0], points[1]));
            }
        }

        return segments;
    }

    private static void TryAddIntersectionPoint(
        Point3D p1,
        double d1,
        Point3D p2,
        double d2,
        List<Point3D> result)
    {
        const double epsilon = 1e-7;
        if ((d1 > epsilon && d2 > epsilon) || (d1 < -epsilon && d2 < -epsilon))
        {
            return;
        }

        if (Math.Abs(d1 - d2) <= epsilon)
        {
            return;
        }

        var t = d1 / (d1 - d2);
        t = Math.Clamp(t, 0.0, 1.0);
        var point = p1 + ((p2 - p1) * t);

        foreach (var existing in result)
        {
            if ((existing - point).LengthSquared <= 1e-10)
            {
                return;
            }
        }

        result.Add(point);
    }

    private static Vector3D NormalizePlaneNormal(Vector3D planeNormal)
    {
        var normal = planeNormal;
        if (normal.LengthSquared < 1e-12)
        {
            return new Vector3D(0, 0, 1);
        }

        normal.Normalize();
        return normal;
    }

    private static void AddPlanarCap(
        List<Point3D> positions,
        List<int> indices,
        Point3D planePoint,
        Vector3D planeNormal)
    {
        var boundaryEdges = FindBoundaryEdges(indices);
        if (boundaryEdges.Count == 0)
        {
            return;
        }

        var loops = BuildBoundaryLoops(boundaryEdges);
        foreach (var loop in loops)
        {
            if (loop.Count < 3)
            {
                continue;
            }

            // Only seal the hole created by this cut. Older cap perimeters (duplicate-vertex rims from
            // previous cuts) must not be projected onto this plane — that creates floating geometry.
            if (!IsLoopOnCutPlane(loop, positions, planePoint, planeNormal))
            {
                continue;
            }

            var projected = ProjectLoopToPlane(loop, positions, planePoint, planeNormal);
            if (projected.Count < 3)
            {
                continue;
            }

            foreach (var (vertexIndex, projectedPoint) in projected)
            {
                positions[vertexIndex] = projectedPoint;
            }

            var ordered = DeduplicateLoopVertices(OrderPointsCounterClockwise(projected, planeNormal));
            if (ordered.Count < 3)
            {
                continue;
            }

            // Reuse boundary vertex indices so the cap is welded to the clipped mesh.
            for (var index = 1; index + 1 < ordered.Count; index++)
            {
                var i0 = ordered[0].Index;
                var i1 = ordered[index].Index;
                var i2 = ordered[index + 1].Index;
                if (i0 == i1 || i1 == i2 || i2 == i0)
                {
                    continue;
                }

                var p0 = positions[i0];
                var p1 = positions[i1];
                var p2 = positions[i2];
                var triNormal = Vector3D.CrossProduct(p1 - p0, p2 - p0);
                if (triNormal.LengthSquared < 1e-14)
                {
                    continue;
                }

                if (Vector3D.DotProduct(triNormal, planeNormal) > 0)
                {
                    indices.Add(i0);
                    indices.Add(i2);
                    indices.Add(i1);
                }
                else
                {
                    indices.Add(i0);
                    indices.Add(i1);
                    indices.Add(i2);
                }
            }
        }
    }

    private static bool IsLoopOnCutPlane(
        IReadOnlyList<int> loop,
        IReadOnlyList<Point3D> positions,
        Point3D planePoint,
        Vector3D planeNormal)
    {
        var onPlaneCount = 0;
        foreach (var vertexIndex in loop)
        {
            if (vertexIndex < 0 || vertexIndex >= positions.Count)
            {
                return false;
            }

            if (Math.Abs(SignedDistance(positions[vertexIndex], planePoint, planeNormal)) <= LoopOnPlaneTolerance)
            {
                onPlaneCount++;
            }
        }

        return onPlaneCount >= 3 && onPlaneCount == loop.Count;
    }

    private static List<(int Index, Point3D Point)> DeduplicateLoopVertices(
        IReadOnlyList<(int Index, Point3D Point)> points)
    {
        if (points.Count <= 1)
        {
            return points.ToList();
        }

        var deduplicated = new List<(int Index, Point3D Point)>(points.Count);
        foreach (var point in points)
        {
            if (deduplicated.Count > 0 &&
                deduplicated[^1].Index == point.Index)
            {
                continue;
            }

            if (deduplicated.Count > 0 &&
                (deduplicated[^1].Point - point.Point).LengthSquared <= 1e-10)
            {
                continue;
            }

            deduplicated.Add(point);
        }

        if (deduplicated.Count > 1 &&
            deduplicated[0].Index == deduplicated[^1].Index)
        {
            deduplicated.RemoveAt(deduplicated.Count - 1);
        }

        return deduplicated;
    }

    private static void RemoveDegenerateTriangles(IReadOnlyList<Point3D> positions, List<int> indices)
    {
        const double minAreaSq = 1e-14;
        for (var index = 0; index + 2 < indices.Count;)
        {
            var i0 = indices[index];
            var i1 = indices[index + 1];
            var i2 = indices[index + 2];
            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= positions.Count || i1 >= positions.Count || i2 >= positions.Count ||
                i0 == i1 || i1 == i2 || i2 == i0)
            {
                indices.RemoveRange(index, 3);
                continue;
            }

            var areaSq = Vector3D.CrossProduct(
                positions[i1] - positions[i0],
                positions[i2] - positions[i0]).LengthSquared;
            if (areaSq < minAreaSq)
            {
                indices.RemoveRange(index, 3);
                continue;
            }

            index += 3;
        }
    }

    private static List<(int Index, Point3D Point)> OrderPointsCounterClockwise(
        IReadOnlyList<(int Index, Point3D Point)> points,
        Vector3D planeNormal)
    {
        if (points.Count <= 3)
        {
            return points.ToList();
        }

        var axisX = Vector3D.CrossProduct(planeNormal, new Vector3D(0, 1, 0));
        if (axisX.LengthSquared < 1e-9)
        {
            axisX = Vector3D.CrossProduct(planeNormal, new Vector3D(1, 0, 0));
        }

        axisX.Normalize();
        var axisY = Vector3D.CrossProduct(axisX, planeNormal);
        axisY.Normalize();

        var centroid = ComputeCentroid(points.Select(p => p.Point));
        return points
            .OrderBy(p => Math.Atan2(
                Vector3D.DotProduct(p.Point - centroid, axisY),
                Vector3D.DotProduct(p.Point - centroid, axisX)))
            .ToList();
    }

    private static List<(int Index, Point3D Point)> ProjectLoopToPlane(
        IReadOnlyList<int> loop,
        IReadOnlyList<Point3D> positions,
        Point3D planePoint,
        Vector3D planeNormal)
    {
        var projected = new List<(int Index, Point3D Point)>(loop.Count);
        foreach (var vertexIndex in loop)
        {
            if (vertexIndex < 0 || vertexIndex >= positions.Count)
            {
                continue;
            }

            var point = positions[vertexIndex];
            var signed = SignedDistance(point, planePoint, planeNormal);
            var projectedPoint = signed <= PlaneEpsilon
                ? point
                : point - (planeNormal * signed);
            projected.Add((vertexIndex, projectedPoint));
        }

        return projected;
    }

    private static Point3D ComputeCentroid(IEnumerable<Point3D> points)
    {
        var count = 0;
        var sum = new Vector3D();
        foreach (var point in points)
        {
            sum += new Vector3D(point.X, point.Y, point.Z);
            count++;
        }

        if (count == 0)
        {
            return new Point3D();
        }

        return new Point3D(sum.X / count, sum.Y / count, sum.Z / count);
    }

    private static List<List<int>> BuildBoundaryLoops(Dictionary<(int A, int B), int> boundaryEdges)
    {
        var adjacency = new Dictionary<int, List<int>>();
        foreach (var edge in boundaryEdges.Keys)
        {
            AddAdjacency(adjacency, edge.A, edge.B);
            AddAdjacency(adjacency, edge.B, edge.A);
        }

        var visited = new HashSet<(int A, int B)>();
        var loops = new List<List<int>>();

        foreach (var edge in boundaryEdges.Keys)
        {
            if (visited.Contains(edge))
            {
                continue;
            }

            var loop = new List<int> { edge.A, edge.B };
            visited.Add(edge);
            visited.Add((edge.B, edge.A));

            var current = edge.B;
            while (true)
            {
                if (!adjacency.TryGetValue(current, out var neighbors))
                {
                    break;
                }

                var next = -1;
                foreach (var neighbor in neighbors)
                {
                    if (neighbor == loop[0])
                    {
                        continue;
                    }

                    if (!visited.Contains(NormalizeEdge(current, neighbor)))
                    {
                        next = neighbor;
                        break;
                    }
                }

                if (next < 0)
                {
                    if (loop.Count > 2 &&
                        neighbors.Contains(loop[0]) &&
                        !visited.Contains(NormalizeEdge(current, loop[0])))
                    {
                        break;
                    }

                    break;
                }

                var directed = NormalizeEdge(current, next);
                if (visited.Contains(directed))
                {
                    break;
                }

                visited.Add(directed);
                visited.Add((directed.B, directed.A));
                loop.Add(next);
                current = next;
            }

            if (loop.Count >= 3)
            {
                loops.Add(loop);
            }
        }

        return loops;
    }

    private static void AddAdjacency(Dictionary<int, List<int>> adjacency, int from, int to)
    {
        if (!adjacency.TryGetValue(from, out var list))
        {
            list = [];
            adjacency[from] = list;
        }

        if (!list.Contains(to))
        {
            list.Add(to);
        }
    }

    private static (int A, int B) NormalizeEdge(int a, int b) => a < b ? (a, b) : (b, a);

    private static Dictionary<(int A, int B), int> FindBoundaryEdges(IReadOnlyList<int> indices)
    {
        var edgeCounts = new Dictionary<(int A, int B), int>();
        for (var index = 0; index + 2 < indices.Count; index += 3)
        {
            CountEdge(edgeCounts, indices[index], indices[index + 1]);
            CountEdge(edgeCounts, indices[index + 1], indices[index + 2]);
            CountEdge(edgeCounts, indices[index + 2], indices[index]);
        }

        return edgeCounts
            .Where(pair => pair.Value == 1)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private static void CountEdge(Dictionary<(int A, int B), int> edgeCounts, int a, int b)
    {
        var key = NormalizeEdge(a, b);
        edgeCounts.TryGetValue(key, out var count);
        edgeCounts[key] = count + 1;
    }

    private static int GetOrAddVertex(List<Point3D> positions, Point3D point)
    {
        for (var index = 0; index < positions.Count; index++)
        {
            if ((positions[index] - point).LengthSquared < 1e-10)
            {
                return index;
            }
        }

        positions.Add(point);
        return positions.Count - 1;
    }

    private static List<Point3D> ClipTriangle(
        Point3D p0,
        Point3D p1,
        Point3D p2,
        double d0,
        double d1,
        double d2,
        Point3D planePoint,
        Vector3D planeNormal)
    {
        var input = new List<(Point3D Point, double Distance)>
        {
            (p0, d0),
            (p1, d1),
            (p2, d2)
        };

        var output = ClipPolygonByHalfSpace(input, planePoint, planeNormal, keepPositive: true);
        return output.Select(item => item.Point).ToList();
    }

    private static List<(Point3D Point, double Distance)> ClipPolygonByHalfSpace(
        IReadOnlyList<(Point3D Point, double Distance)> input,
        Point3D planePoint,
        Vector3D planeNormal,
        bool keepPositive)
    {
        if (input.Count == 0)
        {
            return [];
        }

        var output = new List<(Point3D Point, double Distance)>();
        for (var index = 0; index < input.Count; index++)
        {
            var current = input[index];
            var next = input[(index + 1) % input.Count];
            var currentInside = keepPositive ? current.Distance >= -PlaneEpsilon : current.Distance <= PlaneEpsilon;
            var nextInside = keepPositive ? next.Distance >= -PlaneEpsilon : next.Distance <= PlaneEpsilon;

            if (currentInside)
            {
                output.Add(current);
            }

            if (currentInside == nextInside)
            {
                continue;
            }

            var t = IntersectPlane(current, next, planePoint, planeNormal);
            output.Add(t);
        }

        return output;
    }

    private static (Point3D Point, double Distance) IntersectPlane(
        (Point3D Point, double Distance) a,
        (Point3D Point, double Distance) b,
        Point3D planePoint,
        Vector3D planeNormal)
    {
        var denom = a.Distance - b.Distance;
        var t = Math.Abs(denom) < 1e-12 ? 0.5 : a.Distance / denom;
        t = Math.Clamp(t, 0, 1);
        var point = new Point3D(
            a.Point.X + ((b.Point.X - a.Point.X) * t),
            a.Point.Y + ((b.Point.Y - a.Point.Y) * t),
            a.Point.Z + ((b.Point.Z - a.Point.Z) * t));
        return (point, SignedDistance(point, planePoint, planeNormal));
    }

    private static double SignedDistance(Point3D point, Point3D planePoint, Vector3D planeNormal) =>
        Vector3D.DotProduct(point - planePoint, planeNormal);

    private static IEnumerable<(int I0, int I1, int I2)> EnumerateTriangles(MeshSnapshot mesh)
    {
        if (mesh.TriangleIndices.Length >= 3)
        {
            for (var index = 0; index + 2 < mesh.TriangleIndices.Length; index += 3)
            {
                yield return (mesh.TriangleIndices[index], mesh.TriangleIndices[index + 1], mesh.TriangleIndices[index + 2]);
            }

            yield break;
        }

        for (var index = 0; index + 2 < mesh.Positions.Length; index += 3)
        {
            yield return (index, index + 1, index + 2);
        }
    }
}
