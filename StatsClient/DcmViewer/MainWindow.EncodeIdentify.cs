using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using DCMViewer.Services;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Color4 = HelixToolkit.Maths.Color4;
using HelixProjectionCamera = HelixToolkit.Wpf.SharpDX.ProjectionCamera;

namespace DCMViewer;

public partial class MainWindow
{
    private static readonly Color4[] EncodePickColors =
    [
        new(1.0f, 0.2f, 0.2f, 1f),
        new(0.2f, 1.0f, 0.35f, 1f),
        new(0.35f, 0.7f, 1.0f, 1f)
    ];

    private readonly List<Point3D> _encodePickPoints = [];
    private readonly List<MeshGeometryModel3D> _encodePickMarkers = [];
    private bool _isEncodeIdentifyPicking;
    private bool _isEncodeIdentifyRunning;
    private CancellationTokenSource? _encodeIdentifyCts;

    public bool IsEncodeIdentifyActive => _isEncodeIdentifyPicking || _isEncodeIdentifyRunning;

    public void BeginEncodeIdentifyPicking()
    {
        if (_isEncodeIdentifyRunning)
        {
            return;
        }

        CancelEncodeIdentifyPicking();
        _isEncodeIdentifyPicking = true;
        _viewModel.UpdateEncodeWorkflowStatus("Encode identify: click 3 points on the healing cap top (1/3).");
    }

    public void CancelEncodeIdentifyPicking()
    {
        _isEncodeIdentifyPicking = false;
        try { _encodeIdentifyCts?.Cancel(); } catch { }
        _encodeIdentifyCts?.Dispose();
        _encodeIdentifyCts = null;
        _encodePickPoints.Clear();
        ClearEncodePickMarkers();
        if (!_isEncodeIdentifyRunning)
        {
            _viewModel.UpdateEncodeWorkflowStatus("Ready.");
        }
    }

    public async Task<EncodeCapIdentifyResult?> RunEncodeIdentifyAfterPicksAsync()
    {
        if (_encodePickPoints.Count < 3)
        {
            return null;
        }

        _isEncodeIdentifyRunning = true;
        _isEncodeIdentifyPicking = false;

        try
        {
            _viewModel.UpdateEncodeWorkflowStatus("Encode identify: aligning view and measuring…");
            _viewModel.SetEncodeWorkflowBusy(true);

            if (!EncodeCapGeometryHelper.TryBuildCapFrame(
                    _encodePickPoints[0],
                    _encodePickPoints[1],
                    _encodePickPoints[2],
                    out var center,
                    out var capAxis,
                    out var inPlaneReference,
                    out _,
                    out var pickRadius))
            {
                return new EncodeCapIdentifyResult
                {
                    Success = false,
                    ErrorMessage = "The three points do not define a valid plane. Pick three distinct points on the cap top."
                };
            }

            _sectionVisualRadius = Math.Max(pickRadius * 0.85, 2.0);
            _sectionTravelRange = Math.Max(pickRadius * 0.5, 0.5);

            _viewModel.EnsureSectionModeForEncode();
            _sectionCenter = center;
            _viewModel.SectionOffset = 0;

            var rawDiameters = new List<double>();
            var cutDetails = new List<string>();
            var cutSnapshots = new List<EncodeMeasureCutSnapshot>();
            Point? sectionStart = null;
            Point? sectionEnd = null;
            _encodeSectionMeasureStyle = true;
            ShowEncodeMeasureThumbnails(null);

            var profileUp = GetSectionProfileUpDirection();
            foreach (var angle in new[] { 0.0, 120.0, 240.0 })
            {
                _sectionNormal = EncodeCapGeometryHelper.GetDiameterSectionNormal(capAxis, inPlaneReference, angle);
                UpdateSectionPlane();
                await WaitForRenderAsync(1);

                var cutOk = EncodeCapGeometryHelper.TryMeasureCapDiameterMm(
                    _viewModel.LoadedFiles,
                    center,
                    _sectionNormal,
                    profileUp,
                    pickRadius,
                    out var cutDiameterMm,
                    out var cutStart,
                    out var cutEnd);

                if (cutOk)
                {
                    _viewModel.SetSectionMeasurementPoints(cutStart, cutEnd);
                }

                UpdateSectionProfileView(_sectionCenter, _sectionNormal, resetCanvasTransform: false);
                UpdateMeasurementVisual();
                await WaitForRenderAsync(1);

                var cutPng = ViewportCaptureHelper.CapturePng(SectionProfileGraphHost, 240, 180);
                cutSnapshots.Add(new EncodeMeasureCutSnapshot
                {
                    AngleDegrees = angle,
                    DiameterMm = cutOk ? cutDiameterMm : 0,
                    Succeeded = cutOk,
                    Png = cutPng
                });

                if (cutOk)
                {
                    rawDiameters.Add(cutDiameterMm);
                    cutDetails.Add($"{angle:F0}°: {cutDiameterMm:F2} mm");
                    sectionStart = cutStart;
                    sectionEnd = cutEnd;
                }
                else
                {
                    cutDetails.Add($"{angle:F0}°: failed");
                }
            }

            ShowEncodeMeasureThumbnails(cutSnapshots);

            if (rawDiameters.Count == 0)
            {
                _sectionNormal = EncodeCapGeometryHelper.GetDiameterSectionNormal(capAxis, inPlaneReference, 0);
                UpdateSectionPlane();
                if (EncodeCapGeometryHelper.TryMeasureCapDiameterMm(
                        _viewModel.LoadedFiles,
                        center,
                        _sectionNormal,
                        profileUp,
                        pickRadius * 2.5,
                        out var fallbackDiameter,
                        out var fallbackStart,
                        out var fallbackEnd))
                {
                    rawDiameters.Add(fallbackDiameter);
                    cutDetails.Add($"fallback: {fallbackDiameter:F2} mm (wider ROI)");
                    sectionStart = fallbackStart;
                    sectionEnd = fallbackEnd;
                }
            }

            var measureSummary = string.Join("; ", cutDetails);

            if (rawDiameters.Count == 0)
            {
                return CopyResultWithSnapshots(
                    new EncodeCapIdentifyResult
                    {
                        Success = false,
                        ErrorMessage =
                            "Could not measure the healing cap diameter. Place all 3 points on the cap top (not on teeth or gingiva), then reload the case if scans look misaligned."
                    },
                    cutSnapshots,
                    measureSummary);
            }

            var diameterMm = EncodeCapDiameterResolver.Resolve(rawDiameters, out var resolveSummary);
            measureSummary += " | " + resolveSummary;

            if (sectionStart is not null && sectionEnd is not null)
            {
                _viewModel.SetSectionMeasurementPoints(sectionStart.Value, sectionEnd.Value);
                UpdateMeasurementVisual();
            }

            EncodeCapGeometryHelper.FrameCameraOnCap(Viewport, center, capAxis, pickRadius);
            ConfigureRotationBehavior();
            SyncLightingToCamera();
            UpdateZoomPercentLabel();

            await WaitForRenderAsync(2);

            _viewModel.UpdateEncodeWorkflowStatus("Encode identify: capturing image and calling AI…");
            var png = ViewportCaptureHelper.CapturePng(Viewport, 1280, 960);
            if (png.Length == 0)
            {
                return CopyResultWithSnapshots(
                    new EncodeCapIdentifyResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to capture viewport image."
                    },
                    cutSnapshots,
                    measureSummary);
            }

            _encodeIdentifyCts?.Dispose();
            _encodeIdentifyCts = new CancellationTokenSource();
            var result = CopyResultWithSnapshots(
                await EncodeIdentifyService.IdentifyFromScreenshotAsync(png, diameterMm, _encodeIdentifyCts.Token),
                cutSnapshots,
                measureSummary);

            _viewModel.UpdateEncodeWorkflowStatus(result.Success
                ? $"Encode: {result.ThreeShapeSuggestion} (Ø {diameterMm:F2} mm)"
                : $"Encode identify failed: {result.ErrorMessage}");
            return result;
        }
        finally
        {
            _encodeSectionMeasureStyle = false;
            _encodeIdentifyCts?.Dispose();
            _encodeIdentifyCts = null;
            _viewModel.SetEncodeWorkflowBusy(false);
            _isEncodeIdentifyRunning = false;
            _encodePickPoints.Clear();
            ClearEncodePickMarkers();
        }
    }

    private static EncodeCapIdentifyResult CopyResultWithSnapshots(
        EncodeCapIdentifyResult result,
        IReadOnlyList<EncodeMeasureCutSnapshot> cutSnapshots,
        string? measurementSummary = null) =>
        new()
        {
            Success = result.Success,
            ErrorMessage = result.ErrorMessage,
            Profile = result.Profile,
            Family = result.Family,
            CenterGrooves = result.CenterGrooves,
            MeasuredDiameterMm = result.MeasuredDiameterMm,
            PlatformMm = result.PlatformMm,
            ThreeShapeSuggestion = result.ThreeShapeSuggestion,
            Confidence = result.Confidence,
            Notes = result.Notes,
            MeasurementSummary = measurementSummary ?? result.MeasurementSummary,
            CutSnapshots = cutSnapshots,
            VisionDebugLog = result.VisionDebugLog,
            VisionDebugLogFilePath = result.VisionDebugLogFilePath
        };

    private Vector3D GetSectionProfileUpDirection()
    {
        if (Viewport?.Camera is HelixProjectionCamera camera && camera.UpDirection.LengthSquared > 1e-9)
        {
            return camera.UpDirection;
        }

        return new Vector3D(0, 1, 0);
    }

    private void ShowEncodeMeasureThumbnails(IReadOnlyList<EncodeMeasureCutSnapshot>? cuts)
    {
        if (EncodeMeasureThumbnailsPanel is null)
        {
            return;
        }

        if (cuts is null || cuts.Count == 0)
        {
            EncodeMeasureThumbnailsPanel.Visibility = Visibility.Collapsed;
            SetEncodeCutThumbnail(EncodeCutImage0, EncodeCutLabel0, null);
            SetEncodeCutThumbnail(EncodeCutImage1, EncodeCutLabel1, null);
            SetEncodeCutThumbnail(EncodeCutImage2, EncodeCutLabel2, null);
            return;
        }

        EncodeMeasureThumbnailsPanel.Visibility = Visibility.Visible;
        var ordered = cuts.OrderBy(c => c.AngleDegrees).ToList();
        SetEncodeCutThumbnail(EncodeCutImage0, EncodeCutLabel0, ordered.ElementAtOrDefault(0));
        SetEncodeCutThumbnail(EncodeCutImage1, EncodeCutLabel1, ordered.ElementAtOrDefault(1));
        SetEncodeCutThumbnail(EncodeCutImage2, EncodeCutLabel2, ordered.ElementAtOrDefault(2));
    }

    private static void SetEncodeCutThumbnail(System.Windows.Controls.Image image, System.Windows.Controls.TextBlock label, EncodeMeasureCutSnapshot? cut)
    {
        if (cut is null)
        {
            image.Source = null;
            label.Text = string.Empty;
            return;
        }

        label.Text = cut.Succeeded
            ? $"{cut.AngleDegrees:F0}°  Ø {cut.DiameterMm:F2} mm"
            : $"{cut.AngleDegrees:F0}°  failed";

        if (cut.Png.Length == 0)
        {
            image.Source = null;
            return;
        }

        using var stream = new MemoryStream(cut.Png);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        image.Source = bitmap;
    }

    private void ClearEncodePickMarkers()
    {
        foreach (var marker in _encodePickMarkers)
        {
            Viewport.Items.Remove(marker);
        }

        _encodePickMarkers.Clear();
    }

    private void AddEncodePickMarker(Point3D center, int index)
    {
        const double markerRadiusMm = 0.55;
        var color = EncodePickColors[Math.Clamp(index, 0, EncodePickColors.Length - 1)];
        var geometry = SharpDxMeshFactory.CreateSphereGeometry(center, markerRadiusMm);
        var bright = new Color4(
            Math.Min(color.Red + 0.35f, 1f),
            Math.Min(color.Green + 0.35f, 1f),
            Math.Min(color.Blue + 0.35f, 1f),
            1f);
        var material = new PhongMaterial
        {
            DiffuseColor = bright,
            AmbientColor = bright,
            EmissiveColor = bright,
            SpecularColor = new Color4(1f, 1f, 1f, 0.5f),
            SpecularShininess = 16f
        };

        var marker = new MeshGeometryModel3D
        {
            Geometry = geometry,
            Material = material,
            IsHitTestVisible = false,
            RenderOrder = 5000
        };

        Viewport.Items.Add(marker);
        _encodePickMarkers.Add(marker);
    }

    private async Task WaitForRenderAsync(int frames = 1)
    {
        for (var i = 0; i < frames; i++)
        {
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Task.Delay(50);
        }
    }

    private bool TryHandleEncodeIdentifyPick(MouseButtonEventArgs e)
    {
        if (!_isEncodeIdentifyPicking || _isEncodeIdentifyRunning)
        {
            return false;
        }

        var hits = Viewport.FindHits(e.GetPosition(Viewport));
        var hit = hits.FirstOrDefault(h => h.ModelHit is MeshGeometryModel3D model && !_encodePickMarkers.Contains(model));
        if (hit is null)
        {
            _viewModel.UpdateEncodeWorkflowStatus("No mesh hit — click on the healing cap surface.");
            return true;
        }

        var point = new Point3D(hit.PointHit.X, hit.PointHit.Y, hit.PointHit.Z);
        var index = _encodePickPoints.Count;
        _encodePickPoints.Add(point);
        AddEncodePickMarker(point, index);

        var count = _encodePickPoints.Count;
        if (count < 3)
        {
            _viewModel.UpdateEncodeWorkflowStatus($"Encode identify: click 3 points on the healing cap top ({count}/3).");
            e.Handled = true;
            return true;
        }

        e.Handled = true;
        _ = CompleteEncodeIdentifyPicksAsync();
        return true;
    }

    private async Task CompleteEncodeIdentifyPicksAsync()
    {
        EncodeCapIdentifyCompleted?.Invoke(await RunEncodeIdentifyAfterPicksAsync());
    }

    /// <summary>Raised on UI thread when identify workflow finishes (including failures).</summary>
    public event Action<EncodeCapIdentifyResult?>? EncodeCapIdentifyCompleted;
}
