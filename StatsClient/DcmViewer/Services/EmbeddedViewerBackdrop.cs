using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HelixToolkit.Wpf.SharpDX;

namespace DCMViewer.Services;

/// <summary>
/// Shared Order Info Overview backdrop — same brush on the tab grid and embedded 3D canvas.
/// </summary>
internal static class EmbeddedViewerBackdrop
{
    /// <summary>Center / D3D clear tone (matches gradient center stop).</summary>
    public static readonly Color HostRenderClearColor = Color.FromRgb(229, 233, 237);

    /// <summary>Identical radial gradient for Overview tab grid and embedded viewer canvas.</summary>
    public static RadialGradientBrush CreateOrderInfoOverviewBrush()
    {
        var brush = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.58, 0.5),
            Center = new Point(0.58, 0.5),
            RadiusX = 0.92,
            RadiusY = 0.88
        };
        brush.GradientStops.Add(new GradientStop(HostRenderClearColor, 0.0));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(227, 231, 234), 0.35));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(216, 220, 224), 0.7));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(200, 204, 208), 1.0));
        brush.Freeze();
        return brush;
    }

    public static void ApplyHostCanvasBackdrop(Panel watermarkCanvas)
    {
        watermarkCanvas.Background = CreateOrderInfoOverviewBrush();
    }

    public static void ApplyEmbeddedRenderClear(Viewport3DX viewport)
    {
        viewport.Background = Brushes.Transparent;
        viewport.BackgroundColor = HostRenderClearColor;
        viewport.EnableSwapChainRendering = false;
    }

    public static void ApplyEmbeddedHostAppearance(Panel watermarkCanvas, Viewport3DX viewport)
    {
        ApplyHostCanvasBackdrop(watermarkCanvas);
        ApplyEmbeddedRenderClear(viewport);
    }
}
