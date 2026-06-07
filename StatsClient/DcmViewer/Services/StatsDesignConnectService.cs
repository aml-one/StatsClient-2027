using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>
/// Cuts the library crown at its DCM margin spline, bridges to the user margin, and closes with the inner shell.
/// </summary>
internal static class StatsDesignConnectService
{
    public static MeshSnapshot BuildClosedCrown(
        MeshSnapshot libraryMesh,
        MeshSnapshot innerMesh,
        IReadOnlyList<Point3D> userMarginLoop,
        IReadOnlyList<Point3D> libraryMarginLoopWorld,
        StatsDesignManifest settings)
    {
        ArgumentNullException.ThrowIfNull(libraryMesh);
        ArgumentNullException.ThrowIfNull(innerMesh);
        ArgumentNullException.ThrowIfNull(userMarginLoop);
        ArgumentNullException.ThrowIfNull(libraryMarginLoopWorld);
        ArgumentNullException.ThrowIfNull(settings);

        if (libraryMarginLoopWorld.Count < 3)
        {
            throw new InvalidOperationException("Library margin loop is not available.");
        }

        var insertionAxis = ResolveInsertionAxis(settings);
        var userMargin = StatsDesignMarginGeometry.Create(userMarginLoop, insertionAxis);
        var libraryMargin = StatsDesignMarginGeometry.Create(libraryMarginLoopWorld);

        var clippedLibrary = ClipMeshKeepOcclusalSide(libraryMesh, libraryMargin);
        var librarySkirt = StatsDesignMarginBridge.BuildBoundaryToLoopRibbon(clippedLibrary, libraryMarginLoopWorld);
        var marginBridge = StatsDesignMarginBridge.BuildRuledBridge(libraryMarginLoopWorld, userMarginLoop);
        var innerSkirt = StatsDesignMarginBridge.BuildBoundaryToLoopRibbon(innerMesh, userMarginLoop);

        var parts = new List<MeshSnapshot> { innerMesh, clippedLibrary, librarySkirt, marginBridge, innerSkirt };
        var merged = MeshCombineFuse.Fuse(parts);

        if (!IsLikelyWatertight(merged))
        {
            merged = SealWithVoxelEnvelope(merged, settings);
        }

        var maxIsland = MeshFuseOptions.MapCleanupStrengthToMaxIslandTriangles(40);
        merged = MeshIslandCleanup.RemoveTinyIslands(merged, maxIsland);
        return MeshSmoothing.Smooth(merged, passes: 1);
    }

    private static MeshSnapshot ClipMeshKeepOcclusalSide(
        MeshSnapshot mesh,
        StatsDesignMarginGeometry margin)
    {
        var positions = mesh.Positions;
        var indices = new List<int>();

        foreach (var (i0, i1, i2) in VoxelUnionGrid.EnumerateTriangleIndices(mesh))
        {
            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= positions.Length || i1 >= positions.Length || i2 >= positions.Length)
            {
                continue;
            }

            var c = new Point3D(
                (positions[i0].X + positions[i1].X + positions[i2].X) / 3.0,
                (positions[i0].Y + positions[i1].Y + positions[i2].Y) / 3.0,
                (positions[i0].Z + positions[i1].Z + positions[i2].Z) / 3.0);

            var signed = SignedDistanceFromMarginPlane(c, margin);
            if (signed >= -0.12)
            {
                indices.Add(i0);
                indices.Add(i1);
                indices.Add(i2);
            }
        }

        if (indices.Count < 3)
        {
            throw new InvalidOperationException(
                "Library tooth clipping removed all geometry. Check tooth placement relative to the library margin.");
        }

        return new MeshSnapshot(positions, indices.ToArray());
    }

    private static double SignedDistanceFromMarginPlane(Point3D point, StatsDesignMarginGeometry margin)
    {
        var delta = point - margin.Centroid;
        return Vector3D.DotProduct(delta, margin.PlaneNormal);
    }

    private static bool IsLikelyWatertight(MeshSnapshot mesh)
    {
        var edgeCounts = new Dictionary<(int A, int B), int>();
        foreach (var (i0, i1, i2) in VoxelUnionGrid.EnumerateTriangleIndices(mesh))
        {
            CountEdge(edgeCounts, i0, i1);
            CountEdge(edgeCounts, i1, i2);
            CountEdge(edgeCounts, i2, i0);
        }

        return edgeCounts.Values.All(count => count == 2);
    }

    private static MeshSnapshot SealWithVoxelEnvelope(MeshSnapshot merged, StatsDesignManifest settings)
    {
        var gapVoxels = MapGapMmToVoxels(settings.CementGapMm + settings.ExtraCementGapMm);
        var thicknessVoxels = MapThicknessMmToVoxels(settings.MinimumThicknessMm);
        var options = new MeshFuseOptions(
            MeshFuseMode.VoxelEnvelope,
            176,
            gapVoxels,
            Math.Clamp((int)Math.Round(settings.SmoothDistanceMm * 4), 0, 4),
            thicknessVoxels);

        return VoxelShellFusion.Fuse([merged], options);
    }

    private static void CountEdge(Dictionary<(int A, int B), int> edgeCounts, int a, int b)
    {
        var key = a < b ? (a, b) : (b, a);
        edgeCounts.TryGetValue(key, out var count);
        edgeCounts[key] = count + 1;
    }

    private static int MapGapMmToVoxels(double gapMm)
    {
        var normalized = Math.Clamp(gapMm, 0.01, 2.0);
        return Math.Clamp((int)Math.Round(normalized * 12), MeshFuseOptions.MinGapBridgeVoxels, MeshFuseOptions.MaxGapBridgeVoxels);
    }

    private static Vector3D? ResolveInsertionAxis(StatsDesignManifest settings)
    {
        var axis = new Vector3D(settings.InsertionAxisX, settings.InsertionAxisY, settings.InsertionAxisZ);
        return axis.LengthSquared > 1e-12 ? axis : null;
    }

    private static int MapThicknessMmToVoxels(double thicknessMm)
    {
        var normalized = Math.Clamp(thicknessMm, 0.2, 2.5);
        return Math.Clamp((int)Math.Round(normalized * 5), MeshFuseOptions.MinShellThicknessVoxels, MeshFuseOptions.MaxShellThicknessVoxels);
    }
}
