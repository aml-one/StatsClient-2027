using System.Windows.Media.Media3D;
using HelixToolkit.Wpf.SharpDX;

namespace DCMViewer.Services;

/// <summary>Mutable mesh copy used for in-viewer sculpting; export reads the latest snapshot.</summary>
internal sealed class EditableMeshState
{
    private readonly Point3D[] _positions;
    private readonly int[] _triangleIndices;
    private readonly List<int>[] _neighbors;
    private readonly Vector3D[] _vertexNormals;

    public EditableMeshState(MeshSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _positions = snapshot.Positions.ToArray();
        _triangleIndices = snapshot.TriangleIndices.Length >= 3
            ? snapshot.TriangleIndices.ToArray()
            : BuildSoupIndices(snapshot.Positions.Length);

        _neighbors = BuildAdjacency(_positions.Length, _triangleIndices);
        _vertexNormals = ComputeVertexNormals(_positions, _triangleIndices);
    }

    public int VertexCount => _positions.Length;

    public int TriangleCount => _triangleIndices.Length / 3;

    public MeshSnapshot ToSnapshot() => new(_positions.ToArray(), _triangleIndices.ToArray());

    public Point3D[] CapturePositions() => _positions.ToArray();

    public void RestorePositions(Point3D[] positions)
    {
        if (positions.Length != _positions.Length)
        {
            return;
        }

        Array.Copy(positions, _positions, _positions.Length);
        RecomputeVertexNormals();
        InvalidateBrushQueryCache();
    }

    public bool ApplyStroke(
        SculptBrushTool tool,
        Point3D center,
        Vector3D surfaceNormal,
        double radius,
        double strength,
        Vector3D? grabDelta)
    {
        if (_positions.Length == 0 || radius <= 1e-9)
        {
            return false;
        }

        var radiusSq = radius * radius;
        var normal = surfaceNormal;
        if (normal.LengthSquared > 1e-12)
        {
            normal.Normalize();
        }
        else
        {
            normal = new Vector3D(0, 0, 1);
        }

        var changed = false;

        if (tool == SculptBrushTool.Smooth)
        {
            var targets = new Point3D[_positions.Length];
            Array.Copy(_positions, targets, _positions.Length);

            for (var vertexIndex = 0; vertexIndex < _positions.Length; vertexIndex++)
            {
                var delta = _positions[vertexIndex] - center;
                if (delta.LengthSquared > radiusSq)
                {
                    continue;
                }

                var falloff = ComputeFalloff(delta.LengthSquared, radiusSq);
                if (falloff <= 1e-6)
                {
                    continue;
                }

                var nbrs = _neighbors[vertexIndex];
                if (nbrs.Count == 0)
                {
                    continue;
                }

                var avgX = 0.0;
                var avgY = 0.0;
                var avgZ = 0.0;
                foreach (var neighborIndex in nbrs)
                {
                    avgX += _positions[neighborIndex].X;
                    avgY += _positions[neighborIndex].Y;
                    avgZ += _positions[neighborIndex].Z;
                }

                avgX /= nbrs.Count;
                avgY /= nbrs.Count;
                avgZ /= nbrs.Count;

                var blend = Math.Clamp(strength * falloff, 0.0, 1.0);
                targets[vertexIndex] = new Point3D(
                    _positions[vertexIndex].X + (blend * (avgX - _positions[vertexIndex].X)),
                    _positions[vertexIndex].Y + (blend * (avgY - _positions[vertexIndex].Y)),
                    _positions[vertexIndex].Z + (blend * (avgZ - _positions[vertexIndex].Z)));
                changed = true;
            }

            if (changed)
            {
                Array.Copy(targets, _positions, _positions.Length);
            }
        }
        else
        {
            for (var vertexIndex = 0; vertexIndex < _positions.Length; vertexIndex++)
            {
                var delta = _positions[vertexIndex] - center;
                var distSq = delta.LengthSquared;
                if (distSq > radiusSq)
                {
                    continue;
                }

                var falloff = ComputeFalloff(distSq, radiusSq);
                if (falloff <= 1e-6)
                {
                    continue;
                }

                switch (tool)
                {
                    case SculptBrushTool.Add:
                    {
                        var vertexNormal = _vertexNormals[vertexIndex];
                        if (vertexNormal.LengthSquared < 1e-12)
                        {
                            vertexNormal = normal;
                        }

                        var displacement = strength * falloff;
                        _positions[vertexIndex] = new Point3D(
                            _positions[vertexIndex].X + (vertexNormal.X * displacement),
                            _positions[vertexIndex].Y + (vertexNormal.Y * displacement),
                            _positions[vertexIndex].Z + (vertexNormal.Z * displacement));
                        changed = true;
                        break;
                    }

                    case SculptBrushTool.Remove:
                    {
                        var vertexNormal = _vertexNormals[vertexIndex];
                        if (vertexNormal.LengthSquared < 1e-12)
                        {
                            vertexNormal = normal;
                        }

                        var displacement = strength * falloff;
                        _positions[vertexIndex] = new Point3D(
                            _positions[vertexIndex].X - (vertexNormal.X * displacement),
                            _positions[vertexIndex].Y - (vertexNormal.Y * displacement),
                            _positions[vertexIndex].Z - (vertexNormal.Z * displacement));
                        changed = true;
                        break;
                    }

                    case SculptBrushTool.Grab when grabDelta.HasValue:
                    {
                        var move = grabDelta.Value * falloff;
                        _positions[vertexIndex] = new Point3D(
                            _positions[vertexIndex].X + move.X,
                            _positions[vertexIndex].Y + move.Y,
                            _positions[vertexIndex].Z + move.Z);
                        changed = true;
                        break;
                    }
                }
            }
        }

        if (changed)
        {
            RecomputeVertexNormals();
            InvalidateBrushQueryCache();
        }

        return changed;
    }

    public void PushToModel(MeshGeometryModel3D model)
    {
        ArgumentNullException.ThrowIfNull(model);
        model.Geometry = SharpDxMeshFactory.CreateGeometry(ToSnapshot());
    }

    public Vector3D GetNearestSurfaceNormal(Point3D point)
    {
        if (_positions.Length == 0)
        {
            return new Vector3D(0, 0, 1);
        }

        var bestIndex = 0;
        var bestDistSq = double.MaxValue;
        for (var index = 0; index < _positions.Length; index++)
        {
            var delta = _positions[index] - point;
            var distSq = delta.LengthSquared;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestIndex = index;
            }
        }

        var normal = _vertexNormals[bestIndex];
        if (normal.LengthSquared > 1e-12)
        {
            return normal;
        }

        return new Vector3D(0, 0, 1);
    }

    private int[]? _brushLocalTriangleStarts;
    private Point3D _brushLocalTrianglesCenter;
    private double _brushLocalTrianglesRadiusSq;

    private void InvalidateBrushQueryCache() => _brushLocalTriangleStarts = null;

    public Point3D[] BuildSurfaceBrushRing(
        Point3D center,
        Vector3D normal,
        double radius,
        int segments = 32)
    {
        if (_positions.Length == 0 || radius <= 1e-9)
        {
            return [center];
        }

        var ringNormal = normal;
        if (ringNormal.LengthSquared < 1e-12)
        {
            ringNormal = new Vector3D(0, 0, 1);
        }

        ringNormal.Normalize();

        var axisX = Vector3D.CrossProduct(ringNormal, Math.Abs(ringNormal.Z) < 0.9 ? new Vector3D(0, 0, 1) : new Vector3D(0, 1, 0));
        if (axisX.LengthSquared < 1e-12)
        {
            axisX = Vector3D.CrossProduct(ringNormal, new Vector3D(1, 0, 0));
        }

        axisX.Normalize();
        var axisY = Vector3D.CrossProduct(axisX, ringNormal);
        axisY.Normalize();

        var clampedRadius = Math.Max(radius, 0.1);
        var maxDistFromCenterSq = (clampedRadius * 1.04) * (clampedRadius * 1.04);
        var localTriangles = GetLocalTriangleStarts(center, maxDistFromCenterSq);
        const int radialSteps = 6;
        var stepLength = clampedRadius / radialSteps;
        var maxSnapFromPlanarSq = (stepLength * 1.35) * (stepLength * 1.35);
        var clampedSegments = Math.Max(segments, 16);
        var points = new Point3D[clampedSegments + 1];

        for (var segmentIndex = 0; segmentIndex < clampedSegments; segmentIndex++)
        {
            var angle = (Math.PI * 2.0 * segmentIndex) / clampedSegments;
            var direction = (axisX * Math.Cos(angle)) + (axisY * Math.Sin(angle));
            var surfacePoint = center;

            for (var step = 1; step <= radialSteps; step++)
            {
                var planarTarget = center + (direction * (stepLength * step));
                var snapped = ConstrainedClosestOnSurface(
                    planarTarget,
                    center,
                    maxDistFromCenterSq,
                    maxSnapFromPlanarSq,
                    localTriangles);

                if (snapped.HasValue)
                {
                    surfacePoint = snapped.Value;
                }
            }

            var liftNormal = GetNearestSurfaceNormalInLocalTriangles(surfacePoint, localTriangles);
            if (liftNormal.LengthSquared > 1e-12)
            {
                liftNormal.Normalize();
                surfacePoint += liftNormal * 0.012;
            }
            else
            {
                surfacePoint += ringNormal * 0.012;
            }

            points[segmentIndex] = surfacePoint;
        }

        points[clampedSegments] = points[0];
        return points;
    }

    private int[] GetLocalTriangleStarts(Point3D center, double maxDistSq)
    {
        const double reuseMoveFraction = 0.35;
        if (_brushLocalTriangleStarts is not null &&
            DistanceSquared(center, _brushLocalTrianglesCenter) <= maxDistSq * reuseMoveFraction * reuseMoveFraction &&
            Math.Abs(_brushLocalTrianglesRadiusSq - maxDistSq) < 1e-9)
        {
            return _brushLocalTriangleStarts;
        }

        var list = new List<int>(_triangleIndices.Length / 12);
        for (var index = 0; index + 2 < _triangleIndices.Length; index += 3)
        {
            var i0 = _triangleIndices[index];
            var i1 = _triangleIndices[index + 1];
            var i2 = _triangleIndices[index + 2];
            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= _positions.Length || i1 >= _positions.Length || i2 >= _positions.Length)
            {
                continue;
            }

            if (DistanceSquared(_positions[i0], center) > maxDistSq &&
                DistanceSquared(_positions[i1], center) > maxDistSq &&
                DistanceSquared(_positions[i2], center) > maxDistSq)
            {
                continue;
            }

            list.Add(index);
        }

        _brushLocalTriangleStarts = list.ToArray();
        _brushLocalTrianglesCenter = center;
        _brushLocalTrianglesRadiusSq = maxDistSq;
        return _brushLocalTriangleStarts;
    }

    private Point3D? ConstrainedClosestOnSurface(
        Point3D planarTarget,
        Point3D center,
        double maxDistFromCenterSq,
        double maxSnapFromPlanarSq,
        int[] localTriangleStarts)
    {
        Point3D? bestPoint = null;
        var bestDistSq = double.MaxValue;

        foreach (var triangleStart in localTriangleStarts)
        {
            var p0 = _positions[_triangleIndices[triangleStart]];
            var p1 = _positions[_triangleIndices[triangleStart + 1]];
            var p2 = _positions[_triangleIndices[triangleStart + 2]];

            var closest = ClosestPointOnTriangle(planarTarget, p0, p1, p2);
            if (DistanceSquared(closest, center) > maxDistFromCenterSq)
            {
                continue;
            }

            var snapDistSq = DistanceSquared(closest, planarTarget);
            if (snapDistSq > maxSnapFromPlanarSq)
            {
                continue;
            }

            if (snapDistSq < bestDistSq)
            {
                bestDistSq = snapDistSq;
                bestPoint = closest;
            }
        }

        return bestPoint;
    }

    private Vector3D GetNearestSurfaceNormalInLocalTriangles(Point3D point, int[] localTriangleStarts)
    {
        var bestDistSq = double.MaxValue;
        var bestIndex = -1;

        foreach (var triangleStart in localTriangleStarts)
        {
            foreach (var vertexIndex in new[]
                     {
                         _triangleIndices[triangleStart],
                         _triangleIndices[triangleStart + 1],
                         _triangleIndices[triangleStart + 2]
                     })
            {
                if (vertexIndex < 0 || vertexIndex >= _positions.Length)
                {
                    continue;
                }

                var distSq = DistanceSquared(_positions[vertexIndex], point);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestIndex = vertexIndex;
                }
            }
        }

        if (bestIndex >= 0 && _vertexNormals[bestIndex].LengthSquared > 1e-12)
        {
            return _vertexNormals[bestIndex];
        }

        return GetNearestSurfaceNormal(point);
    }

    private static double DistanceSquared(Point3D a, Point3D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var dz = a.Z - b.Z;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    private static Point3D ClosestPointOnTriangle(Point3D point, Point3D a, Point3D b, Point3D c)
    {
        var ab = b - a;
        var ac = c - a;
        var ap = point - a;

        var d1 = Vector3D.DotProduct(ab, ap);
        var d2 = Vector3D.DotProduct(ac, ap);
        if (d1 <= 0.0 && d2 <= 0.0)
        {
            return a;
        }

        var bp = point - b;
        var d3 = Vector3D.DotProduct(ab, bp);
        var d4 = Vector3D.DotProduct(ac, bp);
        if (d3 >= 0.0 && d4 <= d3)
        {
            return b;
        }

        var vc = (d1 * d4) - (d3 * d2);
        if (vc <= 0.0 && d1 >= 0.0 && d3 <= 0.0)
        {
            var v = d1 / (d1 - d3);
            return a + (ab * v);
        }

        var cp = point - c;
        var d5 = Vector3D.DotProduct(ab, cp);
        var d6 = Vector3D.DotProduct(ac, cp);
        if (d6 >= 0.0 && d5 <= d6)
        {
            return c;
        }

        var vb = (d5 * d2) - (d1 * d6);
        if (vb <= 0.0 && d2 >= 0.0 && d6 <= 0.0)
        {
            var w = d2 / (d2 - d6);
            return a + (ac * w);
        }

        var va = (d3 * d6) - (d5 * d4);
        if (va <= 0.0 && (d4 - d3) >= 0.0 && (d5 - d6) >= 0.0)
        {
            var w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            return b + ((c - b) * w);
        }

        var denom = 1.0 / (va + vb + vc);
        var vBary = vb * denom;
        var wBary = vc * denom;
        return a + (ab * vBary) + (ac * wBary);
    }

    private void RecomputeVertexNormals()
    {
        var computed = ComputeVertexNormals(_positions, _triangleIndices);
        Array.Copy(computed, _vertexNormals, computed.Length);
    }

    private static double ComputeFalloff(double distanceSq, double radiusSq)
    {
        var t = 1.0 - (distanceSq / radiusSq);
        return t * t;
    }

    private static int[] BuildSoupIndices(int positionCount)
    {
        var count = Math.Max(0, positionCount / 3) * 3;
        var indices = new int[count];
        for (var index = 0; index < count; index++)
        {
            indices[index] = index;
        }

        return indices;
    }

    private static List<int>[] BuildAdjacency(int vertexCount, int[] triangleIndices)
    {
        var neighbors = new List<int>[vertexCount];
        for (var index = 0; index < vertexCount; index++)
        {
            neighbors[index] = new List<int>();
        }

        for (var index = 0; index + 2 < triangleIndices.Length; index += 3)
        {
            var i0 = triangleIndices[index];
            var i1 = triangleIndices[index + 1];
            var i2 = triangleIndices[index + 2];
            if (i0 < 0 || i1 < 0 || i2 < 0 || i0 >= vertexCount || i1 >= vertexCount || i2 >= vertexCount)
            {
                continue;
            }

            AddNeighbor(neighbors, i0, i1);
            AddNeighbor(neighbors, i0, i2);
            AddNeighbor(neighbors, i1, i0);
            AddNeighbor(neighbors, i1, i2);
            AddNeighbor(neighbors, i2, i0);
            AddNeighbor(neighbors, i2, i1);
        }

        return neighbors;
    }

    private static void AddNeighbor(List<int>[] neighbors, int from, int to)
    {
        var list = neighbors[from];
        if (!list.Contains(to))
        {
            list.Add(to);
        }
    }

    private static Vector3D[] ComputeVertexNormals(Point3D[] positions, int[] triangleIndices)
    {
        var normals = new Vector3D[positions.Length];

        for (var index = 0; index + 2 < triangleIndices.Length; index += 3)
        {
            var i0 = triangleIndices[index];
            var i1 = triangleIndices[index + 1];
            var i2 = triangleIndices[index + 2];
            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= positions.Length || i1 >= positions.Length || i2 >= positions.Length)
            {
                continue;
            }

            var faceNormal = ComputeFaceNormal(positions[i0], positions[i1], positions[i2]);
            if (faceNormal.LengthSquared < 1e-12)
            {
                continue;
            }

            normals[i0] += faceNormal;
            normals[i1] += faceNormal;
            normals[i2] += faceNormal;
        }

        for (var index = 0; index < normals.Length; index++)
        {
            if (normals[index].LengthSquared > 1e-12)
            {
                normals[index].Normalize();
            }
        }

        return normals;
    }

    private static Vector3D ComputeFaceNormal(Point3D p0, Point3D p1, Point3D p2)
    {
        var ux = p1.X - p0.X;
        var uy = p1.Y - p0.Y;
        var uz = p1.Z - p0.Z;
        var vx = p2.X - p0.X;
        var vy = p2.Y - p0.Y;
        var vz = p2.Z - p0.Z;

        var nx = (uy * vz) - (uz * vy);
        var ny = (uz * vx) - (ux * vz);
        var nz = (ux * vy) - (uy * vx);
        return new Vector3D(nx, ny, nz);
    }
}
