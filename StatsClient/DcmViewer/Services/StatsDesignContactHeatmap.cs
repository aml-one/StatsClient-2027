using HelixToolkit.Maths;
using System.Windows.Media.Media3D;
using Color4 = HelixToolkit.Maths.Color4;

namespace DCMViewer.Services;

/// <summary>Per-vertex proximity colors for crown contact review (red = tight, green = ideal, blue = open).</summary>
internal static class StatsDesignContactHeatmap
{
    public static Color4[] BuildVertexColors(
        MeshSnapshot crownMesh,
        IReadOnlyList<MeshSnapshot> contactMeshes,
        double penetratingDistMm = 0.02,
        double idealBandMaxMm = 0.20,
        double maxSearchDistMm = 2.0)
    {
        ArgumentNullException.ThrowIfNull(crownMesh);
        if (contactMeshes.Count == 0)
        {
            return Enumerable.Repeat(new Color4(0.9f, 0.9f, 0.9f, 1f), crownMesh.Positions.Length).ToArray();
        }

        var colors = new Color4[crownMesh.Positions.Length];
        var idealMax = Math.Max(penetratingDistMm + 0.01, idealBandMaxMm);
        var searchMax = Math.Max(idealMax + 0.01, maxSearchDistMm);
        var contactIndices = contactMeshes.Select(mesh => new StatsDesignMeshSpatialIndex(mesh)).ToArray();

        for (var index = 0; index < crownMesh.Positions.Length; index++)
        {
            var distance = StatsDesignMeshProximity.ClosestDistanceToMeshes(
                crownMesh.Positions[index],
                contactMeshes,
                contactIndices);
            colors[index] = MapDistanceToColor(distance, penetratingDistMm, idealMax, searchMax);
        }

        return colors;
    }

    public static string Summarize(MeshSnapshot crownMesh, IReadOnlyList<MeshSnapshot> contactMeshes)
    {
        if (contactMeshes.Count == 0)
        {
            return "Load adjacent/opposing scans to review contacts.";
        }

        var penetrating = 0;
        var ideal = 0;
        var open = 0;
        const double penetratingDistMm = 0.02;
        const double idealMaxMm = 0.20;

        var contactIndices = contactMeshes.Select(mesh => new StatsDesignMeshSpatialIndex(mesh)).ToArray();
        foreach (var vertex in crownMesh.Positions)
        {
            var distance = StatsDesignMeshProximity.ClosestDistanceToMeshes(vertex, contactMeshes, contactIndices);
            if (distance <= penetratingDistMm)
            {
                penetrating++;
            }
            else if (distance < idealMaxMm)
            {
                ideal++;
            }
            else
            {
                open++;
            }
        }

        var total = Math.Max(1, crownMesh.Positions.Length);
        return $"Contact: {ideal * 100.0 / total:F0}% ideal, {penetrating * 100.0 / total:F0}% tight, {open * 100.0 / total:F0}% open (vs scans).";
    }

    private static Color4 MapDistanceToColor(double distanceMm, double penetratingDistMm, double idealMaxMm, double searchMaxMm)
    {
        if (distanceMm <= penetratingDistMm)
        {
            return new Color4(1.0f, 0.1f, 0.1f, 1f);
        }

        if (distanceMm < idealMaxMm)
        {
            return new Color4(0.1f, 0.95f, 0.25f, 1f);
        }

        if (distanceMm < searchMaxMm)
        {
            var t = (float)((distanceMm - idealMaxMm) / (searchMaxMm - idealMaxMm));
            return Color4.Lerp(new Color4(0.1f, 0.95f, 0.25f, 1f), new Color4(0.15f, 0.35f, 1.0f, 1f), t);
        }

        return new Color4(0.9f, 0.9f, 0.9f, 1f);
    }
}
