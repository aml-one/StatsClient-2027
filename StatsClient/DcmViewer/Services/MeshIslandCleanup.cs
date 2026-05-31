using System.Linq;
using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

internal static class MeshIslandCleanup
{
    private readonly struct DirectedEdge(int triangleIndex, int from, int to)
    {
        public int TriangleIndex { get; } = triangleIndex;
        public int From { get; } = from;
        public int To { get; } = to;
    }

    /// <summary>Removes only very small disconnected triangle islands (true loner patches).</summary>
    public static MeshSnapshot RemoveTinyIslands(MeshSnapshot mesh, int maxIslandTriangles = 18)
    {
        var positions = mesh.Positions;
        var indices = mesh.TriangleIndices.Length >= 3
            ? mesh.TriangleIndices
            : BuildSoupIndices(positions.Length);

        var triangleCount = indices.Length / 3;
        if (triangleCount <= maxIslandTriangles)
        {
            return mesh;
        }

        var adjacency = BuildTriangleAdjacency(indices, triangleCount);
        var visited = new bool[triangleCount];
        var components = new List<List<int>>();

        for (var triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            if (visited[triangleIndex])
            {
                continue;
            }

            var component = new List<int>();
            var queue = new Queue<int>();
            queue.Enqueue(triangleIndex);
            visited[triangleIndex] = true;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);

                var baseIndex = current * 3;
                var edges = new (int A, int B)[]
                {
                    (indices[baseIndex], indices[baseIndex + 1]),
                    (indices[baseIndex + 1], indices[baseIndex + 2]),
                    (indices[baseIndex + 2], indices[baseIndex])
                };

                foreach (var (a, b) in edges)
                {
                    var key = UndirectedEdgeKey(a, b);
                    if (!adjacency.TryGetValue(key, out var neighbors))
                    {
                        continue;
                    }

                    foreach (var neighbor in neighbors)
                    {
                        if (neighbor.TriangleIndex == current || visited[neighbor.TriangleIndex])
                        {
                            continue;
                        }

                        visited[neighbor.TriangleIndex] = true;
                        queue.Enqueue(neighbor.TriangleIndex);
                    }
                }
            }

            components.Add(component);
        }

        if (components.Count <= 1)
        {
            return mesh;
        }

        var largestCount = components.Max(static component => component.Count);
        var keepTriangle = new bool[triangleCount];
        Array.Fill(keepTriangle, true);

        foreach (var component in components)
        {
            if (component.Count >= largestCount)
            {
                continue;
            }

            if (component.Count > maxIslandTriangles)
            {
                continue;
            }

            foreach (var removeIndex in component)
            {
                keepTriangle[removeIndex] = false;
            }
        }

        if (keepTriangle.All(static flag => flag))
        {
            return mesh;
        }

        return RebuildMesh(positions, indices, keepTriangle);
    }

    private static MeshSnapshot RebuildMesh(Point3D[] positions, int[] indices, bool[] keepTriangle)
    {
        var triangleCount = indices.Length / 3;
        var newIndices = new List<int>(indices.Length);

        for (var triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            if (!keepTriangle[triangleIndex])
            {
                continue;
            }

            var baseIndex = triangleIndex * 3;
            var i0 = indices[baseIndex];
            var i1 = indices[baseIndex + 1];
            var i2 = indices[baseIndex + 2];
            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= positions.Length || i1 >= positions.Length || i2 >= positions.Length)
            {
                continue;
            }

            if (IsDegenerateTriangle(positions[i0], positions[i1], positions[i2]))
            {
                continue;
            }

            newIndices.Add(i0);
            newIndices.Add(i1);
            newIndices.Add(i2);
        }

        if (newIndices.Count < 3)
        {
            return new MeshSnapshot(positions.ToArray(), indices.ToArray());
        }

        return new MeshSnapshot(positions.ToArray(), newIndices.ToArray());
    }

    private static bool IsDegenerateTriangle(Point3D p0, Point3D p1, Point3D p2)
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
        return ((nx * nx) + (ny * ny) + (nz * nz)) <= 1e-14;
    }

    private static Dictionary<long, List<DirectedEdge>> BuildTriangleAdjacency(int[] indices, int triangleCount)
    {
        var adjacency = new Dictionary<long, List<DirectedEdge>>();

        for (var triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            var baseIndex = triangleIndex * 3;
            AddDirectedEdge(adjacency, indices[baseIndex], indices[baseIndex + 1], triangleIndex);
            AddDirectedEdge(adjacency, indices[baseIndex + 1], indices[baseIndex + 2], triangleIndex);
            AddDirectedEdge(adjacency, indices[baseIndex + 2], indices[baseIndex], triangleIndex);
        }

        return adjacency;
    }

    private static void AddDirectedEdge(
        Dictionary<long, List<DirectedEdge>> adjacency,
        int from,
        int to,
        int triangleIndex)
    {
        var key = UndirectedEdgeKey(from, to);
        if (!adjacency.TryGetValue(key, out var list))
        {
            list = new List<DirectedEdge>();
            adjacency[key] = list;
        }

        list.Add(new DirectedEdge(triangleIndex, from, to));
    }

    private static long UndirectedEdgeKey(int a, int b)
    {
        var min = Math.Min(a, b);
        var max = Math.Max(a, b);
        return ((long)min << 32) | (uint)max;
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
}
