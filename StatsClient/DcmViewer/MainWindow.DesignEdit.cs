using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using DCMViewer.Services;
using DCMViewer.ViewModels;
using HelixToolkit.Wpf.SharpDX;
using HelixProjectionCamera = HelixToolkit.Wpf.SharpDX.ProjectionCamera;

namespace DCMViewer;

public partial class MainWindow
{
    private Point3D _activeCutPlanePoint;
    private Vector3D _activeCutPlaneNormal = new(0, 0, 1);
    private Point3D _cutPlaneCenter;
    private Vector3D _cutPlaneNormal = new(0, 0, 1);
    private double _cutPlaneVisualRadius = 12;
    private double _cutPlaneTravelRange = 10;

    private void ApplyDesignEditToolsPanelLayout()
    {
        if (FindName("DesignEditToolsPanel") is not FrameworkElement panel)
        {
            return;
        }

        // Sit immediately left of the vertical toolbar (same slot as the legacy sculpt panel).
        const double toolbarColumnWidth = 48;
        const double orderInfoRightInset = 212;
        panel.Margin = IsEmbeddedHost && HostMode != ViewerHostMode.Design
            ? new Thickness(0, 10, orderInfoRightInset + toolbarColumnWidth, 0)
            : new Thickness(0, 8, 10 + toolbarColumnWidth, 0);
    }

    private void ApplyDesignEditModeFromViewModel()
    {
        ApplyDesignEditToolsPanelLayout();

        if (FindName("DesignEditToolsPanel") is FrameworkElement designEditToolsPanel)
        {
            // Order Info hosts design-edit UI in its right glass column, not here.
            var showFloatingPanel = _viewModel.ShowDesignEditPanel && !_viewModel.IsOrderInfoHostMode;
            designEditToolsPanel.Visibility = showFloatingPanel ? Visibility.Visible : Visibility.Collapsed;
        }

        if (FindName("SculptToolPanel") is FrameworkElement sculptToolPanel)
        {
            var showFloatingSculpt = _viewModel.IsSculptMode && !_viewModel.IsDesignEditMode && !_viewModel.IsDesignHostMode;
            sculptToolPanel.Visibility = showFloatingSculpt ? Visibility.Visible : Visibility.Collapsed;
        }

        ApplyCutPlaneModeFromViewModel();
        if (!_viewModel.IsDesignEditMode)
        {
            ApplySculptModeFromViewModel();
        }

        UpdateToolButtonStates();
    }

    private void ApplyCutPlaneModeFromViewModel()
    {
        if (FindName("CutPlaneOffsetSlider") is UIElement slider)
        {
            slider.Visibility = _viewModel.IsCutPlaneMode ? Visibility.Visible : Visibility.Collapsed;
        }

        if (!_viewModel.IsCutPlaneMode)
        {
            ClearCutPlaneVisuals();
            return;
        }

        var bounds = GetVisibleBounds();
        if (!bounds.IsEmpty)
        {
            RefreshCutPlaneReferenceFromBounds(bounds);
        }

        UpdateCutPlaneVisual();
    }

    private bool TryPlaceCutPlaneFromClick(MouseButtonEventArgs e)
    {
        if (!_viewModel.IsDesignEditMode || !_viewModel.IsCutPlaneMode)
        {
            return false;
        }

        var hits = Viewport.FindHits(e.GetPosition(Viewport));
        foreach (var hit in hits)
        {
            if (hit.ModelHit is not MeshGeometryModel3D modelHit)
            {
                continue;
            }

            var target = _viewModel.FindLoadedFileByModel(modelHit);
            if (target is null || !target.IsVisible || !_viewModel.CanSculptInDesignEdit(target))
            {
                continue;
            }

            var hitPoint = new Point3D(hit.PointHit.X, hit.PointHit.Y, hit.PointHit.Z);
            var planeNormal = ResolveCutPlaneNormalFromCamera();
            _cutPlaneCenter = hitPoint;
            _cutPlaneNormal = planeNormal;
            _viewModel.CutPlaneOffset = 0;

            if (_viewModel.TryPlaceCutPlane(hitPoint, planeNormal, target))
            {
                UpdateCutPlaneVisual();
                e.Handled = true;
                return true;
            }
        }

        return false;
    }

    internal void UpdateCutPlaneFromViewModel()
    {
        if (!_viewModel.IsCutPlaneMode)
        {
            return;
        }

        UpdateCutPlaneVisual();
    }

    private void UpdateCutPlaneVisual()
    {
        if (!_viewModel.IsCutPlaneMode)
        {
            ClearCutPlaneVisuals();
            return;
        }

        var normal = _cutPlaneNormal;
        if (normal.LengthSquared < 1e-9)
        {
            normal = new Vector3D(0, 0, 1);
        }

        normal.Normalize();
        var offset = _viewModel.CutPlaneOffset * _cutPlaneTravelRange;
        var planePosition = _cutPlaneCenter + (normal * offset);
        _activeCutPlanePoint = planePosition;
        _activeCutPlaneNormal = normal;
        _viewModel.UpdateActiveCutPlane(planePosition, normal);

        // Intersection outline only — a full disk hides the side that will be kept.
        UpdateCutPlaneIntersectionVisual(planePosition, normal);
    }

    private void ClearCutPlaneVisuals()
    {
        ClearCutPlaneIntersectionVisual();
        if (!_viewModel.IsSectionMode)
        {
            ClearSectionPlaneVisuals();
        }
    }

    private void UpdateCutPlaneIntersectionVisual(Point3D planePosition, Vector3D planeNormal)
    {
        if (!_viewModel.IsCutPlaneMode ||
            !_viewModel.TryGetActiveCutPlaneTarget(out var target) ||
            target.MeshSnapshot is null)
        {
            ClearCutPlaneIntersectionVisual();
            return;
        }

        var segments = StatsDesignEditCutService.BuildPlaneIntersectionSegments(
            target.MeshSnapshot,
            planePosition,
            planeNormal);
        if (segments.Count == 0)
        {
            ClearCutPlaneIntersectionVisual();
            return;
        }

        var outlinePoints = new List<Point3D>(segments.Count * 2);
        foreach (var (a, b) in segments)
        {
            outlinePoints.Add(a);
            outlinePoints.Add(b);
        }

        CutPlaneIntersectionVisual.Geometry = SharpDxMeshFactory.CreateSegmentLineGeometry(outlinePoints);
        CutPlaneIntersectionVisual.Visibility = Visibility.Visible;
    }

    private void ClearCutPlaneIntersectionVisual()
    {
        CutPlaneIntersectionVisual.Geometry = null;
        CutPlaneIntersectionVisual.Visibility = Visibility.Collapsed;
    }

    private Vector3D ResolveCutPlaneNormalFromCamera()
    {
        if (Viewport.Camera is not HelixProjectionCamera camera)
        {
            return new Vector3D(1, 0, 0);
        }

        var look = camera.LookDirection;
        var up = camera.UpDirection;
        var perpendicularNormal = Vector3D.CrossProduct(look, up);
        if (perpendicularNormal.LengthSquared < 1e-9)
        {
            perpendicularNormal = Vector3D.CrossProduct(look, new Vector3D(0, 1, 0));
        }

        if (perpendicularNormal.LengthSquared < 1e-9)
        {
            perpendicularNormal = new Vector3D(1, 0, 0);
        }

        perpendicularNormal.Normalize();
        return perpendicularNormal;
    }

    private void RefreshCutPlaneReferenceFromBounds(Rect3D bounds)
    {
        _cutPlaneCenter = new Point3D(
            bounds.X + (bounds.SizeX / 2.0),
            bounds.Y + (bounds.SizeY / 2.0),
            bounds.Z + (bounds.SizeZ / 2.0));

        var maxSpan = Math.Max(bounds.SizeX, Math.Max(bounds.SizeY, bounds.SizeZ));
        var diagonal = new Vector3D(bounds.SizeX, bounds.SizeY, bounds.SizeZ).Length;
        _cutPlaneVisualRadius = Math.Max((diagonal * 0.5) * 1.2, Math.Max(maxSpan * 0.6, 8.0));
        _cutPlaneTravelRange = Math.Max(diagonal * 0.5, 1.0);
    }
}
