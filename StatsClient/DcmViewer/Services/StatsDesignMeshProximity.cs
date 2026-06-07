using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>Closest-point queries between design meshes (triangle-accurate, linear scan).</summary>
internal static class StatsDesignMeshProximity
{
    public static double ClosestDistanceToMesh(Point3D point, MeshSnapshot mesh, StatsDesignMeshSpatialIndex? index = null)
    {
        return ClosestPointOnMesh(point, mesh, index).DistanceMm;
    }

    public static double ClosestDistanceToMeshes(
        Point3D point,
        IReadOnlyList<MeshSnapshot> meshes,
        IReadOnlyList<StatsDesignMeshSpatialIndex>? indices = null)
    {
        if (meshes.Count == 0)
        {
            return double.PositiveInfinity;
        }

        var best = double.PositiveInfinity;
        for (var meshIndex = 0; meshIndex < meshes.Count; meshIndex++)
        {
            var index = indices is not null && meshIndex < indices.Count ? indices[meshIndex] : null;
            best = Math.Min(best, ClosestDistanceToMesh(point, meshes[meshIndex], index));
        }

        return best;
    }

    public static ClosestPointResult ClosestPointOnMesh(
        Point3D point,
        MeshSnapshot mesh,
        StatsDesignMeshSpatialIndex? index = null)
    {
        if (index is not null && ReferenceEquals(index.Mesh, mesh))
        {
            return index.ClosestPointOnMesh(point);
        }

        if (mesh.Positions.Length > 4000)
        {
            return new StatsDesignMeshSpatialIndex(mesh).ClosestPointOnMesh(point);
        }

        return ClosestPointOnMeshBruteForce(point, mesh);
    }

    internal static ClosestPointResult ClosestPointOnMeshBruteForce(Point3D point, MeshSnapshot mesh)
    {
        var bestPoint = mesh.Positions.Length > 0 ? mesh.Positions[0] : point;
        var bestDistSq = double.PositiveInfinity;

        foreach (var (i0, i1, i2) in VoxelUnionGrid.EnumerateTriangleIndices(mesh))
        {
            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= mesh.Positions.Length || i1 >= mesh.Positions.Length || i2 >= mesh.Positions.Length)
            {
                continue;
            }

            var closest = StatsDesignMeshRaycast.ClosestPointOnTriangle(
                point,
                mesh.Positions[i0],
                mesh.Positions[i1],
                mesh.Positions[i2]);
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

        return new ClosestPointResult(bestPoint, Math.Sqrt(bestDistSq));
    }

    internal readonly struct ClosestPointResult
    {
        public ClosestPointResult(Point3D point, double distanceMm)
        {
            Point = point;
            DistanceMm = distanceMm;
        }

        public Point3D Point { get; }
        public double DistanceMm { get; }
    }

    internal readonly struct ClosestPointWithNormalResult
    {
        public ClosestPointWithNormalResult(Point3D point, double distanceMm, Vector3D normal)
        {
            Point = point;
            DistanceMm = distanceMm;
            Normal = normal;
        }

        public Point3D Point { get; }
        public double DistanceMm { get; }
        public Vector3D Normal { get; }
    }

    public static ClosestPointWithNormalResult ClosestPointAndNormalOnMesh(
        Point3D point,
        MeshSnapshot mesh,
        StatsDesignMeshSpatialIndex? index = null)
    {
        if (index is not null && ReferenceEquals(index.Mesh, mesh))
        {
            return index.ClosestPointAndNormalOnMesh(point);
        }

        if (mesh.Positions.Length > 4000)
        {
            return new StatsDesignMeshSpatialIndex(mesh).ClosestPointAndNormalOnMesh(point);
        }

        return ClosestPointAndNormalOnMeshBruteForce(point, mesh);
    }

    public static ClosestPointWithNormalResult ClosestPointAndNormalOnMeshes(
        Point3D point,
        IReadOnlyList<MeshSnapshot> meshes,
        IReadOnlyList<StatsDesignMeshSpatialIndex>? indices = null)
    {
        if (meshes.Count == 0)
        {
            return new ClosestPointWithNormalResult(point, 0, new Vector3D(0, 0, 1));
        }

        var best = ClosestPointAndNormalOnMesh(point, meshes[0], indices is { Count: > 0 } ? indices[0] : null);
        for (var meshIndex = 1; meshIndex < meshes.Count; meshIndex++)
        {
            var index = indices is not null && meshIndex < indices.Count ? indices[meshIndex] : null;
            var candidate = ClosestPointAndNormalOnMesh(point, meshes[meshIndex], index);
            if (candidate.DistanceMm < best.DistanceMm)
            {
                best = candidate;
            }
        }

        return best;
    }

    internal static ClosestPointWithNormalResult ClosestPointAndNormalOnMeshBruteForce(Point3D point, MeshSnapshot mesh)
    {
        var bestPoint = mesh.Positions.Length > 0 ? mesh.Positions[0] : point;
        var bestDistSq = double.PositiveInfinity;
        var bestNormal = new Vector3D(0, 0, 1);

        foreach (var (i0, i1, i2) in VoxelUnionGrid.EnumerateTriangleIndices(mesh))
        {
            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= mesh.Positions.Length || i1 >= mesh.Positions.Length || i2 >= mesh.Positions.Length)
            {
                continue;
            }

            var a = mesh.Positions[i0];
            var b = mesh.Positions[i1];
            var c = mesh.Positions[i2];
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

        if (bestNormal.LengthSquared > 1e-12)
        {
            bestNormal.Normalize();
        }
        else
        {
            bestNormal = new Vector3D(0, 0, 1);
        }

        return new ClosestPointWithNormalResult(bestPoint, Math.Sqrt(bestDistSq), bestNormal);
    }
}
