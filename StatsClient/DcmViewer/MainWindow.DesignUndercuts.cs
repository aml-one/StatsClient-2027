using System.Windows.Media.Media3D;
using DCMViewer.Services;
using HelixToolkit.Maths;
using HelixToolkit.Wpf.SharpDX;
using Color4 = HelixToolkit.Maths.Color4;

namespace DCMViewer;

public partial class MainWindow
{
    private readonly List<MeshGeometryModel3D> _undercutHotspotMarkers = [];

    private void UpdateUndercutHotspotVisuals()
    {
        ClearUndercutHotspotMarkers();
        if (!_viewModel.IsDesignHostMode)
        {
            return;
        }

        const double markerRadiusMm = 0.45;
        var markerColor = new Color4(1.0f, 0.45f, 0.1f, 0.95f);
        var material = new PhongMaterial
        {
            DiffuseColor = markerColor,
            AmbientColor = markerColor,
            EmissiveColor = markerColor * 0.35f,
            SpecularColor = new Color4(1f, 1f, 1f, 0.35f),
            SpecularShininess = 12f
        };

        foreach (var point in _viewModel.UndercutHotspotPoints)
        {
            var marker = new MeshGeometryModel3D
            {
                Geometry = SharpDxMeshFactory.CreateSphereGeometry(point, markerRadiusMm),
                Material = material,
                IsHitTestVisible = false,
                RenderOrder = 4800
            };

            Viewport.Items.Add(marker);
            _undercutHotspotMarkers.Add(marker);
        }
    }

    private void ClearUndercutHotspotMarkers()
    {
        foreach (var marker in _undercutHotspotMarkers)
        {
            Viewport.Items.Remove(marker);
        }

        _undercutHotspotMarkers.Clear();
    }
}
