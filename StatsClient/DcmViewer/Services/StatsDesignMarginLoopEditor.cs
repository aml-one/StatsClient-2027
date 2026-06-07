using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>
/// Partial margin-loop edits: snap picks to an existing loop and replace only the arc covered by a stroke.
/// </summary>
internal static class StatsDesignMarginLoopEditor
{
    public const double SnapToLoopMaxDistanceMm = 4.0;

    public sealed record ClosestOnLoop(int SegmentStartIndex, Point3D Point, double DistanceMm);

    public static ClosestOnLoop FindClosestPoint(
        IReadOnlyList<Point3D> polyline,
        Point3D query,
        bool closed)
    {
        if (polyline.Count == 0)
        {
            return new ClosestOnLoop(0, query, double.MaxValue);
        }

        if (polyline.Count == 1)
        {
            var d0 = Distance(query, polyline[0]);
            return new ClosestOnLoop(0, polyline[0], d0);
        }

        var bestDistSq = double.MaxValue;
        var bestPoint = polyline[0];
        var bestIndex = 0;
        var segmentCount = closed ? polyline.Count : polyline.Count - 1;

        for (var i = 0; i < segmentCount; i++)
        {
            var j = closed ? (i + 1) % polyline.Count : i + 1;
            var closest = ClosestPointOnSegment(polyline[i], polyline[j], query);
            var distSq = (query - closest).LengthSquared;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestPoint = closest;
                bestIndex = i;
            }
        }

        return new ClosestOnLoop(bestIndex, bestPoint, Math.Sqrt(bestDistSq));
    }

    /// <summary>
    /// Loop kept on the long arc; the short arc from start to end is replaced by the stroke.</summary>
    public static List<Point3D> BuildSplicedPolyline(
        IReadOnlyList<Point3D> baseLoop,
        int startIndex,
        int endIndex,
        IReadOnlyList<Point3D> stroke,
        bool closed)
    {
        if (baseLoop.Count < 2 || stroke.Count == 0)
        {
            return stroke.Count > 0 ? stroke.ToList() : baseLoop.ToList();
        }

        if (!closed)
        {
            return BuildSplicedOpen(baseLoop, startIndex, endIndex, stroke);
        }

        var forwardCount = ForwardArcVertexCount(baseLoop.Count, startIndex, endIndex);
        var backwardCount = baseLoop.Count + 2 - forwardCount;
        var replaceForward = forwardCount <= backwardCount;

        var kept = replaceForward
            ? CollectBackwardArc(baseLoop, startIndex, endIndex)
            : CollectForwardArc(baseLoop, startIndex, endIndex);

        var merged = new List<Point3D>(kept.Count + stroke.Count + 2);
        merged.AddRange(kept);
        if (merged.Count > 0 && stroke.Count > 0 && Distance(merged[^1], stroke[0]) > 0.02)
        {
            merged.Add(stroke[0]);
        }

        var strokeStart = merged.Count > 0 && Distance(merged[^1], stroke[0]) < 0.02 ? 1 : 0;
        for (var i = strokeStart; i < stroke.Count; i++)
        {
            if (merged.Count == 0 || Distance(merged[^1], stroke[i]) > 0.02)
            {
                merged.Add(stroke[i]);
            }
        }

        return merged;
    }

    public static List<Point3D> DecimateToControlPoints(IReadOnlyList<Point3D> dense, int maxControlPoints = 48)
    {
        if (dense.Count <= maxControlPoints)
        {
            return dense.ToList();
        }

        var result = new List<Point3D>(maxControlPoints);
        var step = (double)(dense.Count - 1) / (maxControlPoints - 1);
        for (var i = 0; i < maxControlPoints; i++)
        {
            var index = (int)Math.Round(i * step);
            index = Math.Clamp(index, 0, dense.Count - 1);
            if (result.Count == 0 || result[^1] != dense[index])
            {
                result.Add(dense[index]);
            }
        }

        return result;
    }

    private static List<Point3D> BuildSplicedOpen(
        IReadOnlyList<Point3D> baseLoop,
        int startIndex,
        int endIndex,
        IReadOnlyList<Point3D> stroke)
    {
        startIndex = Math.Clamp(startIndex, 0, baseLoop.Count - 1);
        endIndex = Math.Clamp(endIndex, 0, baseLoop.Count - 1);
        if (startIndex > endIndex)
        {
            (startIndex, endIndex) = (endIndex, startIndex);
        }

        var merged = new List<Point3D>();
        for (var i = 0; i <= startIndex; i++)
        {
            merged.Add(baseLoop[i]);
        }

        foreach (var point in stroke)
        {
            if (merged.Count == 0 || Distance(merged[^1], point) > 0.02)
            {
                merged.Add(point);
            }
        }

        for (var i = endIndex; i < baseLoop.Count; i++)
        {
            if (merged.Count == 0 || Distance(merged[^1], baseLoop[i]) > 0.02)
            {
                merged.Add(baseLoop[i]);
            }
        }

        return merged;
    }

    private static int ForwardArcVertexCount(int count, int start, int end)
    {
        if (start <= end)
        {
            return end - start + 1;
        }

        return (count - start) + end + 1;
    }

    private static List<Point3D> CollectForwardArc(IReadOnlyList<Point3D> loop, int start, int end)
    {
        var result = new List<Point3D>();
        var i = start;
        var guard = 0;
        while (guard++ <= loop.Count + 1)
        {
            result.Add(loop[i]);
            if (i == end)
            {
                break;
            }

            i = (i + 1) % loop.Count;
        }

        return result;
    }

    private static List<Point3D> CollectBackwardArc(IReadOnlyList<Point3D> loop, int start, int end)
    {
        var result = new List<Point3D>();
        var i = start;
        var guard = 0;
        while (guard++ <= loop.Count + 1)
        {
            result.Add(loop[i]);
            if (i == end)
            {
                break;
            }

            i = (i - 1 + loop.Count) % loop.Count;
        }

        return result;
    }

    private static Point3D ClosestPointOnSegment(Point3D a, Point3D b, Point3D query)
    {
        var ab = b - a;
        var lengthSq = ab.LengthSquared;
        if (lengthSq < 1e-12)
        {
            return a;
        }

        var t = Vector3D.DotProduct(query - a, ab) / lengthSq;
        t = Math.Clamp(t, 0, 1);
        return a + (ab * t);
    }

    private static double Distance(Point3D a, Point3D b)
    {
        var d = a - b;
        return d.Length;
    }
}
