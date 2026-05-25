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
using HelixToolkit.Wpf;
using Line = System.Windows.Shapes.Line;
using Ellipse = System.Windows.Shapes.Ellipse;

namespace DCMViewer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const double DefaultPerspectiveFieldOfView = 45.0;
    private readonly MainViewModel _viewModel;
    private readonly Brush _activeToolBrush = new SolidColorBrush(Color.FromRgb(214, 185, 92));
    private readonly Brush _activeToolForegroundBrush = new SolidColorBrush(Color.FromRgb(58, 49, 18));
    private double _zoomReferenceDistance = 1.0;
    private double _zoomReferenceOrthographicWidth = 1.0;
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
    private double _defaultNearPlaneDistance = 0.1;
    private double _defaultFarPlaneDistance = 100000;
    private bool _isSectionRefreshQueued;

    public void RestoreEmbeddedInteraction()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            Viewport.UpdateLayout();
            Viewport.IsZoomEnabled = true;
            Viewport.IsPanEnabled = true;
            Viewport.IsRotationEnabled = true;
            ConfigureRotationBehavior();
            SyncLightingToCamera();
            UpdateZoomPercentLabel();
            Viewport.Focus();
            Keyboard.Focus(Viewport);
        }));
    }

    public async Task LoadCaseFilesAsync(IEnumerable<DCMFileItem> files)
    {
        var fileItems = files
            .Where(x => !string.IsNullOrWhiteSpace(x.FilePath) && File.Exists(x.FilePath))
            .GroupBy(x => Path.GetFullPath(x.FilePath), StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();

        _viewModel.TextureOverrides.Clear();
        _viewModel.CategoryOverrides.Clear();
        _viewModel.IsExternalFileDropEnabled = false;

        foreach (var item in fileItems)
        {
            ApplyFileOverrides(item);
        }

        await _viewModel.LoadFilesAsync(fileItems.Select(x => x.FilePath), clearExisting: true);

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
        _viewModel.UnloadFile(fullPath);
        RestoreEmbeddedInteraction();
    }

    private void ApplyFileOverrides(DCMFileItem item)
    {
        var fullPath = Path.GetFullPath(item.FilePath);
        _viewModel.TextureOverrides[fullPath] = item.MaterialName;
        _viewModel.CategoryOverrides[fullPath] = item.SourceKind == DCMFileSourceKind.ModelScan
            ? "scan"
            : item.GroupName switch
            {
                "Model/Die" => "model",
                "Abutment" => "abutment",
                _ => "restoration"
            };
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

    public void SetCanvasBackgroundTransparent(bool isTransparent)
    {
        WatermarkCanvas.Background = isTransparent ? Brushes.Transparent : WatermarkCanvas.Background;
    }

    public MainWindow()
    {
        InitializeComponent();

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
        _viewModel.PropertyChanged += ViewModelOnPropertyChanged;
        _viewModel.LoadedFiles.CollectionChanged += LoadedFilesOnCollectionChanged;
        DataContext = _viewModel;
        SetDefaultProjectionMode();

        if (FindName("ProjectionToggleButton") is Button projectionToggleButton)
        {
            projectionToggleButton.Click += ProjectionToggleButton_OnClick;
        }

        UpdateProjectionToggleButtonState();
        UpdateClippingToggleButtonState();

        ConfigureRotationBehavior();

        CompositionTarget.Rendering += OnCompositionTargetRendering;
        Closed += (_, _) => CompositionTarget.Rendering -= OnCompositionTargetRendering;
        SyncLightingToCamera();
        UpdateZoomPercentLabel();
        Viewport.MouseLeftButtonDown += Viewport_OnMouseLeftButtonDown;
        SectionProfileCanvas.SizeChanged += (_, _) =>
        {
            if (_viewModel.IsSectionMode)
            {
                UpdateSectionProfileView(_activeSectionPlanePoint, _activeSectionPlaneNormal, resetCanvasTransform: false);
            }
        };

        var startupScanPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "scan.dcm"));
        if (File.Exists(startupScanPath))
        {
            _ = _viewModel.LoadFileAsync(startupScanPath);
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
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        }
        else
        {
            RenderOptions.SetBitmapScalingMode(Viewport, BitmapScalingMode.HighQuality);
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;
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
        if (Viewport.Camera is not ProjectionCamera currentCamera)
        {
            return;
        }

        var fieldOfView = currentCamera is PerspectiveCamera perspective
            ? perspective.FieldOfView
            : DefaultPerspectiveFieldOfView;
        var orthographicWidth = CalculateOrthographicWidth(currentCamera.LookDirection.Length, fieldOfView);

        var orthographicCamera = new OrthographicCamera
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
        if (string.Equals(e.PropertyName, nameof(MainViewModel.ModelGroup), StringComparison.Ordinal))
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
                    new Action(() => ShowAllButton_Click(this, new RoutedEventArgs())));
            }
        }

        if (string.Equals(e.PropertyName, nameof(MainViewModel.IsSectionMode), StringComparison.Ordinal))
        {
            ApplySectionModeFromViewModel();
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

            SectionCutGroup.CuttingPlanes.Clear();
            SectionCutGroup.CuttingPlanes.Add(new Plane3D(_activeSectionPlanePoint, _activeSectionPlaneNormal));
            UpdateSectionPlaneVisual(_activeSectionPlanePoint, _activeSectionPlaneNormal);
            UpdateSectionProfileView(_activeSectionPlanePoint, _activeSectionPlaneNormal, resetCanvasTransform: false);
        }));
    }

    private void LoadedFilesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is not (NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Reset))
        {
            return;
        }

        _pendingShowAllAfterModelUpdate = true;
    }

    private void ConfigureRotationBehavior()
    {
        var controller = Viewport.CameraController;
        if (controller is null)
        {
            return;
        }

        ConfigureInteractionCursors(controller);

        var bounds = GetVisibleBounds();
        if (bounds.IsEmpty)
        {
            return;
        }

        var center = new Point3D(
            bounds.X + (bounds.SizeX / 2.0),
            bounds.Y + (bounds.SizeY / 2.0),
            bounds.Z + (bounds.SizeZ / 2.0));

        controller.FixedRotationPointEnabled = true;
        controller.FixedRotationPoint = center;
        controller.RotateAroundMouseDownPoint = false;

        if (Viewport.Camera is ProjectionCamera camera)
        {
            _zoomReferenceDistance = GetDistance(camera.Position, center);
            if (camera is OrthographicCamera orthographicCamera)
            {
                _zoomReferenceOrthographicWidth = Math.Max(orthographicCamera.Width, 1e-9);
            }
        }

        if (!_viewModel.IsSectionMode)
        {
            RefreshSectionReferenceFromBounds(bounds);
        }
        UpdateZoomPercentLabel();
    }

    private static void ConfigureInteractionCursors(CameraController controller)
    {
        controller.PanCursor = Cursors.Arrow;
        controller.RotateCursor = Cursors.Arrow;
    }

    private void ShowAllButton_Click(object sender, RoutedEventArgs e)
    {
        Viewport.ZoomExtents(250);
        ConfigureRotationBehavior();
        SyncLightingToCamera();
    }

    private void ProjectionToggleButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Viewport.Camera is not ProjectionCamera currentCamera)
        {
            return;
        }

        if (_isOrthographicView)
        {
            var perspectiveCamera = new PerspectiveCamera
            {
                Position = currentCamera.Position,
                LookDirection = currentCamera.LookDirection,
                UpDirection = currentCamera.UpDirection,
                FieldOfView = DefaultPerspectiveFieldOfView
            };

            BindCameraPose(perspectiveCamera);
            Viewport.Camera = perspectiveCamera;
            _isOrthographicView = false;
            CaptureClipDefaults(perspectiveCamera);
            ApplyClipMode(perspectiveCamera);
        }
        else
        {
            var fieldOfView = currentCamera is PerspectiveCamera perspective ? perspective.FieldOfView : DefaultPerspectiveFieldOfView;
            var orthographicWidth = CalculateOrthographicWidth(currentCamera.LookDirection.Length, fieldOfView);

            var orthographicCamera = new OrthographicCamera
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
        if (Viewport.Camera is ProjectionCamera camera)
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
        clippingToggleButton.Content = CreateToolbarIcon("/DcmViewer/Images/clipping.png");
        clippingToggleButton.Background = _isNearClippingRelaxed ? _activeToolBrush : null;
        clippingToggleButton.Foreground = _isNearClippingRelaxed ? _activeToolForegroundBrush : Brushes.Black;
    }

    private void CaptureClipDefaults(ProjectionCamera camera)
    {
        _defaultNearPlaneDistance = Math.Clamp(camera.NearPlaneDistance, 0.0005, 0.02);
        _defaultFarPlaneDistance = Math.Max(camera.FarPlaneDistance, _defaultNearPlaneDistance + 1);
    }

    private void ApplyClipMode(ProjectionCamera camera)
    {
        if (_isNearClippingRelaxed)
        {
            var distance = Math.Max(camera.LookDirection.Length, 1e-3);
            var relaxedNear = Math.Max(distance * 0.00002, 0.00001);
            camera.NearPlaneDistance = relaxedNear;
            camera.FarPlaneDistance = Math.Max(_defaultFarPlaneDistance, distance * 10000.0);
            return;
        }

        camera.NearPlaneDistance = _defaultNearPlaneDistance;
        camera.FarPlaneDistance = _defaultFarPlaneDistance;
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
        projectionToggleButton.Content = CreateToolbarIcon("/DcmViewer/Images/perspective.png");
    }

    private static Image CreateToolbarIcon(string relativePath)
    {
        var image = new Image
        {
            Width = 16,
            Height = 16,
            Stretch = Stretch.Uniform,
            Source = new BitmapImage(new Uri($"pack://application:,,,{relativePath}", UriKind.Absolute))
        };

        return image;
    }

    private void BindCameraPose(ProjectionCamera camera)
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

        BindingOperations.SetBinding(camera, ProjectionCamera.PositionProperty, positionBinding);
        BindingOperations.SetBinding(camera, ProjectionCamera.LookDirectionProperty, lookBinding);
        BindingOperations.SetBinding(camera, ProjectionCamera.UpDirectionProperty, upBinding);
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
        if (Viewport.Camera is ProjectionCamera camera && _isNearClippingRelaxed)
        {
            ApplyClipMode(camera);
        }

        SyncLightingToCamera();
        UpdateZoomPercentLabel();
    }

    private void SyncLightingToCamera()
    {
        if (Viewport.Camera is not ProjectionCamera camera)
        {
            return;
        }

        _viewModel.UpdateLightRigFromCamera(camera.LookDirection, camera.UpDirection);
    }

    private void UpdateZoomPercentLabel()
    {
        if (Viewport.Camera is not ProjectionCamera camera)
        {
            return;
        }

        var bounds = GetVisibleBounds();
        if (bounds.IsEmpty)
        {
            ZoomPercentText.Text = "Zoom: 100%";
            return;
        }

        var center = new Point3D(
            bounds.X + (bounds.SizeX / 2.0),
            bounds.Y + (bounds.SizeY / 2.0),
            bounds.Z + (bounds.SizeZ / 2.0));

        if (camera is OrthographicCamera orthographicCamera)
        {
            var currentWidth = Math.Max(orthographicCamera.Width, 1e-9);
            var ratio = _zoomReferenceOrthographicWidth / currentWidth;
            if (_zoomReferenceOrthographicWidth < 1e-9 || ratio < 0.05 || ratio > 20)
            {
                _zoomReferenceOrthographicWidth = currentWidth;
                ratio = 1.0;
            }

            var orthographicZoomPercent = ratio * 100.0;
            var orthographicRoundedPercent = Math.Clamp((int)Math.Round(orthographicZoomPercent), 1, 9999);
            ZoomPercentText.Text = $"Zoom: {orthographicRoundedPercent}%";
            return;
        }

        var currentDistance = GetDistance(camera.Position, center);
        if (_zoomReferenceDistance < 1e-9)
        {
            _zoomReferenceDistance = currentDistance;
        }

        var zoomPercent = (_zoomReferenceDistance / Math.Max(currentDistance, 1e-9)) * 100.0;
        var roundedPercent = Math.Clamp((int)Math.Round(zoomPercent), 1, 9999);
        ZoomPercentText.Text = $"Zoom: {roundedPercent}%";
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

        if (!_viewModel.IsSectionMode)
        {
            SectionCutGroup.IsEnabled = false;
            SectionPlaneVisual.Content = null;
            SectionPlaneOutlineVisual.Points = new Point3DCollection();
            SectionPlaneOutlineVisual.IsRendering = false;
            SectionProfilePanel.Visibility = Visibility.Collapsed;
            SectionProfileCanvas.Children.Clear();
            _hasSectionProjectionMap = false;
            UpdateMeasurementVisual();
            return;
        }

        SectionProfilePanel.Visibility = Visibility.Visible;
        SectionProfileHintText.Text = "Click on mesh to place section plane";
        SectionProfileHintText.Visibility = Visibility.Visible;

        var bounds = GetVisibleBounds();
        if (!bounds.IsEmpty)
        {
            RefreshSectionReferenceFromBounds(bounds);
        }

        UpdateSectionPlane();
    }

    private void Viewport_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_viewModel.IsSectionMode)
        {
            return;
        }

        var sectionHits = Viewport3DHelper.FindHits(Viewport.Viewport, e.GetPosition(Viewport));
        var sectionHit = sectionHits.FirstOrDefault(h => h.Model is GeometryModel3D);
        if (sectionHit is null)
        {
            return;
        }

        _sectionCenter = sectionHit.Position;
        if (Viewport.Camera is ProjectionCamera sectionCamera)
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

        // Always anchor a new plane exactly at the click point. Resetting offset to zero
        // re-centers the slider so prior offset does not shift the new placement.
        _viewModel.SectionOffset = 0;

        SectionCutGroup.IsEnabled = true;
        UpdateSectionPlane();
        e.Handled = true;
        return;
    }

    private void UpdateMeasurementVisual()
    {
        MeasurementLine.IsRendering = true;
        MeasurementText.IsRendering = true;

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
        if (SectionCutGroup is null)
        {
            return;
        }

        if (!_viewModel.IsSectionMode)
        {
            SectionCutGroup.IsEnabled = false;
            SectionPlaneVisual.Content = null;
            SectionPlaneOutlineVisual.Points = new Point3DCollection();
            SectionPlaneOutlineVisual.IsRendering = false;
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

        SectionCutGroup.IsEnabled = true;
        SectionCutGroup.CuttingPlanes.Clear();
        SectionCutGroup.CuttingPlanes.Add(new Plane3D(planePosition, normal));

        UpdateSectionPlaneVisual(planePosition, normal);
        UpdateSectionProfileView(planePosition, normal, resetCanvasTransform: false);
    }

    private void UpdateSectionPlaneVisual(Point3D center, Vector3D normal)
    {
        var up = new Vector3D(0, 1, 0);
        if (Viewport.Camera is ProjectionCamera camera && camera.UpDirection.LengthSquared > 1e-9)
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
        var positions = new Point3DCollection(segments + 1)
        {
            center
        };

        for (var i = 0; i < segments; i++)
        {
            var angle = (Math.PI * 2.0 * i) / segments;
            var point = center + (axisX * (Math.Cos(angle) * radius)) + (axisY * (Math.Sin(angle) * radius));
            positions.Add(point);
        }

        var triangles = new Int32Collection(segments * 3);
        for (var i = 0; i < segments; i++)
        {
            var current = i + 1;
            var next = ((i + 1) % segments) + 1;
            triangles.Add(0);
            triangles.Add(current);
            triangles.Add(next);
        }

        var mesh = new MeshGeometry3D
        {
            Positions = positions,
            TriangleIndices = triangles
        };

        // Use EmissiveMaterial only — not affected by scene lighting, renders exact ARGB colour.
        // #00807D at higher opacity for darker appearance
        var planeBrush = new SolidColorBrush(Color.FromArgb(200, 0, 128, 125));
        var material = new EmissiveMaterial(planeBrush);

        var planeModel = new GeometryModel3D
        {
            Geometry = mesh,
            Material = material,
            BackMaterial = material
        };

        SectionPlaneVisual.Content = planeModel;

        // LinesVisual3D draws independent segments from consecutive pairs, so each
        // edge of the circle must be encoded as (p_i, p_{i+1}) — two points per segment.
        var normalOffset = normal * 0.05;
        var ringPts = new Point3D[segments];
        for (var i = 0; i < segments; i++)
        {
            var angle = (Math.PI * 2.0 * i) / segments;
            ringPts[i] = center + (axisX * (Math.Cos(angle) * radius)) + (axisY * (Math.Sin(angle) * radius)) + normalOffset;
        }

        var outlinePoints = new Point3DCollection(segments * 2);
        for (var i = 0; i < segments; i++)
        {
            outlinePoints.Add(ringPts[i]);
            outlinePoints.Add(ringPts[(i + 1) % segments]);
        }

        SectionPlaneOutlineVisual.Points = outlinePoints;
        SectionPlaneOutlineVisual.IsRendering = true;
    }

    private void UpdateToolButtonStates()
    {
        SectionToggleButton.Background = _viewModel.IsSectionMode ? _activeToolBrush : null;
        SectionToggleButton.Foreground = _viewModel.IsSectionMode ? _activeToolForegroundBrush : Brushes.Black;
        SectionToggleButton.Content = CreateToolbarIcon(_viewModel.IsSectionMode ? "/DcmViewer/Images/turnoffsection.png" : "/DcmViewer/Images/sectionmode.png");

        MeasureToggleButton.Background = _viewModel.IsMeasureMode ? _activeToolBrush : null;
        MeasureToggleButton.Foreground = _viewModel.IsMeasureMode ? _activeToolForegroundBrush : Brushes.Black;
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

        var normal = planeNormal;
        if (normal.LengthSquared < 1e-9)
        {
            normal = new Vector3D(0, 0, 1);
        }
        normal.Normalize();

        var axisY = new Vector3D(0, 1, 0);
        if (Viewport.Camera is ProjectionCamera camera && camera.UpDirection.LengthSquared > 1e-9)
        {
            axisY = camera.UpDirection;
        }
        axisY.Normalize();

        var axisX = Vector3D.CrossProduct(normal, axisY);
        if (axisX.LengthSquared < 1e-9)
        {
            axisX = Vector3D.CrossProduct(normal, new Vector3D(1, 0, 0));
        }
        axisX.Normalize();
        axisY = Vector3D.CrossProduct(axisX, normal);
        axisY.Normalize();

        var segments2D = SectionGeometryService.BuildSectionSegments2D(_viewModel.LoadedFiles, planePoint, normal, axisX, axisY);
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
        DrawMeasureMarker(startCanvas);

        if (_viewModel.MeasureEndSection is null)
        {
            return;
        }

        var endCanvas = SectionToCanvasPoint(_viewModel.MeasureEndSection.Value);
        DrawMeasureMarker(endCanvas);

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

    private void DrawMeasureMarker(Point point)
    {
        var markerSize = GetTransformedMarkerSize();
        var markerRadius = markerSize * 0.5;
        var marker = new Ellipse
        {
            Width = markerSize,
            Height = markerSize,
            Fill = Brushes.Black,
            Stroke = null
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
