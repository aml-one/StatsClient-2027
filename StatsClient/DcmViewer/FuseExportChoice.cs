using DCMViewer.Services;

namespace DCMViewer;

internal sealed record FuseExportChoice(
    MeshFuseMode Mode,
    bool CleanupSavedStlArtifacts,
    int CleanupStrength);
