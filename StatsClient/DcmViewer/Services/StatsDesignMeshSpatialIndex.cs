using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>Uniform-grid acceleration for triangle raycasts and localized closest-point queries.</summary>
internal sealed class StatsDesignMeshSpatialIndex
{
    private readonly MeshSnapshot _mesh;
    private readonly double _cellSize;
    private readonly Dictionary<(int X, int Y, int Z), List<int>> _triangleBuckets = new();
    private readonly (Point3D Min, Point3D Max) _bounds;

    public StatsDesignMeshSpatialIndex(MeshSnapshot mesh, double cellSizeMm = 0.85)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        _mesh = mesh;
        _cellSize = Math.Max(cellSizeMm, 0.25);
        _bounds = ComputeBounds(mesh);
        BuildTriangleBuckets();
    }

    public MeshSnapshot Mesh => _mesh;

    public (Point3D Min, Point3D Max) Bounds => _bounds;

    public IEnumerable<int> EnumerateTriangleIndicesNear(Point3D point, double radiusMm)
    {
        var cellRadius = Math.Max(1, (int)Math.Ceiling(radiusMm / _cellSize));
        var center = CellKey(point);
        var seen = new HashSet<int>();

        for (var dx = -cellRadius; dx <= cellRadius; dx++)
        {
            for (var dy = -cellRadius; dy <= cellRadius; dy++)
            {
                for (var dz = -cellRadius; dz <= cellRadius; dz++)
                {
                    var key = (center.X + dx, center.Y + dy, center.Z + dz);
                    if (!_triangleBuckets.TryGetValue(key, out var bucket))
                    {
                        continue;
                    }

                    foreach (var triangleIndex in bucket)
                    {
                        if (seen.Add(triangleIndex))
                        {
                            yield return triangleIndex;
                        }
                    }
                }
            }
        }
    }

    public StatsDesignMeshProximity.ClosestPointResult ClosestPointOnMesh(Point3D point, double searchRadiusMm = 6.0)
    {
        var bestPoint = point;
        var bestDistSq = double.PositiveInfinity;
        var radius = Math.Max(searchRadiusMm, _cellSize * 2.0);

        foreach (var triangleIndex in EnumerateTriangleIndicesNear(point, radius))
        {
            if (!TryGetTriangle(triangleIndex, out var i0, out var i1, out var i2))
            {
                continue;
            }

            var closest = StatsDesignMeshRaycast.ClosestPointOnTriangle(
                point,
                _mesh.Positions[i0],
                _mesh.Positions[i1],
                _mesh.Positions[i2]);
            var dx = point.X - closest.X;
            var dy = point.Y - closest.Y;
            var dz = point.Z - closest.Z;
            var distSq = (dx * dx) + (dy * dy) + (dz * dz);
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestPoint = closest;
            }
        }

        if (double.IsPositiveInfinity(bestDistSq))
        {
            return StatsDesignMeshProximity.ClosestPointOnMeshBruteForce(point, _mesh);
        }

        return new StatsDesignMeshProximity.ClosestPointResult(bestPoint, Math.Sqrt(bestDistSq));
    }

    public StatsDesignMeshProximity.ClosestPointWithNormalResult ClosestPointAndNormalOnMesh(
        Point3D point,
        double searchRadiusMm = 6.0)
    {
        var bestPoint = point;
        var bestDistSq = double.PositiveInfinity;
        var bestNormal = new Vector3D(0, 0, 1);
        var radius = Math.Max(searchRadiusMm, _cellSize * 2.0);

        foreach (var triangleIndex in EnumerateTriangleIndicesNear(point, radius))
        {
            if (!TryGetTriangle(triangleIndex, out var i0, out var i1, out var i2))
            {
                continue;
            }

            var a = _mesh.Positions[i0];
            var b = _mesh.Positions[i1];
            var c = _mesh.Positions[i2];
            var closest = StatsDesignMeshRaycast.ClosestPointOnTriangle(point, a, b, c);
            var dx = point.X - closest.X;
            var dy = point.Y - closest.Y;
            var dz = point.Z - closest.Z;
            var distSq = (dx * dx) + (dy * dy) + (dz * dz);
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestPoint = closest;
                bestNormal = Vector3D.CrossProduct(b - a, c - a);
            }
        }

        if (double.IsPositiveInfinity(bestDistSq))
        {
            return StatsDesignMeshProximity.ClosestPointAndNormalOnMeshBruteForce(point, _mesh);
        }

        if (bestNormal.LengthSquared > 1e-12)
        {
            bestNormal.Normalize();
        }

        return new StatsDesignMeshProximity.ClosestPointWithNormalResult(bestPoint, Math.Sqrt(bestDistSq), bestNormal);
    }

    public bool TryRaycast(
        Point3D origin,
        Vector3D direction,
        double maxDistanceMm,
        out StatsDesignMeshRaycast.RayHit hit,
        int skipVertexIndex = -1,
        double minDistanceMm = 0.03)
    {
        hit = default;
        if (direction.LengthSquared < 1e-12 || maxDistanceMm <= minDistanceMm)
        {
            return false;
        }

        direction.Normalize();
        var bestT = double.PositiveInfinity;
        var found = false;
        Point3D bestPoint = default;
        var bestTriangle = -1;

        foreach (var triangleIndex in EnumerateTriangleIndicesNear(origin, maxDistanceMm))
        {
            if (!TryGetTriangle(triangleIndex, out var i0, out var i1, out var i2))
            {
                continue;
            }

            if (skipVertexIndex >= 0 &&
                (i0 == skipVertexIndex || i1 == skipVertexIndex || i2 == skipVertexIndex))
            {
                continue;
            }

            if (!StatsDesignMeshRaycast.TryIntersectRayTriangle(
                    origin,
                    direction,
                    _mesh.Positions[i0],
                    _mesh.Positions[i1],
                    _mesh.Positions[i2],
                    minDistanceMm,
                    maxDistanceMm,
                    out var t,
                    out var point))
            {
                continue;
            }

            if (t < bestT)
            {
                bestT = t;
                bestPoint = point;
                bestTriangle = triangleIndex;
                found = true;
            }
        }

        if (!found)
        {
            return false;
        }

        hit = new StatsDesignMeshRaycast.RayHit(bestPoint, bestT, bestTriangle);
        return true;
    }

    public IReadOnlyList<int> GetVertexIndicesInBounds((Point3D Min, Point3D Max) bounds)
    {
        var active = new List<int>();
        for (var index = 0; index < _mesh.Positions.Length; index++)
        {
            var vertex = _mesh.Positions[index];
            if (vertex.X >= bounds.Min.X && vertex.X <= bounds.Max.X &&
                vertex.Y >= bounds.Min.Y && vertex.Y <= bounds.Max.Y &&
                vertex.Z >= bounds.Min.Z && vertex.Z <= bounds.Max.Z)
            {
                active.Add(index);
            }
        }

        return active;
    }

    private void BuildTriangleBuckets()
    {
        var triangleIndex = 0;
        foreach (var (i0, i1, i2) in VoxelUnionGrid.EnumerateTriangleIndices(_mesh))
        {
            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= _mesh.Positions.Length || i1 >= _mesh.Positions.Length || i2 >= _mesh.Positions.Length)
            {
                triangleIndex++;
                continue;
            }

            var centroid = new Point3D(
                (_mesh.Positions[i0].X + _mesh.Positions[i1].X + _mesh.Positions[i2].X) / 3.0,
                (_mesh.Positions[i0].Y + _mesh.Positions[i1].Y + _mesh.Positions[i2].Y) / 3.0,
                (_mesh.Positions[i0].Z + _mesh.Positions[i1].Z + _mesh.Positions[i2].Z) / 3.0);

            var key = CellKey(centroid);
            if (!_triangleBuckets.TryGetValue(key, out var bucket))
            {
                bucket = new List<int>();
                _triangleBuckets[key] = bucket;
            }

            bucket.Add(triangleIndex);
            triangleIndex++;
        }
    }

    private bool TryGetTriangle(int triangleIndex, out int i0, out int i1, out int i2)
    {
        i0 = i1 = i2 = -1;
        var baseIndex = triangleIndex * 3;
        if (_mesh.TriangleIndices.Length >= baseIndex + 3)
        {
            i0 = _mesh.TriangleIndices[baseIndex];
            i1 = _mesh.TriangleIndices[baseIndex + 1];
            i2 = _mesh.TriangleIndices[baseIndex + 2];
            return true;
        }

        baseIndex = triangleIndex * 3;
        if (_mesh.Positions.Length >= baseIndex + 3)
        {
            i0 = baseIndex;
            i1 = baseIndex + 1;
            i2 = baseIndex + 2;
            return true;
        }

        return false;
    }

    private (int X, int Y, int Z) CellKey(Point3D point) =>
        ((int)Math.Floor(point.X / _cellSize),
         (int)Math.Floor(point.Y / _cellSize),
         (int)Math.Floor(point.Z / _cellSize));

    private static (Point3D Min, Point3D Max) ComputeBounds(MeshSnapshot mesh)
    {
        if (mesh.Positions.Length == 0)
        {
            return (new Point3D(0, 0, 0), new Point3D(0, 0, 0));
        }

        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var minZ = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;
        var maxZ = double.NegativeInfinity;

        foreach (var point in mesh.Positions)
        {
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            minZ = Math.Min(minZ, point.Z);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
            maxZ = Math.Max(maxZ, point.Z);
        }

        return (new Point3D(minX, minY, minZ), new Point3D(maxX, maxY, maxZ));
    }
}
