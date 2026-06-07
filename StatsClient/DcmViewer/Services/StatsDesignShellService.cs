using System.Windows.Media.Media3D;



namespace DCMViewer.Services;



internal static class StatsDesignShellService

{

    /// <summary>

    /// Open inner cement surface inside the margin (yellow preview mesh — not watertight).

    /// </summary>

    public static MeshSnapshot CreateOpenInnerShell(

        IReadOnlyList<MeshSnapshot> prepMeshes,

        IReadOnlyList<Point3D> marginLoop,

        StatsDesignManifest settings)

    {

        ArgumentNullException.ThrowIfNull(prepMeshes);

        ArgumentNullException.ThrowIfNull(marginLoop);

        ArgumentNullException.ThrowIfNull(settings);



        if (prepMeshes.Count == 0)

        {

            throw new InvalidOperationException("No arch scan meshes are available to build a design shell.");

        }



        if (!settings.IsMarginClosed || marginLoop.Count < 3)

        {

            throw new InvalidOperationException("Close the margin loop on the arch scan before generating a shell.");

        }



        var prep = StatsDesignCementShellBuilder.PickBestPrepMesh(prepMeshes, marginLoop);

        return StatsDesignCementShellBuilder.BuildOpenInnerSurface(prep, marginLoop, settings);

    }



    public static IReadOnlyList<MeshSnapshot> CollectPrepSnapshots(

        IEnumerable<(MeshSnapshot Snapshot, ViewModels.MeshCategory Category, bool IsVisible, bool IsLoadFailed, string FilePath)> meshes,

        IReadOnlyList<Point3D>? marginLoop = null) =>

        StatsDesignPrepMeshResolver.CollectSnapshots(meshes, marginLoop);



    public static bool IsInnerShellPath(string? filePath) =>

        !string.IsNullOrWhiteSpace(filePath) &&

        filePath.Contains("_inner", StringComparison.OrdinalIgnoreCase) &&

        filePath.EndsWith(".stl", StringComparison.OrdinalIgnoreCase);



    public static bool IsCrownPath(string? filePath) =>

        !string.IsNullOrWhiteSpace(filePath) &&

        filePath.Contains("_crown", StringComparison.OrdinalIgnoreCase) &&

        filePath.EndsWith(".stl", StringComparison.OrdinalIgnoreCase);

}

