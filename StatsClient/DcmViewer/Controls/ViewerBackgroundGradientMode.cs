namespace DCMViewer.Controls;

/// <summary>
/// Supported background gradient layouts for <see cref="DcmViewerCanvasComponent"/>.
/// </summary>
public enum ViewerBackgroundGradientMode
{
    /// <summary>
    /// Radial gradient centered in the viewer area.
    /// </summary>
    Radial = 0,

    /// <summary>
    /// Linear gradient flowing from left to right.
    /// </summary>
    LinearHorizontal = 1,

    /// <summary>
    /// Linear gradient flowing from top to bottom.
    /// </summary>
    LinearVertical = 2
}
