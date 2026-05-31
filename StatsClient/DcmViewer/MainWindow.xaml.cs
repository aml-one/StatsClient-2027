using StatsClient.MVVM.Core;
using System.IO;
using System.Globalization;
using System.Windows;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using DCMViewer.Services;
using DCMViewer.ViewModels;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using SharpDX;
using HelixProjectionCamera = HelixToolkit.Wpf.SharpDX.ProjectionCamera;
using HelixPerspectiveCamera = HelixToolkit.Wpf.SharpDX.PerspectiveCamera;
using HelixOrthographicCamera = HelixToolkit.Wpf.SharpDX.OrthographicCamera;
using Line = System.Windows.Shapes.Line;
using Ellipse = System.Windows.Shapes.Ellipse;

namespace DCMViewer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>When true, skips loading a demo scan on startup (embedded Order Info host).</summary>
    public bool SuppressStartupLoad { get; set; }

    /// <summary>When true, this window hosts its content inside Order Info instead of running standalone.</summary>
    public bool IsEmbeddedHost { get; set; }

    public MainViewModel ViewModel => _viewModel;

    private const double DefaultPerspectiveFieldOfView = 45.0;
    private const double ZoomExtentsPadding = 1.08;
    private const double EmbeddedDefaultZoomPercent = 66.0;
    private const double EmbeddedChromeLeftMargin = 420;
    private const double EmbeddedCoordinateSystemHorizontalPosition = -0.88;
    private const double EmbeddedViewCubeHorizontalPosition = 0.48;
    private const double EmbeddedSectionPanelLeftInset = 10;
    private const double EmbeddedSectionPanelBottomInset = 8;
    private const double EmbeddedModelVerticalViewBias = 0.14;
    private readonly MainViewModel _viewModel;

    private void BusyOverlayCancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        CancelEncodeIdentifyPicking();
        _viewModel.CancelBusyWork();
    }
    private readonly Brush _activeToolBrush = new SolidColorBrush(Color.FromRgb(214, 185, 92));
    private readonly Brush _toolbarButtonBackground = new SolidColorBrush(Color.FromRgb(237, 242, 248));
    private readonly Brush _activeToolForegroundBrush = new SolidColorBrush(Color.FromRgb(58, 49, 18));
    private Point3D _sectionCenter = new(0, 0, 0);
    private Vector3D _sectionNormal = new(0, 0, 1);
    private Point3D _activeSectionPlanePoint = new(0, 0, 0);
    private Vector3D _activeSectionPlaneNormal = new(0, 0, 1);
    private double _sectionTravelRange = 1.0;
    private double _sectionVisualRadius = 30.0;
    private bool _hasSectionProjectionMap;
    private double _sectionProjectionScale;
    private double _sectionProjectionCenterX;
    private double _sectionProjectionCenterY;
    private double _sectionProjectionCanvasCenterX;
    private double _sectionProjectionCanvasCenterY;
    private List<SectionSegment2D> _currentSectionSegments = new();
    private Matrix _canvasMatrix = Matrix.Identity;
    private bool _isCanvasPanning;
    private Point _canvasPanAnchor;
    private bool _isSectionPanelDragging;
    private Point _sectionPanelDragAnchor;
    private bool _isOrthographicView;
    private bool _pendingShowAllAfterModelUpdate;
    private bool _isNearClippingRelaxed = true;
    private bool _alwaysRelaxNearClipping = true;
    private double _defaultNearPlaneDistance = 0.1;
    private double _defaultFarPlaneDistance = 100000;
    private bool _isSectionRefreshQueued;
    private bool _isCompositionRenderingHooked;
    private bool _isSculptDragging;
    private LoadedMeshItemViewModel? _sculptTarget;
    private Point _sculptLastScreen;
    private Vector3D _sculptLastNormal = new(0, 0, 1);
    private Point? _sculptBrushPreviewScreen;
    private Point _sculptBrushPreviewLastRenderedScreen;
    private long _sculptBrushPreviewLastUpdateTicks;
    private const double SculptBrushPreviewMinMovePx = 2.5;
    private const long SculptBrushPreviewMinIntervalTicks = 25 * TimeSpan.TicksPerMillisecond;
    private Point3D _lastRenderedCameraPosition;
    private Vector3D _lastRenderedCameraLook;
    private Vector3D _lastRenderedCameraUp;
    private bool _hasLastRenderedCameraPose;
    private int _renderingFrameCounter;
    private string _lastZoomPercentLabel = string.Empty;
    private Brush? _defaultWatermarkBackground;
    private bool _encodeSectionMeasureStyle;

    public void RestoreEmbeddedInteraction()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            Viewport.UpdateLayout();
            Viewport.IsZoomEnabled = true;
            Viewport.IsPanEnabled = true;
            Viewport.IsRotationEnabled = true;
            ConfigureViewportGestures();
            ConfigureRotationBehavior();
            SyncLightingToCamera();
            UpdateZoomPercentLabel();
            Viewport.Focus();
            Keyboard.Focus(Viewport);
        }));
    }

    public async Task LoadCaseFilesAsync(IEnumerable<DCMFileItem> files, string? orderFolderPath = null)
    {
        var fileItems = files
            .Where(x => !string.IsNullOrWhiteSpace(x.FilePath) && File.Exists(x.FilePath))
            .GroupBy(x => Path.GetFullPath(x.FilePath), StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();

        _viewModel.SetSculptOrderFolder(orderFolderPath);
        _viewModel.TextureOverrides.Clear();
        _viewModel.CategoryOverrides.Clear();
        _viewModel.IsExternalFileDropEnabled = false;

        foreach (var item in fileItems)
        {
            ApplyFileOverrides(item);
        }

        try
        {
            await _viewModel.LoadFilesAsync(
                fileItems.Select(x => x.FilePath),
                clearExisting: true,
                cancellationToken: _viewModel.BusyCancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }

        var hiddenFiles = fileItems
            .Where(x => x.StartHidden)
            .Select(x => Path.GetFullPath(x.FilePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var loadedFile in _viewModel.LoadedFiles)
        {
            if (hiddenFiles.Contains(Path.GetFullPath(loadedFile.FilePath)))
            {
                loadedFile.IsVisible = false;
            }
        }

        RestoreEmbeddedInteraction();
        ScheduleTopRightOverlayFade();
        EnsureEmbeddedHostAppearance();
        _viewModel.ApplyPersistedSculptTree();
    }

    public async Task AddCaseFileAsync(DCMFileItem file)
    {
        if (string.IsNullOrWhiteSpace(file.FilePath) || !File.Exists(file.FilePath))
        {
            return;
        }

        var fullPath = Path.GetFullPath(file.FilePath);
        ApplyFileOverrides(file);

        var hiddenFile = file.StartHidden;
        await _viewModel.LoadFilesAsync(new[] { fullPath }, clearExisting: false);

        if (hiddenFile)
        {
            var loadedFile = _viewModel.LoadedFiles.FirstOrDefault(x =>
                string.Equals(Path.GetFullPath(x.FilePath), fullPath, StringComparison.OrdinalIgnoreCase));
            if (loadedFile is not null)
            {
                loadedFile.IsVisible = false;
            }
        }

        RestoreEmbeddedInteraction();
        ScheduleTopRightOverlayFade();
    }

    public void RemoveCaseFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        var fullPath = Path.GetFullPath(filePath);
        _viewModel.TextureOverrides.Remove(fullPath);
        _viewModel.CategoryOverrides.Remove(fullPath);
        _viewModel.ArchOverrides.Remove(fullPath);
        _viewModel.UnloadFile(fullPath);
        RestoreEmbeddedInteraction();
    }

    private void ApplyFileOverrides(DCMFileItem item)
    {
        var fullPath = Path.GetFullPath(item.FilePath);
        _viewModel.TextureOverrides[fullPath] = PrepScanMaterialRules.IsPreopScan(fullPath)
            ? PrepScanMaterialRules.TextureName
            : item.MaterialName;
        _viewModel.CategoryOverrides[fullPath] = item.SourceKind == DCMFileSourceKind.ModelScan
            ? "scan"
            : item.GroupName switch
            {
                "Model/Die" => "model",
                "Abutment" => "abutment",
                _ => "restoration"
            };
        _viewModel.ArchOverrides[fullPath] = ScanLayerArchResolver.Resolve(fullPath, item.GroupName);
    }

    private void ScheduleTopRightOverlayFade()
    {
        if (TopRightOverlayPanel is null)
        {
            return;
        }

        TopRightOverlayPanel.BeginAnimation(UIElement.OpacityProperty, null);
        TopRightOverlayPanel.Opacity = 1;

        var fadeAnimation = new DoubleAnimation
        {
            BeginTime = TimeSpan.FromSeconds(10),
            To = 0.15,
            Duration = TimeSpan.FromSeconds(0.9),
            FillBehavior = FillBehavior.HoldEnd
        };

        TopRightOverlayPanel.BeginAnimation(UIElement.OpacityProperty, fadeAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    public void PinViewerDataContext()
    {
        DataContext = _viewModel;
        WatermarkCanvas.DataContext = _viewModel;
        if (Viewport is not null)
        {
            Viewport.DataContext = _viewModel;
        }

        if (Resources["ViewerBindingProxy"] is BindingProxy proxy)
        {
            proxy.Data = _viewModel;
        }
    }

    public void AttachEmbeddedHost()
    {
        HookCompositionRendering();
        PinViewerDataContext();
        UpdateProjectionToggleButtonState();
        UpdateClippingToggleButtonState();
        UpdateHideLayerLabelsButtonState();
    }

    public async Task PrepareForHostUnloadAsync()
    {
        UnhookCompositionRendering();
        await _viewModel.LoadFilesAsync(Array.Empty<string>(), clearExisting: true);
    }

    public void ShutdownEmbeddedHost()
    {
        UnhookCompositionRendering();
        _viewModel.DisposeEffectsManager();
    }

    private void HookCompositionRendering()
    {
        if (_isCompositionRenderingHooked)
        {
            return;
        }

        CompositionTarget.Rendering += OnCompositionTargetRendering;
        _isCompositionRenderingHooked = true;
    }

    private void UnhookCompositionRendering()
    {
        if (!_isCompositionRenderingHooked)
        {
            return;
        }

        CompositionTarget.Rendering -= OnCompositionTargetRendering;
        _isCompositionRenderingHooked = false;
    }

    /// <summary>
    /// When hosted in Order Info, hides watermark chrome and applies the same backdrop as the Overview tab.
    /// </summary>
    public void SetCanvasBackgroundTransparent(bool hideWatermarkForHost)
    {
        if (!hideWatermarkForHost)
        {
            _defaultWatermarkBackground ??= WatermarkCanvas.Background;
            WatermarkCanvas.Background = _defaultWatermarkBackground;
            ApplyViewportClearTransparency();
        }
        else
        {
            EnsureEmbeddedHostAppearance();
        }

        PinViewerDataContext();

        var watermarkVisibility = hideWatermarkForHost ? Visibility.Collapsed : Visibility.Visible;
        WatermarkLogoImage.Visibility = watermarkVisibility;
        WatermarkTextBlock.Visibility = watermarkVisibility;
    }

    /// <summary>Order Info matching gradient + D3D clear tuned for transparent meshes.</summary>
    public void EnsureEmbeddedHostAppearance()
    {
        if (!IsEmbeddedHost)
        {
            return;
        }

        EmbeddedViewerBackdrop.ApplyEmbeddedHostAppearance(WatermarkCanvas, Viewport);
        if (Content is Grid hostRoot)
        {
            hostRoot.Background = ColorSchemeResourceCatalog.GetBrush("TransparentBrush");
        }

        // Leave room for Order Info left/right overlay panels on full-bleed canvas.
        TopRightChromeGrid.Margin = new Thickness(0, 10, 212, 0);
        ZoomBadgeBorder.Margin = new Thickness(EmbeddedChromeLeftMargin, 10, 0, 0);
        BottomStatusBorder.Margin = new Thickness(EmbeddedChromeLeftMargin, 0, 212, 8);

        ApplyEmbeddedViewportChromeLayout();

        ConfigureTransparencyRendering();
    }

    private void ApplyEmbeddedViewportChromeLayout()
    {
        Viewport.ZoomExtentsWhenLoaded = false;
        Viewport.CoordinateSystemHorizontalPosition = EmbeddedCoordinateSystemHorizontalPosition;
        Viewport.CoordinateSystemVerticalPosition = -0.8;
        Viewport.ViewCubeHorizontalPosition = EmbeddedViewCubeHorizontalPosition;
        Viewport.ViewCubeVerticalPosition = -0.8;
    }

    /// <summary>
    /// Sets camera distance from our fit formula so the zoom label matches <see cref="EmbeddedDefaultZoomPercent"/>.
    /// </summary>
    private void ApplyEmbeddedDefaultZoom()
    {
        if (!IsEmbeddedHost || Viewport.Camera is not HelixProjectionCamera camera)
        {
            return;
        }

        var bounds = GetVisibleBounds();
        if (bounds.IsEmpty)
        {
            return;
        }

        _viewModel.ApplyFacialCameraToVisibleMeshes();

        var center = GetEmbeddedLookAtCenter(bounds);

        var zoomScale = EmbeddedDefaultZoomPercent / 100.0;
        if (zoomScale <= 0)
        {
            return;
        }

        var look = _viewModel.CameraLookDirection;
        if (look.LengthSquared < 1e-9)
        {
            look = new Vector3D(0, 0, -1);
        }
        else
        {
            look.Normalize();
        }

        var up = _viewModel.CameraUpDirection;
        if (up.LengthSquared < 1e-9)
        {
            up = new Vector3D(0, 1, 0);
        }

        if (camera is HelixOrthographicCamera orthographicCamera)
        {
            var fitWidth = CalculateOrthographicFitWidth(bounds, look, up);
            orthographicCamera.Width = fitWidth / zoomScale;
            return;
        }

        var fieldOfView = camera is HelixPerspectiveCamera perspective
            ? perspective.FieldOfView
            : DefaultPerspectiveFieldOfView;
        var fitDistance = CalculatePerspectiveFitDistance(
            bounds,
            look,
            up,
            fieldOfView,
            Viewport.ActualWidth,
            Viewport.ActualHeight);
        var targetDistance = fitDistance / zoomScale;
        camera.Position = center - (look * targetDistance);
        camera.LookDirection = center - camera.Position;
    }

    private Point3D GetEmbeddedLookAtCenter(Rect3D bounds)
    {
        var center = new Point3D(
            bounds.X + (bounds.SizeX / 2.0),
            bounds.Y + (bounds.SizeY / 2.0),
            bounds.Z + (bounds.SizeZ / 2.0));

        if (!IsEmbeddedHost || Viewport.Camera is not HelixProjectionCamera camera)
        {
            return center;
        }

        var up = camera.UpDirection;
        if (up.LengthSquared < 1e-9)
        {
            center.Y += bounds.SizeY * EmbeddedModelVerticalViewBias;
            return center;
        }

        up.Normalize();
        center += up * (bounds.SizeY * EmbeddedModelVerticalViewBias);
        return center;
    }

    private void ApplyEmbeddedInitialView()
    {
        if (!IsEmbeddedHost)
        {
            return;
        }

        void Apply()
        {
            if (GetVisibleBounds().IsEmpty)
            {
                return;
            }

            ApplyEmbeddedDefaultZoom();
            ConfigureRotationBehavior();
            SyncLightingToCamera();
            UpdateZoomPercentLabel();
        }

        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, Apply);
        Dispatcher.BeginInvoke(DispatcherPriority.Render, Apply);
    }

    public void EnsureEmbeddedHostCanvasTransparent() => EnsureEmbeddedHostAppearance();

    private void ApplyViewportClearTransparency()
    {
        // WPF Colors.Transparent is #00FFFFFF; Helix/DX11 needs #00000000 for a true clear.
        Viewport.BackgroundColor = Color.FromArgb(0, 0, 0, 0);
        Viewport.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        Viewport.EnableSwapChainRendering = false;
        ConfigureTransparencyRendering();
    }

    private void ConfigureTransparencyRendering()
    {
        Viewport.EnableRenderOrder = true;
    }

    private void ConfigureViewportGestures()
    {
        // Helix 3.x defaults pan to right-click+shift; WPF viewer used middle-click pan (PanGesture2).
        if (!Viewport.InputBindings.OfType<MouseBinding>().Any(b =>
                b.Command == ViewportCommands.Pan &&
                b.Gesture is MouseGesture { MouseAction: MouseAction.MiddleClick }))
        {
            Viewport.InputBindings.Add(new MouseBinding(
                ViewportCommands.Pan,
                new MouseGesture(MouseAction.MiddleClick)));
        }
    }

    public MainWindow()
        : this(suppressStartupLoad: false, isEmbeddedHost: false)
    {
    }

    public MainWindow(bool suppressStartupLoad, bool isEmbeddedHost)
    {
        SuppressStartupLoad = suppressStartupLoad;
        IsEmbeddedHost = isEmbeddedHost;

        InitializeComponent();
        EnsureSectionPlaneFillMaterial();

        if (isEmbeddedHost)
        {
            Background = ColorSchemeResourceCatalog.GetBrush("TransparentBrush");
            ShowInTaskbar = false;
            ShowActivated = false;
            ConfigureViewportGestures();
            EnsureEmbeddedHostAppearance();
            WatermarkCanvas.SizeChanged += WatermarkCanvas_OnSizeChanged;
        }
        else
        {
            _defaultWatermarkBackground = WatermarkCanvas.Background;
            ApplyViewportClearTransparency();
            ConfigureViewportGestures();
        }

        // Log render tier — written to %ProgramData%\Stats_Client\render-info.log (always writable).
        // Tier 0 = software only, Tier 1 = partial HW, Tier 2 = full DirectX HW acceleration.
        int renderTier = RenderCapability.Tier >> 16;
        bool isRemoteSession = System.Windows.SystemParameters.IsRemotelyControlled;
        bool scalingOverrideApplied = renderTier < 2;
        try
        {
            string logFolder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Stats_Client");
            System.IO.Directory.CreateDirectory(logFolder);
            string logPath = System.IO.Path.Combine(logFolder, "render-info.log");
            System.IO.File.AppendAllLines(logPath, new[]
            {
                $"[DCMViewer] Render tier: {renderTier}  (0=SW, 1=partial HW, 2=full HW)",
                $"[DCMViewer] IsRemoteSession: {isRemoteSession}",
                $"[DCMViewer] BitmapScalingMode override applied: {scalingOverrideApplied}",
            });
        }
        catch { /* never crash over a log write */ }

        // Also push into the in-app debug log so it shows in the Debug panel without needing a file.
        StatsClient.MVVM.ViewModel.MainViewModel.Instance?.AddDebugLine(
            message: $"Render tier: {renderTier} | Remote: {isRemoteSession} | ScalingOverride: {scalingOverrideApplied}",
            location: "DCMViewer-HW");

        // On Tier 0 / remote sessions prefer NearestNeighbor scaling – much faster than the
        // default Fant/HighQuality resampler when compositing 3D viewport frames.
        ApplyRenderTierSettings();

        // Re-apply whenever WPF detects a render-tier change (e.g. RDP session attach/detach).
        RenderCapability.TierChanged += OnRenderTierChanged;
        Closed += (_, _) => RenderCapability.TierChanged -= OnRenderTierChanged;

        _viewModel = new MainViewModel(new DcmParser());
        if (isEmbeddedHost)
        {
            _viewModel.SuppressAutomaticCameraFraming = true;
        }

        _viewModel.PropertyChanged += ViewModelOnPropertyChanged;
        _viewModel.LoadedFiles.CollectionChanged += LoadedFilesOnCollectionChanged;
        DataContext = _viewModel;
        _viewModel.FuseInnerSidePickHandler = PickFuseInnerSideHintsAsync;
        PinViewerDataContext();
        SetDefaultProjectionMode();

        if (FindName("ProjectionToggleButton") is Button projectionToggleButton)
        {
            projectionToggleButton.Click += ProjectionToggleButton_OnClick;
        }

        if (!IsEmbeddedHost)
        {
            UpdateProjectionToggleButtonState();
            UpdateClippingToggleButtonState();
        }

        UpdateHideLayerLabelsButtonState();

        if (Viewport.Camera is HelixProjectionCamera initialCamera)
        {
            BindCameraPose(initialCamera);
        }

        if (!IsEmbeddedHost)
        {
            ConfigureRotationBehavior();
            HookCompositionRendering();
        }

        Closed += (_, _) =>
        {
            UnhookCompositionRendering();
            if (!IsEmbeddedHost)
            {
                _viewModel.DisposeEffectsManager();
            }
        };
        SyncLightingToCamera();
        UpdateZoomPercentLabel();
        Viewport.MouseLeftButtonDown += Viewport_OnMouseLeftButtonDown;
        Viewport.MouseMove += Viewport_OnMouseMove;
        Viewport.MouseLeftButtonUp += Viewport_OnMouseLeftButtonUp;
        Viewport.MouseLeave += Viewport_OnMouseLeave;
        Viewport.LostMouseCapture += Viewport_OnLostMouseCapture;
        SectionProfileCanvas.SizeChanged += (_, _) =>
        {
            if (_viewModel.IsSectionMode)
            {
                UpdateSectionProfileView(_activeSectionPlanePoint, _activeSectionPlaneNormal, resetCanvasTransform: false);
            }
        };

        if (!SuppressStartupLoad)
        {
            var startupScanPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "scan.dcm"));
            if (File.Exists(startupScanPath))
            {
                _ = _viewModel.LoadFileAsync(startupScanPath);
            }
        }
    }

    private void ApplyRenderTierSettings()
    {
        int tier = RenderCapability.Tier >> 16;
        bool isRemote = System.Windows.SystemParameters.IsRemotelyControlled;
        bool applyOverride = tier < 2;

        if (applyOverride)
        {
            RenderOptions.SetBitmapScalingMode(Viewport, BitmapScalingMode.NearestNeighbor);
        }
        else
        {
            RenderOptions.SetBitmapScalingMode(Viewport, BitmapScalingMode.HighQuality);
        }

        string msg = $"TierChanged → tier:{tier} remote:{isRemote} override:{applyOverride}";
        try
        {
            string logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Stats_Client", "render-info.log");
            System.IO.File.AppendAllLines(logPath, [$"[DCMViewer] {msg}"]);
        }
        catch { }
        StatsClient.MVVM.ViewModel.MainViewModel.Instance?.AddDebugLine(message: msg, location: "DCMViewer-HW");
    }

    private void OnRenderTierChanged(object? sender, EventArgs e) => ApplyRenderTierSettings();

    private void SetDefaultProjectionMode()
{
        if (Viewport.Camera is not HelixProjectionCamera currentCamera)
        {
            return;
        }

        var fieldOfView = currentCamera is HelixPerspectiveCamera perspective
            ? perspective.FieldOfView
            : DefaultPerspectiveFieldOfView;
        var orthographicWidth = CalculateOrthographicWidth(currentCamera.LookDirection.Length, fieldOfView);

        var orthographicCamera = new HelixOrthographicCamera
        {
            Position = currentCamera.Position,
            LookDirection = currentCamera.LookDirection,
            UpDirection = currentCamera.UpDirection,
            Width = orthographicWidth,
            CreateLeftHandSystem = false
        };

        BindCameraPose(orthographicCamera);
        Viewport.Camera = orthographicCamera;
        _isOrthographicView = true;
        CaptureClipDefaults(orthographicCamera);
        ApplyClipMode(orthographicCamera);
    }

    private void Window_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (!_viewModel.IsExternalFileDropEnabled)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (!_viewModel.IsExternalFileDropEnabled)
        {
            return;
        }

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
        {
            return;
        }

        var loadableFiles = files
            .Where(File.Exists)
            .Where(path => path.EndsWith(".dcm", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".stl", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (loadableFiles.Length == 0)
        {
            return;
        }

        await _viewModel.LoadFilesAsync(loadableFiles, clearExisting: false);
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(MainViewModel.SceneItems), StringComparison.Ordinal))
        {
            ConfigureRotationBehavior();
            ScheduleTopRightOverlayFade();
            if (_viewModel.IsSectionMode)
            {
                QueueSectionRefresh();
            }

            if (_pendingShowAllAfterModelUpdate && !GetVisibleBounds().IsEmpty)
            {
                _pendingShowAllAfterModelUpdate = false;
                Dispatcher.BeginInvoke(
                    DispatcherPriority.Loaded,
                    new Action(() =>
                    {
                        if (IsEmbeddedHost)
                        {
                            ApplyEmbeddedInitialView();
                        }
                        else
                        {
                            ShowAllButton_Click(this, new RoutedEventArgs());
                        }
                    }));
            }
        }

        if (string.Equals(e.PropertyName, nameof(MainViewModel.IsSectionMode), StringComparison.Ordinal))
        {
            ApplySectionModeFromViewModel();
        }

        if (string.Equals(e.PropertyName, nameof(MainViewModel.IsSculptMode), StringComparison.Ordinal))
        {
            ApplySculptModeFromViewModel();
        }

        if (_viewModel.IsSculptMode &&
            (string.Equals(e.PropertyName, nameof(MainViewModel.SculptBrushRadiusMm), StringComparison.Ordinal) ||
             string.Equals(e.PropertyName, nameof(MainViewModel.SculptToolName), StringComparison.Ordinal)) &&
            _sculptBrushPreviewScreen is Point previewScreen)
        {
            UpdateSculptBrushPreview(previewScreen, force: true);
        }

        if (string.Equals(e.PropertyName, nameof(MainViewModel.SectionOffset), StringComparison.Ordinal) && _viewModel.IsSectionMode)
        {
            UpdateSectionPlane();
        }

        if (string.Equals(e.PropertyName, nameof(MainViewModel.MeasureStartSection), StringComparison.Ordinal) ||
            string.Equals(e.PropertyName, nameof(MainViewModel.MeasureEndSection), StringComparison.Ordinal))
        {
            UpdateMeasurementVisual();
        }

        if (IsEmbeddedHost &&
            string.Equals(e.PropertyName, nameof(MainViewModel.IsBusy), StringComparison.Ordinal) &&
            !_viewModel.IsBusy)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, ApplyEmbeddedInitialView);
        }

        if (string.Equals(e.PropertyName, nameof(MainViewModel.IsBusy), StringComparison.Ordinal))
        {
            if (_viewModel.IsBusy)
            {
                StopViewportCameraSpin();
                Viewport.IsRotationEnabled = false;
            }
            else
            {
                Viewport.IsRotationEnabled = true;
            }
        }

        if (string.Equals(e.PropertyName, nameof(MainViewModel.HideLayerLabels), StringComparison.Ordinal))
        {
            UpdateHideLayerLabelsButtonState();
        }
    }

    private void QueueSectionRefresh()
    {
        if (_isSectionRefreshQueued)
        {
            return;
        }

        _isSectionRefreshQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _isSectionRefreshQueued = false;
            if (!_viewModel.IsSectionMode)
            {
                return;
            }

            _viewModel.ApplySectionPlane(_activeSectionPlanePoint, _activeSectionPlaneNormal, _viewModel.IsSectionMode);
            UpdateSectionPlaneVisual(_activeSectionPlanePoint, _activeSectionPlaneNormal);
            UpdateSectionProfileView(_activeSectionPlanePoint, _activeSectionPlaneNormal, resetCanvasTransform: false);
        }));
    }

    private void LoadedFilesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (LoadedMeshItemViewModel item in e.NewItems)
            {
                item.PropertyChanged += OnLoadedFilePropertyChanged;
            }
        }

        if (e.OldItems is not null)
        {
            foreach (LoadedMeshItemViewModel item in e.OldItems)
            {
                item.PropertyChanged -= OnLoadedFilePropertyChanged;
            }
        }

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var item in _viewModel.LoadedFiles)
            {
                item.PropertyChanged += OnLoadedFilePropertyChanged;
            }
        }

        if (e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Reset)
        {
            _pendingShowAllAfterModelUpdate = true;
        }
    }

    private void OnLoadedFilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!IsEmbeddedHost)
        {
            return;
        }

        if (string.Equals(e.PropertyName, nameof(LoadedMeshItemViewModel.Opacity), StringComparison.Ordinal))
        {
            QueueEmbeddedHostAppearanceRefresh();
        }
    }

    private void QueueEmbeddedHostAppearanceRefresh()
    {
        if (!IsEmbeddedHost)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            if (IsEmbeddedHost && Viewport.IsVisible)
            {
                EnsureEmbeddedHostAppearance();
            }
        }, DispatcherPriority.Render);
    }

    private void StopViewportCameraSpin()
    {
        try
        {
            Viewport?.StopSpin();
        }
        catch
        {
            // Helix may throw if the viewport is not fully initialized yet.
        }
    }

    private void ConfigureRotationBehavior()
    {
        StopViewportCameraSpin();
        Viewport.PanCursor = Cursors.Arrow;
        Viewport.RotateCursor = Cursors.Arrow;

        var bounds = GetVisibleBounds();
        if (bounds.IsEmpty)
        {
            return;
        }

        var center = IsEmbeddedHost
            ? GetEmbeddedLookAtCenter(bounds)
            : new Point3D(
                bounds.X + (bounds.SizeX / 2.0),
                bounds.Y + (bounds.SizeY / 2.0),
                bounds.Z + (bounds.SizeZ / 2.0));

        Viewport.FixedRotationPointEnabled = true;
        Viewport.FixedRotationPoint = center;
        Viewport.RotateAroundMouseDownPoint = false;

        if (!_viewModel.IsSectionMode)
        {
            RefreshSectionReferenceFromBounds(bounds);
        }
        UpdateZoomPercentLabel();
    }

    private void ShowAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsEmbeddedHost)
        {
            ApplyEmbeddedInitialView();
            return;
        }

        Viewport.ZoomExtents(250);
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            ConfigureRotationBehavior();
            SyncLightingToCamera();
            UpdateZoomPercentLabel();
        });
    }

    private void ProjectionToggleButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Viewport.Camera is not HelixProjectionCamera currentCamera)
        {
            return;
        }

        if (_isOrthographicView)
        {
            var perspectiveCamera = new HelixPerspectiveCamera
            {
                Position = currentCamera.Position,
                LookDirection = currentCamera.LookDirection,
                UpDirection = currentCamera.UpDirection,
                FieldOfView = DefaultPerspectiveFieldOfView,
                CreateLeftHandSystem = false
            };

            BindCameraPose(perspectiveCamera);
            Viewport.Camera = perspectiveCamera;
            _isOrthographicView = false;
            CaptureClipDefaults(perspectiveCamera);
            ApplyClipMode(perspectiveCamera);
        }
        else
        {
            var fieldOfView = currentCamera is HelixPerspectiveCamera perspective ? perspective.FieldOfView : DefaultPerspectiveFieldOfView;
            var orthographicWidth = CalculateOrthographicWidth(currentCamera.LookDirection.Length, fieldOfView);

            var orthographicCamera = new HelixOrthographicCamera
            {
                Position = currentCamera.Position,
                LookDirection = currentCamera.LookDirection,
                UpDirection = currentCamera.UpDirection,
                Width = orthographicWidth
            };

            BindCameraPose(orthographicCamera);
            Viewport.Camera = orthographicCamera;
            _isOrthographicView = true;
            CaptureClipDefaults(orthographicCamera);
            ApplyClipMode(orthographicCamera);
        }

        ConfigureRotationBehavior();
        UpdateProjectionToggleButtonState();
        UpdateClippingToggleButtonState();
    }

    private void ClippingToggleButton_OnClick(object sender, RoutedEventArgs e)
    {
        _isNearClippingRelaxed = !_isNearClippingRelaxed;
        if (Viewport.Camera is HelixProjectionCamera camera)
        {
            ApplyClipMode(camera);
        }

        UpdateClippingToggleButtonState();
    }

    private void UpdateClippingToggleButtonState()
    {
        if (FindName("ClippingToggleButton") is not Button clippingToggleButton)
        {
            return;
        }

        clippingToggleButton.ToolTip = _isNearClippingRelaxed
            ? "Use normal near clipping"
            : "Relax near clipping";
        clippingToggleButton.Content = TryCreateToolbarIcon("/DcmViewer/Images/clipping.png");
        clippingToggleButton.Background = _isNearClippingRelaxed ? _activeToolBrush : null;
        clippingToggleButton.Foreground = _isNearClippingRelaxed ? _activeToolForegroundBrush : ColorSchemeResourceCatalog.GetBrush("BlackColor");
    }

    private void CaptureClipDefaults(HelixProjectionCamera camera)
    {
        _defaultNearPlaneDistance = Math.Clamp(camera.NearPlaneDistance, 0.0005, 0.02);
        _defaultFarPlaneDistance = Math.Max(camera.FarPlaneDistance, _defaultNearPlaneDistance + 1);
    }

    private void ApplyClipMode(HelixProjectionCamera camera)
    {
        var distance = Math.Max(camera.LookDirection.Length, 1e-3);
        var boundsDiagonal = GetVisibleBoundsDiagonal();
        var relaxedNear = Math.Max(distance * 1e-5, boundsDiagonal * 1e-4);
        relaxedNear = Math.Clamp(relaxedNear, 1e-6, 0.05);
        var relaxedFar = Math.Max(_defaultFarPlaneDistance, Math.Max(distance * 20000.0, boundsDiagonal * 200.0));

        if (_alwaysRelaxNearClipping || _isNearClippingRelaxed)
        {
            camera.NearPlaneDistance = relaxedNear;
            camera.FarPlaneDistance = relaxedFar;
            return;
        }

        camera.NearPlaneDistance = Math.Max(_defaultNearPlaneDistance, relaxedNear * 2.0);
        camera.FarPlaneDistance = relaxedFar;
    }

    private double GetVisibleBoundsDiagonal()
    {
        var bounds = GetVisibleBounds();
        if (bounds.IsEmpty)
        {
            return 80.0;
        }

        return Math.Max(bounds.SizeX, Math.Max(bounds.SizeY, bounds.SizeZ));
    }

    private void UpdateProjectionToggleButtonState()
    {
        if (FindName("ProjectionToggleButton") is not Button projectionToggleButton)
        {
            return;
        }

        projectionToggleButton.ToolTip = _isOrthographicView
            ? "Switch to perspective view"
            : "Switch to orthographic view";
        projectionToggleButton.Content = TryCreateToolbarIcon("/DcmViewer/Images/perspective.png");
    }

    private static Image? TryCreateToolbarIcon(string relativePath)
    {
        try
        {
            var componentPath = relativePath.TrimStart('/');
            var assemblyName = typeof(MainWindow).Assembly.GetName().Name;
            var uri = new Uri($"pack://application:,,,/{assemblyName};component/{componentPath}", UriKind.Absolute);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = uri;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            return new Image
            {
                Width = 16,
                Height = 16,
                Stretch = Stretch.Uniform,
                Source = bitmap
            };
        }
        catch
        {
            return null;
        }
    }

    private void BindCameraPose(HelixProjectionCamera camera)
    {
        var positionBinding = new Binding(nameof(MainViewModel.CameraPosition))
        {
            Source = _viewModel,
            Mode = BindingMode.OneWay
        };
        var lookBinding = new Binding(nameof(MainViewModel.CameraLookDirection))
        {
            Source = _viewModel,
            Mode = BindingMode.OneWay
        };
        var upBinding = new Binding(nameof(MainViewModel.CameraUpDirection))
        {
            Source = _viewModel,
            Mode = BindingMode.OneWay
        };

        BindingOperations.SetBinding(camera, HelixProjectionCamera.PositionProperty, positionBinding);
        BindingOperations.SetBinding(camera, HelixProjectionCamera.LookDirectionProperty, lookBinding);
        BindingOperations.SetBinding(camera, HelixProjectionCamera.UpDirectionProperty, upBinding);
    }

    private double CalculateOrthographicWidth(double cameraDistance, double fieldOfViewDegrees)
    {
        var distance = Math.Max(cameraDistance, 1e-3);
        var fovRadians = Math.Clamp(fieldOfViewDegrees, 1.0, 179.0) * (Math.PI / 180.0);
        var visibleHeight = 2.0 * distance * Math.Tan(fovRadians * 0.5);

        var viewportWidth = Viewport.ActualWidth;
        var viewportHeight = Viewport.ActualHeight;
        var aspect = viewportHeight > 1e-6 ? viewportWidth / viewportHeight : 1.0;

        return Math.Max(visibleHeight * Math.Max(aspect, 1e-3), 1e-3);
    }

    private void OnCompositionTargetRendering(object? sender, EventArgs e)
    {
        if (Viewport is null || !Viewport.IsLoaded || !Viewport.IsVisible)
        {
            return;
        }

        if (Viewport.Camera is not HelixProjectionCamera camera)
        {
            return;
        }

        var poseChanged = !_hasLastRenderedCameraPose ||
                          (camera.Position - _lastRenderedCameraPosition).LengthSquared > 1e-8 ||
                          (camera.LookDirection - _lastRenderedCameraLook).LengthSquared > 1e-8 ||
                          (camera.UpDirection - _lastRenderedCameraUp).LengthSquared > 1e-8;

        if (!poseChanged && _viewModel.IsBusy)
        {
            return;
        }

        if (poseChanged)
        {
            ApplyClipMode(camera);
            SyncLightingToCamera();
            _lastRenderedCameraPosition = camera.Position;
            _lastRenderedCameraLook = camera.LookDirection;
            _lastRenderedCameraUp = camera.UpDirection;
            _hasLastRenderedCameraPose = true;
        }

        _renderingFrameCounter++;
        if (poseChanged || _renderingFrameCounter % 8 == 0)
        {
            UpdateZoomPercentLabelIfChanged();
        }
    }

    private void SyncLightingToCamera()
    {
        if (Viewport.Camera is not HelixProjectionCamera camera)
        {
            return;
        }

        _viewModel.UpdateLightRigFromCamera(camera.LookDirection, camera.UpDirection);
    }

    private void UpdateZoomPercentLabel()
    {
        UpdateZoomPercentLabelIfChanged(force: true);
    }

    private string BuildZoomPercentLabel()
    {
        if (Viewport.Camera is not HelixProjectionCamera camera)
        {
            return "Zoom: 100%";
        }

        var bounds = GetVisibleBounds();
        if (bounds.IsEmpty)
        {
            return "Zoom: 100%";
        }

        var center = new Point3D(
            bounds.X + (bounds.SizeX / 2.0),
            bounds.Y + (bounds.SizeY / 2.0),
            bounds.Z + (bounds.SizeZ / 2.0));

        double zoomPercent;
        if (camera is HelixOrthographicCamera orthographicCamera)
        {
            var fitWidth = CalculateOrthographicFitWidth(bounds, camera.LookDirection, camera.UpDirection);
            var currentWidth = Math.Max(orthographicCamera.Width, 1e-9);
            zoomPercent = (fitWidth / currentWidth) * 100.0;
        }
        else
        {
            var fieldOfView = camera is HelixPerspectiveCamera perspective
                ? perspective.FieldOfView
                : DefaultPerspectiveFieldOfView;
            var fitDistance = CalculatePerspectiveFitDistance(
                bounds,
                camera.LookDirection,
                camera.UpDirection,
                fieldOfView,
                Viewport.ActualWidth,
                Viewport.ActualHeight);
            var currentDistance = GetDistance(camera.Position, center);
            zoomPercent = (fitDistance / Math.Max(currentDistance, 1e-9)) * 100.0;
        }

        var roundedPercent = Math.Clamp((int)Math.Round(zoomPercent), 1, 9999);
        return $"Zoom: {roundedPercent}%";
    }

    private void UpdateZoomPercentLabelIfChanged(bool force = false)
    {
        var label = BuildZoomPercentLabel();
        if (!force && string.Equals(label, _lastZoomPercentLabel, StringComparison.Ordinal))
        {
            return;
        }

        _lastZoomPercentLabel = label;
        ZoomPercentText.Text = label;
    }

    private static IEnumerable<Point3D> EnumerateBoundsCorners(Rect3D bounds)
    {
        var x0 = bounds.X;
        var y0 = bounds.Y;
        var z0 = bounds.Z;
        var x1 = x0 + bounds.SizeX;
        var y1 = y0 + bounds.SizeY;
        var z1 = z0 + bounds.SizeZ;

        for (var ix = 0; ix < 2; ix++)
        {
            for (var iy = 0; iy < 2; iy++)
            {
                for (var iz = 0; iz < 2; iz++)
                {
                    yield return new Point3D(
                        ix == 0 ? x0 : x1,
                        iy == 0 ? y0 : y1,
                        iz == 0 ? z0 : z1);
                }
            }
        }
    }

    private static void ProjectBoundsToViewPlane(
        Rect3D bounds,
        Vector3D look,
        Vector3D up,
        out double widthExtent,
        out double heightExtent)
    {
        look.Normalize();
        up.Normalize();

        var right = Vector3D.CrossProduct(look, up);
        if (right.LengthSquared < 1e-9)
        {
            right = Vector3D.CrossProduct(look, new Vector3D(1, 0, 0));
        }

        right.Normalize();
        var viewUp = Vector3D.CrossProduct(right, look);
        viewUp.Normalize();

        var center = new Point3D(
            bounds.X + (bounds.SizeX * 0.5),
            bounds.Y + (bounds.SizeY * 0.5),
            bounds.Z + (bounds.SizeZ * 0.5));

        var minRight = double.PositiveInfinity;
        var maxRight = double.NegativeInfinity;
        var minUp = double.PositiveInfinity;
        var maxUp = double.NegativeInfinity;

        foreach (var corner in EnumerateBoundsCorners(bounds))
        {
            var offset = corner - center;
            var r = Vector3D.DotProduct(offset, right);
            var u = Vector3D.DotProduct(offset, viewUp);
            minRight = Math.Min(minRight, r);
            maxRight = Math.Max(maxRight, r);
            minUp = Math.Min(minUp, u);
            maxUp = Math.Max(maxUp, u);
        }

        widthExtent = Math.Max(maxRight - minRight, 1e-6);
        heightExtent = Math.Max(maxUp - minUp, 1e-6);
    }

    private double CalculateOrthographicFitWidth(Rect3D bounds, Vector3D look, Vector3D up)
    {
        ProjectBoundsToViewPlane(bounds, look, up, out var widthExtent, out var heightExtent);

        var viewportWidth = Math.Max(Viewport.ActualWidth, 1.0);
        var viewportHeight = Math.Max(Viewport.ActualHeight, 1.0);
        var aspect = viewportWidth / viewportHeight;

        return Math.Max(widthExtent, heightExtent * aspect) * ZoomExtentsPadding;
    }

    private static double CalculatePerspectiveFitDistance(
        Rect3D bounds,
        Vector3D look,
        Vector3D up,
        double fieldOfViewDegrees,
        double viewportWidth,
        double viewportHeight)
    {
        ProjectBoundsToViewPlane(bounds, look, up, out var widthExtent, out var heightExtent);

        viewportWidth = Math.Max(viewportWidth, 1.0);
        viewportHeight = Math.Max(viewportHeight, 1.0);
        var aspect = viewportWidth / viewportHeight;

        var fovRadians = Math.Clamp(fieldOfViewDegrees, 1.0, 179.0) * (Math.PI / 180.0);
        var tanHalfFov = Math.Tan(fovRadians * 0.5);

        var distanceForHeight = (heightExtent * 0.5) / tanHalfFov;
        var distanceForWidth = (widthExtent * 0.5) / (tanHalfFov * aspect);

        return Math.Max(distanceForHeight, distanceForWidth) * ZoomExtentsPadding;
    }

    private static double GetDistance(Point3D a, Point3D b)
    {
        var delta = a - b;
        return delta.Length;
    }

    private Rect3D GetVisibleBounds()
    {
        Rect3D? bounds = null;

        foreach (var item in _viewModel.LoadedFiles)
        {
            if (!item.IsVisible || item.Bounds.IsEmpty)
            {
                continue;
            }

            bounds = bounds.HasValue ? Rect3D.Union(bounds.Value, item.Bounds) : item.Bounds;
        }

        return bounds ?? Rect3D.Empty;
    }

    private void Slider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Slider slider)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject source && FindVisualParent<Thumb>(source) is not null)
        {
            return;
        }

        var track = FindVisualChild<Track>(slider);
        if (track is null)
        {
            return;
        }

        var clickPosition = e.GetPosition(track);
        var span = slider.Orientation == Orientation.Horizontal
            ? Math.Max(track.ActualWidth, 1)
            : Math.Max(track.ActualHeight, 1);

        var ratio = slider.Orientation == Orientation.Horizontal
            ? clickPosition.X / span
            : 1.0 - (clickPosition.Y / span);

        ratio = Math.Clamp(ratio, 0.0, 1.0);
        var value = slider.Minimum + (ratio * (slider.Maximum - slider.Minimum));
        slider.Value = Math.Clamp(value, slider.Minimum, slider.Maximum);
        e.Handled = true;
    }

    private void ApplySectionModeFromViewModel()
    {
        SectionOffsetSlider.Visibility = _viewModel.IsSectionMode ? Visibility.Visible : Visibility.Collapsed;
        UpdateToolButtonStates();
        if (IsEmbeddedHost)
        {
            EnsureEmbeddedHostAppearance();
        }
        else
        {
            ApplyViewportClearTransparency();
        }

        if (!_viewModel.IsSectionMode)
        {
            _viewModel.ApplySectionPlane(_activeSectionPlanePoint, _activeSectionPlaneNormal, false);
            ClearSectionPlaneVisuals();
            SectionProfilePanel.Visibility = Visibility.Collapsed;
            SectionProfileCanvas.Children.Clear();
            _hasSectionProjectionMap = false;
            UpdateMeasurementVisual();
            return;
        }

        SectionProfilePanel.Visibility = Visibility.Visible;
        SectionProfileHintText.Text = "Click on mesh to place section plane";
        SectionProfileHintText.Visibility = Visibility.Visible;

        if (IsEmbeddedHost)
        {
            PositionEmbeddedSectionProfilePanel();
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                if (_viewModel.IsSectionMode)
                {
                    PositionEmbeddedSectionProfilePanel();
                }
            });
        }

        var bounds = GetVisibleBounds();
        if (!bounds.IsEmpty)
        {
            RefreshSectionReferenceFromBounds(bounds);
        }

        UpdateSectionPlane();
    }

    private void WatermarkCanvas_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsEmbeddedHost && _viewModel.IsSectionMode)
        {
            PositionEmbeddedSectionProfilePanel();
        }
    }

    private void PositionEmbeddedSectionProfilePanel()
    {
        var host = WatermarkCanvas;
        var panelWidth = SectionProfilePanel.ActualWidth > 1 ? SectionProfilePanel.ActualWidth : SectionProfilePanel.Width;
        var panelHeight = SectionProfilePanel.ActualHeight > 1 ? SectionProfilePanel.ActualHeight : SectionProfilePanel.Height;
        var hostWidth = host.ActualWidth > 1 ? host.ActualWidth : ActualWidth;
        var hostHeight = host.ActualHeight > 1 ? host.ActualHeight : ActualHeight;

        SectionProfilePanel.HorizontalAlignment = HorizontalAlignment.Left;
        SectionProfilePanel.VerticalAlignment = VerticalAlignment.Top;
        var left = Math.Clamp(EmbeddedSectionPanelLeftInset, 0, Math.Max(0, hostWidth - panelWidth));
        var top = Math.Max(0, hostHeight - panelHeight - EmbeddedSectionPanelBottomInset);
        SectionProfilePanel.Margin = new Thickness(left, top, 0, 0);
    }

    private void EnsureSectionPlaneFillMaterial()
    {
        var ring = ColorSchemeResourceCatalog.GetColor("ViewerSculptRingColor");
        const float alpha = 0.32f;
        var fill = new HelixToolkit.Maths.Color4(ring.R / 255f, ring.G / 255f, ring.B / 255f, alpha);
        SectionPlaneVisual.Material = new PhongMaterial
        {
            AmbientColor = fill * 0.75f,
            DiffuseColor = fill,
            EmissiveColor = new HelixToolkit.Maths.Color4(0, 0, 0, 0),
            SpecularColor = new HelixToolkit.Maths.Color4(0.55f, 0.65f, 0.82f, alpha * 0.4f),
            SpecularShininess = 14f
        };
        SectionPlaneVisual.IsTransparent = true;
        SectionPlaneVisual.CullMode = SharpDX.Direct3D11.CullMode.None;
        SectionPlaneVisual.RenderOrder = 2500;
    }

    private void ClearSectionPlaneVisuals()
    {
        SectionPlaneVisual.Geometry = null;
        SectionPlaneVisual.Visibility = Visibility.Collapsed;
        SectionPlaneOutlineVisual.Geometry = null;
        SectionPlaneOutlineVisual.Visibility = Visibility.Collapsed;
        MeasurementLine.Visibility = Visibility.Collapsed;
        MeasurementText.Visibility = Visibility.Collapsed;
    }

    private void Viewport_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (TryHandleEncodeIdentifyPick(e))
        {
            return;
        }

        if (TryHandleFuseInnerSidePick(e))
        {
            return;
        }

        if (_viewModel.IsSculptMode)
        {
            if (TryBeginSculptStroke(e))
            {
                e.Handled = true;
            }

            return;
        }

        if (!_viewModel.IsSectionMode)
        {
            return;
        }

        var sectionHits = Viewport.FindHits(e.GetPosition(Viewport));
        var sectionHit = sectionHits.FirstOrDefault(h => h.ModelHit is MeshGeometryModel3D);
        if (sectionHit is null)
        {
            return;
        }

        _sectionCenter = new Point3D(sectionHit.PointHit.X, sectionHit.PointHit.Y, sectionHit.PointHit.Z);
        if (Viewport.Camera is HelixProjectionCamera sectionCamera)
        {
            var look = sectionCamera.LookDirection;
            var up = sectionCamera.UpDirection;
            var perpendicularNormal = Vector3D.CrossProduct(look, up);
            if (perpendicularNormal.LengthSquared < 1e-9)
            {
                perpendicularNormal = Vector3D.CrossProduct(look, new Vector3D(0, 1, 0));
            }

            if (perpendicularNormal.LengthSquared < 1e-9)
            {
                perpendicularNormal = new Vector3D(1, 0, 0);
            }

            _sectionNormal = perpendicularNormal;
        }

        _viewModel.SectionOffset = 0;
        UpdateSectionPlane();
        e.Handled = true;
    }

    private void UpdateMeasurementVisual()
    {
        MeasurementLine.Visibility = Visibility.Visible;
        MeasurementText.Visibility = Visibility.Visible;

        if (_viewModel.IsSectionMode)
        {
            UpdateSectionProfileView(_activeSectionPlanePoint, _activeSectionPlaneNormal, resetCanvasTransform: false);
        }
    }

    private void RefreshSectionReferenceFromBounds(Rect3D bounds)
    {
        if (bounds.IsEmpty)
        {
            return;
        }

        _sectionCenter = new Point3D(
            bounds.X + (bounds.SizeX / 2.0),
            bounds.Y + (bounds.SizeY / 2.0),
            bounds.Z + (bounds.SizeZ / 2.0));

        var maxSpan = Math.Max(bounds.SizeX, Math.Max(bounds.SizeY, bounds.SizeZ));
        var diagonal = new Vector3D(bounds.SizeX, bounds.SizeY, bounds.SizeZ).Length;
        _sectionVisualRadius = Math.Max((diagonal * 0.5) * 1.2, Math.Max(maxSpan * 0.6, 8.0));

        _sectionTravelRange = Math.Max(diagonal * 0.5, 1.0);
        UpdateSectionPlane();
    }

    private void UpdateSectionPlane()
    {
        if (!_viewModel.IsSectionMode)
        {
            _viewModel.ApplySectionPlane(_activeSectionPlanePoint, _activeSectionPlaneNormal, false);
            ClearSectionPlaneVisuals();
            SectionProfileHintText.Text = "Enable section and click on mesh to position plane";
            SectionProfileHintText.Visibility = Visibility.Visible;
            _hasSectionProjectionMap = false;
            return;
        }

        var normal = _sectionNormal;
        if (normal.LengthSquared < 1e-9)
        {
            normal = new Vector3D(0, 0, 1);
        }

        normal.Normalize();

        var offset = _viewModel.SectionOffset * _sectionTravelRange;
        var planePosition = _sectionCenter + (normal * offset);
        _activeSectionPlanePoint = planePosition;
        _activeSectionPlaneNormal = normal;

        _viewModel.ApplySectionPlane(planePosition, normal, true);
        UpdateSectionPlaneVisual(planePosition, normal);
        UpdateSectionProfileView(planePosition, normal, resetCanvasTransform: false);
    }

    private void UpdateSectionPlaneVisual(Point3D center, Vector3D normal)
    {
        var up = new Vector3D(0, 1, 0);
        if (Viewport.Camera is HelixProjectionCamera camera && camera.UpDirection.LengthSquared > 1e-9)
        {
            up = camera.UpDirection;
        }

        up.Normalize();

        var axisX = Vector3D.CrossProduct(normal, up);
        if (axisX.LengthSquared < 1e-9)
        {
            axisX = Vector3D.CrossProduct(normal, new Vector3D(1, 0, 0));
        }

        if (axisX.LengthSquared < 1e-9)
        {
            axisX = Vector3D.CrossProduct(normal, new Vector3D(0, 0, 1));
        }

        axisX.Normalize();
        var axisY = Vector3D.CrossProduct(axisX, normal);
        axisY.Normalize();

        var radius = Math.Max(_sectionVisualRadius, 4.0);
        const int segments = 64;

        var normalOffset = normal * 0.05;
        var diskCenter = center + normalOffset;
        var ringPts = new Point3D[segments];
        for (var i = 0; i < segments; i++)
        {
            var angle = (Math.PI * 2.0 * i) / segments;
            ringPts[i] = diskCenter + (axisX * (Math.Cos(angle) * radius)) + (axisY * (Math.Sin(angle) * radius));
        }

        SectionPlaneVisual.Geometry = SharpDxMeshFactory.CreateDiskGeometry(diskCenter, axisX, axisY, radius, segments);
        SectionPlaneVisual.Visibility = Visibility.Visible;

        var outlinePoints = new List<Point3D>(segments * 2);
        for (var i = 0; i < segments; i++)
        {
            outlinePoints.Add(ringPts[i]);
            outlinePoints.Add(ringPts[(i + 1) % segments]);
        }

        SectionPlaneOutlineVisual.Geometry = SharpDxMeshFactory.CreateSegmentLineGeometry(outlinePoints);
        SectionPlaneOutlineVisual.Visibility = Visibility.Visible;
    }

    private void UpdateToolButtonStates()
    {
        SectionToggleButton.Background = _viewModel.IsSectionMode ? _activeToolBrush : _toolbarButtonBackground;
        SectionToggleButton.Foreground = _viewModel.IsSectionMode ? _activeToolForegroundBrush : ColorSchemeResourceCatalog.GetBrush("BlackColor");
        SectionToggleButton.Content = TryCreateToolbarIcon(_viewModel.IsSectionMode ? "/DcmViewer/Images/turnoffsection.png" : "/DcmViewer/Images/sectionmode.png");

        MeasureToggleButton.Background = _viewModel.IsMeasureMode ? _activeToolBrush : _toolbarButtonBackground;
        MeasureToggleButton.Foreground = _viewModel.IsMeasureMode ? _activeToolForegroundBrush : ColorSchemeResourceCatalog.GetBrush("BlackColor");

        if (FindName("SculptToggleButton") is Button sculptToggleButton)
        {
            ApplyActiveToolbarButtonState(sculptToggleButton, _viewModel.IsSculptMode);
        }

        UpdateHideLayerLabelsButtonState();
    }

    private void ApplyActiveToolbarButtonState(Button button, bool isActive)
    {
        button.Background = isActive ? _activeToolBrush : _toolbarButtonBackground;
        button.Foreground = isActive ? _activeToolForegroundBrush : ColorSchemeResourceCatalog.GetBrush("BlackColor");
        button.BorderBrush = isActive
            ? new SolidColorBrush(Color.FromRgb(42, 106, 163))
            : new SolidColorBrush(Color.FromRgb(189, 199, 210));
        button.BorderThickness = isActive ? new Thickness(1.5) : new Thickness(1);
    }

    private void ApplySculptModeFromViewModel()
    {
        if (FindName("SculptToolPanel") is FrameworkElement sculptToolPanel)
        {
            sculptToolPanel.Visibility = _viewModel.IsSculptMode ? Visibility.Visible : Visibility.Collapsed;
        }

        Viewport.IsRotationEnabled = true;
        Viewport.IsPanEnabled = true;
        Viewport.IsZoomEnabled = true;
        UpdateToolButtonStates();

        if (!_viewModel.IsSculptMode)
        {
            EndSculptStroke();
            ClearSculptBrushPreview();
        }
    }

    private void HideSculptBrushVisual()
    {
        SculptBrushVisual.Geometry = null;
        SculptBrushVisual.Visibility = Visibility.Collapsed;
    }

    private void ClearSculptBrushPreview()
    {
        _sculptBrushPreviewScreen = null;
        HideSculptBrushVisual();
    }

    private void UpdateSculptBrushPreview(Point screenPoint, bool force = false)
    {
        if (!_viewModel.IsSculptMode || _isSculptDragging)
        {
            return;
        }

        _sculptBrushPreviewScreen = screenPoint;

        if (!force)
        {
            var now = DateTime.UtcNow.Ticks;
            var dx = screenPoint.X - _sculptBrushPreviewLastRenderedScreen.X;
            var dy = screenPoint.Y - _sculptBrushPreviewLastRenderedScreen.Y;
            if ((dx * dx) + (dy * dy) < SculptBrushPreviewMinMovePx * SculptBrushPreviewMinMovePx &&
                now - _sculptBrushPreviewLastUpdateTicks < SculptBrushPreviewMinIntervalTicks)
            {
                return;
            }
        }

        var hit = FindSculptHit(screenPoint);
        if (hit is null)
        {
            HideSculptBrushVisual();
            return;
        }

        SculptBrushVisual.Geometry = SharpDxMeshFactory.CreateClosedLineGeometry(
            hit.Value.Target.BuildSculptBrushRingPoints(
                hit.Value.Point,
                hit.Value.Normal,
                _viewModel.SculptBrushRadiusMm));
        SculptBrushVisual.Visibility = Visibility.Visible;
        _sculptBrushPreviewLastRenderedScreen = screenPoint;
        _sculptBrushPreviewLastUpdateTicks = DateTime.UtcNow.Ticks;
    }

    private void Viewport_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (_viewModel.IsSculptMode && !_isSculptDragging)
        {
            ClearSculptBrushPreview();
        }
    }

    private void Viewport_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_viewModel.IsSculptMode)
        {
            if (_isSculptDragging && _sculptTarget is not null)
            {
                if (e.LeftButton != MouseButtonState.Pressed)
                {
                    EndSculptStroke();
                    UpdateSculptBrushPreview(e.GetPosition(Viewport));
                    return;
                }

                ApplySculptStrokeAt(e.GetPosition(Viewport), isDrag: true);
                e.Handled = true;
                return;
            }

            UpdateSculptBrushPreview(e.GetPosition(Viewport));
            return;
        }

        if (!_isSculptDragging || _sculptTarget is null)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndSculptStroke();
            return;
        }

        ApplySculptStrokeAt(e.GetPosition(Viewport), isDrag: true);
        e.Handled = true;
    }

    private void Viewport_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSculptDragging)
        {
            return;
        }

        EndSculptStroke();
        e.Handled = true;
    }

    private void Viewport_OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        StopViewportCameraSpin();
        EndSculptStroke();
    }

    private bool TryBeginSculptStroke(MouseButtonEventArgs e)
    {
        var hit = FindSculptHit(e.GetPosition(Viewport));
        if (hit is null)
        {
            return false;
        }

        _isSculptDragging = true;
        _sculptTarget = hit.Value.Target;
        _sculptLastScreen = e.GetPosition(Viewport);
        _sculptLastNormal = hit.Value.Normal;
        StopViewportCameraSpin();
        HideSculptBrushVisual();
        _viewModel.BeginSculptStroke(_sculptTarget);
        Viewport.CaptureMouse();
        ApplySculptStrokeAt(_sculptLastScreen, isDrag: false);
        return true;
    }

    private void ApplySculptStrokeAt(Point screenPoint, bool isDrag)
    {
        if (_sculptTarget is null)
        {
            return;
        }

        var hit = FindSculptHit(screenPoint, _sculptTarget.Model);
        if (hit is null)
        {
            return;
        }

        Vector3D? grabDelta = null;
        if (_viewModel.CurrentSculptTool == SculptBrushTool.Grab && isDrag)
        {
            grabDelta = ComputeScreenDragDelta(screenPoint, _sculptLastScreen);
        }

        _viewModel.TryApplySculptStroke(
            hit.Value.Target,
            hit.Value.Point,
            hit.Value.Normal,
            grabDelta);

        _sculptLastScreen = screenPoint;
        _sculptLastNormal = hit.Value.Normal;
    }

    private void EndSculptStroke()
    {
        if (_isSculptDragging)
        {
            _viewModel.CommitSculptStroke();
        }

        _isSculptDragging = false;
        _sculptTarget = null;
        StopViewportCameraSpin();
        if (Viewport.IsMouseCaptured)
        {
            Viewport.ReleaseMouseCapture();
        }

        if (_viewModel.IsSculptMode && Viewport.IsMouseOver)
        {
            UpdateSculptBrushPreview(Mouse.GetPosition(Viewport));
        }
    }

    private void MainWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && TryCancelFuseInnerSidePickingFromKeyboard())
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (!_viewModel.IsSculptMode)
            {
                return;
            }

            if (TryUndoSculptFromKeyboard())
            {
                e.Handled = true;
            }

            return;
        }

        if ((e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control) ||
            (e.Key == Key.Z && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)))
        {
            if (!_viewModel.IsSculptMode)
            {
                return;
            }

            if (TryRedoSculptFromKeyboard())
            {
                e.Handled = true;
            }
        }
    }

    public bool TryRedoSculptFromKeyboard()
    {
        if (!_viewModel.IsSculptMode)
        {
            return false;
        }

        if (_isSculptDragging)
        {
            EndSculptStroke();
        }

        return _viewModel.TryRedoSculpt();
    }

    public bool TryUndoSculptFromKeyboard()
    {
        if (!_viewModel.IsSculptMode)
        {
            return false;
        }

        if (_isSculptDragging)
        {
            EndSculptStroke();
        }

        return _viewModel.TryUndoSculpt();
    }

    private (LoadedMeshItemViewModel Target, Point3D Point, Vector3D Normal)? FindSculptHit(
        Point screenPoint,
        MeshGeometryModel3D? requiredModel = null)
    {
        var hits = Viewport.FindHits(screenPoint);
        foreach (var hit in hits)
        {
            if (hit.ModelHit is not MeshGeometryModel3D model)
            {
                continue;
            }

            if (requiredModel is not null && !ReferenceEquals(model, requiredModel))
            {
                continue;
            }

            var target = _viewModel.FindLoadedFileByModel(model);
            if (target is null || target.IsLoadFailed || !target.IsVisible)
            {
                continue;
            }

            var point = new Point3D(hit.PointHit.X, hit.PointHit.Y, hit.PointHit.Z);
            return (target, point, target.GetSurfaceNormalNear(point));
        }

        return null;
    }

    private Vector3D ComputeScreenDragDelta(Point currentScreen, Point lastScreen)
    {
        if (Viewport.Camera is not HelixProjectionCamera camera)
        {
            return new Vector3D();
        }

        var look = camera.LookDirection;
        if (look.LengthSquared < 1e-12)
        {
            return new Vector3D();
        }

        look.Normalize();
        var up = camera.UpDirection;
        if (up.LengthSquared < 1e-12)
        {
            up = new Vector3D(0, 1, 0);
        }

        up.Normalize();
        var right = Vector3D.CrossProduct(look, up);
        if (right.LengthSquared < 1e-12)
        {
            right = new Vector3D(1, 0, 0);
        }

        right.Normalize();
        var cameraUp = Vector3D.CrossProduct(right, look);
        cameraUp.Normalize();

        var distance = Math.Max(camera.LookDirection.Length, 1.0);
        var scale = distance / Math.Max(Viewport.ActualHeight, 1.0);
        var dx = (currentScreen.X - lastScreen.X) * scale;
        var dy = (currentScreen.Y - lastScreen.Y) * scale;
        return (right * dx) + (cameraUp * -dy);
    }

    private void UpdateHideLayerLabelsButtonState()
    {
        if (FindName("HideLayerLabelsButton") is not Button hideLayerLabelsButton)
        {
            return;
        }

        var labelsVisible = !_viewModel.HideLayerLabels;
        ApplyActiveToolbarButtonState(hideLayerLabelsButton, labelsVisible);
        hideLayerLabelsButton.ToolTip = _viewModel.HideLayerLabels
            ? "Show layer file labels"
            : "Hide layer file labels";
    }

    private void ResetCanvasTransform()
    {
        _canvasMatrix = Matrix.Identity;
        SectionProfileCanvas.RenderTransform = new MatrixTransform(_canvasMatrix);
        RefreshProfileStrokeThickness();
        ResetZoomButton.Visibility = Visibility.Collapsed;
    }

    private void ResetZoomButton_OnClick(object sender, RoutedEventArgs e)
    {
        ResetCanvasTransform();
    }

    private void SectionProfileCanvas_OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!_hasSectionProjectionMap) return;
        var factor = e.Delta > 0 ? 1.15 : 1.0 / 1.15;
        var localPos = e.GetPosition(SectionProfileCanvas);
        var m = _canvasMatrix;
        var currentScale = Math.Max(m.M11, 1e-9);
        var targetScale = currentScale * factor;
        if (targetScale < 1.0)
        {
            factor = 1.0 / currentScale;
        }

        m.ScaleAt(factor, factor, localPos.X, localPos.Y);
        _canvasMatrix = m;
        SectionProfileCanvas.RenderTransform = new MatrixTransform(_canvasMatrix);
        RefreshProfileStrokeThickness();
        ResetZoomButton.Visibility = _canvasMatrix.M11 > 1.0001 ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    private void SectionProfileHeader_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_viewModel.IsSectionMode)
        {
            return;
        }

        EnsureSectionProfilePanelAbsoluteLayout();
        _isSectionPanelDragging = true;
        _sectionPanelDragAnchor = e.GetPosition(WatermarkCanvas);

        if (sender is UIElement element)
        {
            element.CaptureMouse();
        }

        e.Handled = true;
    }

    private void SectionProfileHeader_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isSectionPanelDragging)
        {
            return;
        }

        var host = WatermarkCanvas;
        var next = e.GetPosition(host);
        var delta = next - _sectionPanelDragAnchor;
        _sectionPanelDragAnchor = next;

        var panelWidth = SectionProfilePanel.ActualWidth > 1 ? SectionProfilePanel.ActualWidth : SectionProfilePanel.Width;
        var panelHeight = SectionProfilePanel.ActualHeight > 1 ? SectionProfilePanel.ActualHeight : SectionProfilePanel.Height;

        var hostWidth = host.ActualWidth > 1 ? host.ActualWidth : ActualWidth;
        var hostHeight = host.ActualHeight > 1 ? host.ActualHeight : ActualHeight;

        var margin = SectionProfilePanel.Margin;
        var left = Math.Clamp(margin.Left + delta.X, 0, Math.Max(0, hostWidth - panelWidth));
        var top = Math.Clamp(margin.Top + delta.Y, 0, Math.Max(0, hostHeight - panelHeight));

        SectionProfilePanel.Margin = new Thickness(left, top, 0, 0);

        e.Handled = true;
    }

    private void SectionProfileHeader_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSectionPanelDragging)
        {
            return;
        }

        _isSectionPanelDragging = false;
        if (sender is UIElement element)
        {
            element.ReleaseMouseCapture();
        }

        e.Handled = true;
    }

    private void EnsureSectionProfilePanelAbsoluteLayout()
    {
        if (SectionProfilePanel.HorizontalAlignment == HorizontalAlignment.Left &&
            SectionProfilePanel.VerticalAlignment == VerticalAlignment.Top)
        {
            return;
        }

        var host = WatermarkCanvas;
        var topLeft = SectionProfilePanel.TranslatePoint(new Point(0, 0), host);

        SectionProfilePanel.HorizontalAlignment = HorizontalAlignment.Left;
        SectionProfilePanel.VerticalAlignment = VerticalAlignment.Top;
        SectionProfilePanel.Margin = new Thickness(Math.Max(0, topLeft.X), Math.Max(0, topLeft.Y), 0, 0);
    }

    private void SectionProfileCanvas_OnRightMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isCanvasPanning = true;
        _canvasPanAnchor = e.GetPosition((IInputElement)((FrameworkElement)SectionProfileCanvas).Parent);
        SectionProfileCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void SectionProfileCanvas_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isCanvasPanning) return;
        var parent = (IInputElement)((FrameworkElement)SectionProfileCanvas).Parent;
        var pos = e.GetPosition(parent);
        var delta = pos - _canvasPanAnchor;
        _canvasPanAnchor = pos;
        var m = _canvasMatrix;
        m.Translate(delta.X, delta.Y);
        _canvasMatrix = m;
        SectionProfileCanvas.RenderTransform = new MatrixTransform(_canvasMatrix);
        RefreshProfileStrokeThickness();
    }

    private void SectionProfileCanvas_OnRightMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isCanvasPanning) return;
        _isCanvasPanning = false;
        SectionProfileCanvas.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void UpdateSectionProfileView(Point3D planePoint, Vector3D planeNormal, bool resetCanvasTransform = true)
    {
        SectionProfileGridCanvas.Children.Clear();
        SectionProfileCanvas.Children.Clear();
        if (resetCanvasTransform)
        {
            ResetCanvasTransform();
        }

        var width = SectionProfileCanvas.ActualWidth;
        var height = SectionProfileCanvas.ActualHeight;
        if (width < 40 || height < 40)
        {
            _hasSectionProjectionMap = false;
            return;
        }

        DrawSectionGrid(width, height);

        var profileUp = new Vector3D(0, 1, 0);
        if (Viewport.Camera is HelixProjectionCamera camera && camera.UpDirection.LengthSquared > 1e-9)
        {
            profileUp = camera.UpDirection;
        }

        SectionGeometryService.GetSectionProfileAxes(planeNormal, profileUp, out var axisX, out var axisY);

        var segments2D = SectionGeometryService.BuildSectionSegments2D(_viewModel.LoadedFiles, planePoint, planeNormal, axisX, axisY);
        _currentSectionSegments = segments2D;
        if (segments2D.Count == 0)
        {
            _hasSectionProjectionMap = false;
            SectionProfileHintText.Text = "No profile at current plane position";
            SectionProfileHintText.Visibility = Visibility.Visible;
            return;
        }

        if (resetCanvasTransform || !_hasSectionProjectionMap)
        {
            var minX = segments2D.Min(s => Math.Min(s.A.X, s.B.X));
            var maxX = segments2D.Max(s => Math.Max(s.A.X, s.B.X));
            var minY = segments2D.Min(s => Math.Min(s.A.Y, s.B.Y));
            var maxY = segments2D.Max(s => Math.Max(s.A.Y, s.B.Y));

            var extentX = Math.Max(maxX - minX, 1e-6);
            var extentY = Math.Max(maxY - minY, 1e-6);
            var padding = 18.0;
            var drawableW = Math.Max(width - (padding * 2.0), 1.0);
            var drawableH = Math.Max(height - (padding * 2.0), 1.0);
            var scale = Math.Min(drawableW / extentX, drawableH / extentY);

            _sectionProjectionScale = scale;
            _sectionProjectionCenterX = (minX + maxX) * 0.5;
            _sectionProjectionCenterY = (minY + maxY) * 0.5;
            _sectionProjectionCanvasCenterX = width * 0.5;
            _sectionProjectionCanvasCenterY = height * 0.5;
        }

        _hasSectionProjectionMap = true;

        foreach (var segment in segments2D)
        {
            var color = segment.Category == MeshCategory.Restoration
                ? DarkenColor(MaterialLibrary.Get("Zirconia").FrontDiffuse, 0.62)
                : Color.FromRgb(30, 58, 96);

            var line = new Line
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = GetTransformedLineStrokeThickness(),
                SnapsToDevicePixels = true
            };

            line.X1 = _sectionProjectionCanvasCenterX + ((segment.A.X - _sectionProjectionCenterX) * _sectionProjectionScale);
            line.Y1 = _sectionProjectionCanvasCenterY - ((segment.A.Y - _sectionProjectionCenterY) * _sectionProjectionScale);
            line.X2 = _sectionProjectionCanvasCenterX + ((segment.B.X - _sectionProjectionCenterX) * _sectionProjectionScale);
            line.Y2 = _sectionProjectionCanvasCenterY - ((segment.B.Y - _sectionProjectionCenterY) * _sectionProjectionScale);

            SectionProfileCanvas.Children.Add(line);
        }

        DrawSectionMeasurementOverlay();
        RefreshProfileStrokeThickness();
        SectionProfileHintText.Visibility = Visibility.Collapsed;
    }

    private void SectionProfileCanvas_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_viewModel.IsMeasureMode || !_viewModel.IsSectionMode || !_hasSectionProjectionMap)
        {
            return;
        }

        var click = e.GetPosition(SectionProfileCanvas);
        if (click.X < 0 || click.Y < 0 || click.X > SectionProfileCanvas.ActualWidth || click.Y > SectionProfileCanvas.ActualHeight)
        {
            return;
        }

        var sectionPoint = CanvasToSectionPoint(click);
        if (!TrySnapToSectionCurve(sectionPoint, out var snapped))
        {
            return;
        }

        _viewModel.RegisterSectionMeasurementPoint(snapped);
        e.Handled = true;
    }

    private bool TrySnapToSectionCurve(Point point, out Point snapped)
    {
        snapped = point;
        if (_currentSectionSegments.Count == 0)
        {
            return false;
        }

        var bestDistanceSquared = double.MaxValue;
        foreach (var segment in _currentSectionSegments)
        {
            var candidate = ClosestPointOnSegment(point, segment.A, segment.B);
            var delta = candidate - point;
            var distanceSquared = (delta.X * delta.X) + (delta.Y * delta.Y);
            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                snapped = candidate;
            }
        }

        var toleranceInSectionUnits = 10.0 / Math.Max(_sectionProjectionScale, 1e-9);
        return bestDistanceSquared <= (toleranceInSectionUnits * toleranceInSectionUnits);
    }

    private static Point ClosestPointOnSegment(Point p, Point a, Point b)
    {
        var ab = b - a;
        var abLengthSquared = (ab.X * ab.X) + (ab.Y * ab.Y);
        if (abLengthSquared <= 1e-12)
        {
            return a;
        }

        var ap = p - a;
        var t = ((ap.X * ab.X) + (ap.Y * ab.Y)) / abLengthSquared;
        t = Math.Clamp(t, 0.0, 1.0);
        return new Point(a.X + (ab.X * t), a.Y + (ab.Y * t));
    }

    private Point CanvasToSectionPoint(Point canvasPoint)
    {
        var x = ((canvasPoint.X - _sectionProjectionCanvasCenterX) / _sectionProjectionScale) + _sectionProjectionCenterX;
        var y = ((-_sectionProjectionCanvasCenterY + canvasPoint.Y) / _sectionProjectionScale * -1.0) + _sectionProjectionCenterY;
        return new Point(x, y);
    }

    private Point SectionToCanvasPoint(Point sectionPoint)
    {
        var x = _sectionProjectionCanvasCenterX + ((sectionPoint.X - _sectionProjectionCenterX) * _sectionProjectionScale);
        var y = _sectionProjectionCanvasCenterY - ((sectionPoint.Y - _sectionProjectionCenterY) * _sectionProjectionScale);
        return new Point(x, y);
    }

    private void DrawSectionMeasurementOverlay()
    {
        if (_viewModel.MeasureStartSection is null)
        {
            return;
        }

        var startCanvas = SectionToCanvasPoint(_viewModel.MeasureStartSection.Value);
        DrawMeasureMarker(startCanvas, isStart: true);

        if (_viewModel.MeasureEndSection is null)
        {
            return;
        }

        var endCanvas = SectionToCanvasPoint(_viewModel.MeasureEndSection.Value);
        DrawMeasureMarker(endCanvas, isStart: false);

        var line = new Line
        {
            X1 = startCanvas.X,
            Y1 = startCanvas.Y,
            X2 = endCanvas.X,
            Y2 = endCanvas.Y,
            Stroke = new SolidColorBrush(Color.FromRgb(20, 38, 70)),
            StrokeThickness = GetTransformedLineStrokeThickness()
        };
        SectionProfileCanvas.Children.Add(line);

        var delta = _viewModel.MeasureEndSection.Value - _viewModel.MeasureStartSection.Value;
        var distance = Math.Sqrt((delta.X * delta.X) + (delta.Y * delta.Y));
        var label = new TextBlock
        {
            Text = $"{distance:F3} mm",
            Foreground = new SolidColorBrush(Color.FromRgb(20, 38, 70)),
            Background = new SolidColorBrush(Color.FromArgb(205, 247, 250, 255)),
            FontSize = 20,
            Padding = new Thickness(3, 1, 3, 1)
        };

        var textScale = GetTransformedTextScale();
        label.RenderTransformOrigin = new Point(0, 0);
        label.RenderTransform = new ScaleTransform(textScale, textScale);

        var midX = (startCanvas.X + endCanvas.X) * 0.5;
        var midY = (startCanvas.Y + endCanvas.Y) * 0.5;
        Canvas.SetLeft(label, midX + (4.0 * textScale));
        Canvas.SetTop(label, midY - (16.0 * textScale));
        SectionProfileCanvas.Children.Add(label);
    }

    private void DrawMeasureMarker(Point point, bool isStart)
    {
        var markerSize = _encodeSectionMeasureStyle ? 6.0 : GetTransformedMarkerSize();
        var markerRadius = markerSize * 0.5;
        Brush fill;
        if (_encodeSectionMeasureStyle)
        {
            var core = isStart ? Color.FromRgb(235, 70, 70) : Color.FromRgb(55, 130, 235);
            fill = new RadialGradientBrush(
                Color.FromRgb(255, 255, 255),
                core)
            {
                GradientOrigin = new Point(0.32, 0.28),
                Center = new Point(0.32, 0.28),
                RadiusX = 0.85,
                RadiusY = 0.85
            };
            fill.Freeze();
        }
        else
        {
            fill = ColorSchemeResourceCatalog.GetBrush("BlackColor");
        }

        var marker = new Ellipse
        {
            Width = markerSize,
            Height = markerSize,
            Fill = fill,
            Stroke = _encodeSectionMeasureStyle
                ? new SolidColorBrush(Color.FromArgb(180, 30, 45, 70))
                : null,
            StrokeThickness = _encodeSectionMeasureStyle ? 0.8 : 0
        };

        Canvas.SetLeft(marker, point.X - markerRadius);
        Canvas.SetTop(marker, point.Y - markerRadius);
        SectionProfileCanvas.Children.Add(marker);
    }

    private void DrawSectionGrid(double width, double height)
    {
        const int divisions = 6;
        var gridBrush = new SolidColorBrush(Color.FromRgb(211, 220, 230));

        for (var i = 0; i <= divisions; i++)
        {
            var x = i * (width / divisions);
            var vertical = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = gridBrush,
                StrokeThickness = 2.0
            };
            SectionProfileGridCanvas.Children.Add(vertical);

            var y = i * (height / divisions);
            var horizontal = new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = width,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 2.0
            };
            SectionProfileGridCanvas.Children.Add(horizontal);
        }
    }

    private void RefreshProfileStrokeThickness()
    {
        var thickness = GetTransformedLineStrokeThickness();
        foreach (var line in SectionProfileCanvas.Children.OfType<Line>())
        {
            line.StrokeThickness = thickness;
        }

        var markerSize = GetTransformedMarkerSize();
        var markerRadius = markerSize * 0.5;
        foreach (var marker in SectionProfileCanvas.Children.OfType<Ellipse>())
        {
            var centerX = Canvas.GetLeft(marker) + (marker.Width * 0.5);
            var centerY = Canvas.GetTop(marker) + (marker.Height * 0.5);
            marker.Width = markerSize;
            marker.Height = markerSize;
            Canvas.SetLeft(marker, centerX - markerRadius);
            Canvas.SetTop(marker, centerY - markerRadius);
        }

        var textScale = GetTransformedTextScale();
        foreach (var label in SectionProfileCanvas.Children.OfType<TextBlock>())
        {
            label.RenderTransformOrigin = new Point(0, 0);
            label.RenderTransform = new ScaleTransform(textScale, textScale);
        }
    }

    private double GetTransformedLineStrokeThickness()
    {
        var scale = Math.Abs(_canvasMatrix.M11);
        if (scale < 1e-9)
        {
            scale = 1.0;
        }

        return 2.0 / scale;
    }

    private double GetTransformedMarkerSize()
    {
        var scale = Math.Abs(_canvasMatrix.M11);
        if (scale < 1e-9)
        {
            scale = 1.0;
        }

        return 14.0 / scale;
    }

    private double GetTransformedTextScale()
    {
        var scale = Math.Abs(_canvasMatrix.M11);
        if (scale < 1e-9)
        {
            scale = 1.0;
        }

        return 1.0 / scale;
    }

    private static Color DarkenColor(Color color, double factor)
    {
        var clampedFactor = Math.Clamp(factor, 0.0, 1.0);
        return Color.FromRgb(
            (byte)Math.Clamp((int)Math.Round(color.R * clampedFactor), 0, 255),
            (byte)Math.Clamp((int)Math.Round(color.G * clampedFactor), 0, 255),
            (byte)Math.Clamp((int)Math.Round(color.B * clampedFactor), 0, 255));
    }

    private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        var current = child;
        while (current is not null)
        {
            if (current is T wanted)
            {
                return wanted;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T wanted)
            {
                return wanted;
            }

            var nested = FindVisualChild<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}

public sealed class WatermarkSizeConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double size || double.IsNaN(size) || size <= 0)
        {
            return 0d;
        }

        return Math.Min(size * 0.5, 512d);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
