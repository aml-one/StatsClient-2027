using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

/// <summary>User-indicated working (inner) side for one mesh in a unified-shell fuse.</summary>
public sealed record FuseInnerSideHint(
    int MeshIndex,
    Point3D Point,
    Vector3D PreferredNormal);

public sealed record FuseInnerSidePickItem(
    int MeshIndex,
    string DisplayName,
    ViewModels.LoadedMeshItemViewModel Target);
