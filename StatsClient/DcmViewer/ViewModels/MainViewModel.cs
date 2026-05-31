using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using DCMViewer.Infrastructure;
using DCMViewer.Services;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Microsoft.Win32;
using static StatsClient.MVVM.Core.LocalSettingsDB;

namespace DCMViewer.ViewModels;

public sealed partial class MainViewModel : INotifyPropertyChanged
{
    private const string HideLayerLabelsSettingKey = "DcmViewerHideLayerLabels";
    private const double LayerPanelWidthWithLabels = 330;
    private const double LayerPanelWidthWithoutLabels = 144;

    private const double SatinSpecularIntensity = 1.0;
    private const double MatteSpecularIntensity = 0.35;

    private const double SatinSpecularShininess = 90.0;
    private const double MatteSpecularShininess = 30.0;

    private static readonly Color AmbientLightColor = Color.FromRgb(30, 30, 30);
    private static readonly Color KeyLightColor = Color.FromRgb(92, 92, 92);
    private static readonly Color FillLightColor = Color.FromRgb(72, 72, 72);
    private static readonly Color RimLightColor = Color.FromRgb(46, 46, 46);

    /// <summary>Weak opposite-side key to keep concave scan areas from going pure black.</summary>
    private const double BackFillLightStrength = 0.275;

    private readonly DcmParser _parser;
    private readonly RelayCommand _openFileCommand;
    private readonly RelayCommand _exportVisibleMeshesCommand;
    private readonly RelayCommand _exportSeparatedMeshesCommand;
    private readonly RelayCommand _toggleFinishCommand;
    private readonly RelayCommand _toggleSectionModeCommand;
    private readonly RelayCommand _clearSectionMeasurementCommand;
    private readonly RelayCommand _toggleModelScanMaterialCommand;
    private readonly RelayCommand _toggleRestorationGroupExpandedCommand;
    private readonly RelayCommand _toggleAbutmentGroupExpandedCommand;
    private readonly RelayCommand _toggleHideLayerLabelsCommand;
    private readonly ObservableCollection<LoadedMeshItemViewModel> _loadedFiles = new();
    private readonly ObservableCollection<LoadedMeshItemViewModel> _restorationGroupFiles = new();
    private readonly ObservableCollection<LoadedMeshItemViewModel> _abutmentGroupFiles = new();
    private readonly ObservableCollection<LoadedMeshItemViewModel> _upperScanFiles = new();
    private readonly ObservableCollection<LoadedMeshItemViewModel> _lowerScanFiles = new();
    private readonly ObservableCollection<LoadedMeshItemViewModel> _miscScanFiles = new();
    private readonly ObservableCollection<LoadedMeshItemViewModel> _otherLoadedFiles = new();
    private readonly AmbientLight3D _ambientLight = new();
    private readonly DirectionalLight3D _keyLight = new();
    private readonly DirectionalLight3D _fillLight = new();
    private readonly DirectionalLight3D _rimLight = new();
    private readonly DirectionalLight3D _backFillLight = new();
    private readonly ObservableElement3DCollection _sceneItems = new();
    private bool _lightsAdded;

    private Point3D _cameraPosition = new(0, 0, 500);
    private Vector3D _cameraLookDirection = new(0, 0, -500);
    private Vector3D _cameraUpDirection = new(0, 1, 0);
    private const int StatusResetDelaySeconds = 15;

    private string _statusText = "";
    private DispatcherTimer? _statusResetTimer;
    private bool _isBusy;
    private CancellationTokenSource? _busyCancellationSource;
    private double _loadProgress;
    private double _lightingStrength = 1.0;
    private bool _isMatteFinish;
    private bool _isSectionMode;
    private double _sectionOffset;
    private Point? _measureStartSection;
    private Point? _measureEndSection;
    private string _defaultTextureName = MaterialLibrary.DefaultName;
    private bool _restorationGroupVisible = true;
    private bool _abutmentGroupVisible = true;
    private double _restorationGroupOpacity = 1.0;
    private double _abutmentGroupOpacity = 1.0;
    private bool _isRestorationGroupExpanded;
    private bool _isAbutmentGroupExpanded;
    private bool _isExternalFileDropEnabled = true;
    private bool _suppressAutomaticCameraFraming;
    private bool _useStoneForModelScans;
    private int _bulkVisualUpdateNesting;
    private bool _isVisualRefreshPending;
    private bool _hideLayerLabels;

    public MainViewModel(DcmParser parser)
    {
        _ = bool.TryParse(ReadLocalSetting(HideLayerLabelsSettingKey), out _hideLayerLabels);

        _parser = parser;
        _openFileCommand = new RelayCommand(OpenFile, () => !IsBusy);
        _exportVisibleMeshesCommand = new RelayCommand(ExportVisibleMeshes, CanExportVisibleMeshes);
        _exportSeparatedMeshesCommand = new RelayCommand(ExportSeparatedMeshes, CanExportVisibleMeshes);
        _toggleFinishCommand = new RelayCommand(ToggleFinish);
        _toggleSectionModeCommand = new RelayCommand(ToggleSectionMode);
        _clearSectionMeasurementCommand = new RelayCommand(ClearSectionMeasurement);
        _toggleModelScanMaterialCommand = new RelayCommand(ToggleModelScanMaterial);
        _toggleRestorationGroupExpandedCommand = new RelayCommand(ToggleRestorationGroupExpanded);
        _toggleAbutmentGroupExpandedCommand = new RelayCommand(ToggleAbutmentGroupExpanded);
        _toggleHideLayerLabelsCommand = new RelayCommand(ToggleHideLayerLabels);
        InitSculptCommands();
        _loadedFiles.CollectionChanged += LoadedFilesOnCollectionChanged;

        EffectsManager = new DefaultEffectsManager();
        InitializeLightRig();
        ApplyLightingStrength();
        UpdateLightRigFromCamera(_cameraLookDirection, _cameraUpDirection);
        RebuildSceneItems();
    }

    public IEffectsManager EffectsManager { get; }

    public void DisposeEffectsManager()
    {
        if (EffectsManager is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand OpenFileCommand => _openFileCommand;

    public ICommand ToggleHideLayerLabelsCommand => _toggleHideLayerLabelsCommand;

    /// <summary>When true, layer list file/group labels are hidden and sliders use a fixed width.</summary>
    public bool HideLayerLabels
    {
        get => _hideLayerLabels;
        set
        {
            if (_hideLayerLabels == value)
            {
                return;
            }

            _hideLayerLabels = value;
            WriteLocalSetting(HideLayerLabelsSettingKey, value.ToString());
            if (value)
            {
                IsRestorationGroupExpanded = false;
                IsAbutmentGroupExpanded = false;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(LayerPanelWidth));
            OnPropertyChanged(nameof(LayerRowLabelColumnWidth));
            OnPropertyChanged(nameof(LayerRowSliderColumnWidth));
        }
    }

    public double LayerPanelWidth => HideLayerLabels ? LayerPanelWidthWithoutLabels : LayerPanelWidthWithLabels;

    public GridLength LayerRowLabelColumnWidth =>
        HideLayerLabels ? new GridLength(0) : new GridLength(1, GridUnitType.Star);

    public GridLength LayerRowSliderColumnWidth =>
        HideLayerLabels ? new GridLength(1, GridUnitType.Star) : new GridLength(110);

    /// <summary>
    /// Per-filename texture overrides.  Key = filename (e.g. "teeth.dcm"), Value = texture name
    /// from <see cref="MaterialLibrary"/> (e.g. "Zirconia").  Files not present here use the
    /// metadata-heuristic then the <see cref="DefaultTextureName"/> fallback.
    /// Populate this from the host application before or after loading files.
    /// </summary>
    public Dictionary<string, string> TextureOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>All available texture names (for ComboBox binding).</summary>
    public IReadOnlyList<string> AvailableTextures => MaterialLibrary.Names;

    /// <summary>
    /// Per-filename category assignment. Key = filename (for example, "r1.dcm"),
    /// Value = one of: model, scan, restoration, abutment.
    /// </summary>
    public Dictionary<string, string> CategoryOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Per-file arch assignment for scan/model layer list grouping (Upper/Lower).</summary>
    public Dictionary<string, ScanLayerArch> ArchOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Enables or disables drag-and-drop loading of external files (.dcm/.stl/.xml).
    /// Set to false when hosting the viewer in apps that must block user file drops.
    /// </summary>
    public bool IsExternalFileDropEnabled
    {
        get => _isExternalFileDropEnabled;
        set
        {
            if (_isExternalFileDropEnabled == value)
            {
                return;
            }

            _isExternalFileDropEnabled = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// When true, <see cref="FrameCameraToBounds"/> is skipped so the host can set zoom (e.g. Order Info 66%).
    /// </summary>
    public bool SuppressAutomaticCameraFraming
    {
        get => _suppressAutomaticCameraFraming;
        set => _suppressAutomaticCameraFraming = value;
    }

    public bool HasRestorationGroup => _loadedFiles.Any(item => item.Category == MeshCategory.Restoration);

    public bool HasAbutmentGroup => _loadedFiles.Any(item => item.Category == MeshCategory.Abutment);

    public bool HasRestorationSection => _loadedFiles.Any(item => item.Category == MeshCategory.Restoration);

    public bool HasAbutmentSection => _loadedFiles.Any(item => item.Category == MeshCategory.Abutment);

    public bool ShowRestorationGroupHeader => RestorationGroupCount > 1;

    public bool ShowAbutmentGroupHeader => AbutmentGroupCount > 1;

    public bool ShowRestorationGroupExpander => RestorationGroupCount > 1;

    public bool ShowAbutmentGroupExpander => AbutmentGroupCount > 1;

    public int RestorationGroupCount => _loadedFiles.Count(item => item.Category == MeshCategory.Restoration);

    public int AbutmentGroupCount => _loadedFiles.Count(item => item.Category == MeshCategory.Abutment);

    /// <summary>True when every file in the restoration group uses maroon failed/encrypted styling.</summary>
    public bool RestorationGroupUsesFailedStyle =>
        _restorationGroupFiles.Count > 0 && _restorationGroupFiles.All(item => item.UsesFailedLayerStyle);

    /// <summary>True when every file in the abutment group uses maroon failed/encrypted styling.</summary>
    public bool AbutmentGroupUsesFailedStyle =>
        _abutmentGroupFiles.Count > 0 && _abutmentGroupFiles.All(item => item.UsesFailedLayerStyle);

    public ObservableCollection<LoadedMeshItemViewModel> RestorationGroupFiles => _restorationGroupFiles;

    public ObservableCollection<LoadedMeshItemViewModel> AbutmentGroupFiles => _abutmentGroupFiles;

    public ObservableCollection<LoadedMeshItemViewModel> UpperScanFiles => _upperScanFiles;

    public ObservableCollection<LoadedMeshItemViewModel> LowerScanFiles => _lowerScanFiles;

    public ObservableCollection<LoadedMeshItemViewModel> MiscScanFiles => _miscScanFiles;

    public ObservableCollection<LoadedMeshItemViewModel> OtherLoadedFiles => _otherLoadedFiles;

    public bool HasUpperScanSection => _upperScanFiles.Count > 0;

    public bool HasLowerScanSection => _lowerScanFiles.Count > 0;

    public bool HasMiscScanSection => _miscScanFiles.Count > 0;

    public bool RestorationGroupVisible
    {
        get => _restorationGroupVisible;
        set
        {
            if (_restorationGroupVisible == value)
            {
                return;
            }

            _restorationGroupVisible = value;
            ApplyCategoryVisibility(MeshCategory.Restoration, value);
            OnPropertyChanged();
        }
    }

    public bool AbutmentGroupVisible
    {
        get => _abutmentGroupVisible;
        set
        {
            if (_abutmentGroupVisible == value)
            {
                return;
            }

            _abutmentGroupVisible = value;
            ApplyCategoryVisibility(MeshCategory.Abutment, value);
            OnPropertyChanged();
        }
    }

    public double RestorationGroupOpacity
    {
        get => _restorationGroupOpacity;
        set
        {
            var clamped = Math.Clamp(value, 0.0, 1.0);
            if (Math.Abs(_restorationGroupOpacity - clamped) < 0.0001)
            {
                return;
            }

            _restorationGroupOpacity = clamped;
            ApplyCategoryOpacity(MeshCategory.Restoration, clamped);
            OnPropertyChanged();
        }
    }

    public double AbutmentGroupOpacity
    {
        get => _abutmentGroupOpacity;
        set
        {
            var clamped = Math.Clamp(value, 0.0, 1.0);
            if (Math.Abs(_abutmentGroupOpacity - clamped) < 0.0001)
            {
                return;
            }

            _abutmentGroupOpacity = clamped;
            ApplyCategoryOpacity(MeshCategory.Abutment, clamped);
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// The fallback texture used for files with no explicit override and no metadata hint.
    /// Changing this live re-colors all such already-loaded files.
    /// </summary>
    public string DefaultTextureName
    {
        get => _defaultTextureName;
        set
        {
            if (string.Equals(_defaultTextureName, value, StringComparison.OrdinalIgnoreCase) ||
                !MaterialLibrary.Contains(value))
                return;
            var previous = _defaultTextureName;
            _defaultTextureName = value;
            OnPropertyChanged();
            ReapplyDefaultTexture(previous, value);
        }
    }

    public ICommand ExportVisibleMeshesCommand => _exportVisibleMeshesCommand;

    public ICommand ExportSeparatedMeshesCommand => _exportSeparatedMeshesCommand;

    public ICommand ToggleFinishCommand => _toggleFinishCommand;

    public ICommand ToggleSectionModeCommand => _toggleSectionModeCommand;

    public ICommand ClearSectionMeasurementCommand => _clearSectionMeasurementCommand;

    public ICommand ToggleModelScanMaterialCommand => _toggleModelScanMaterialCommand;

    public Func<IReadOnlyList<FuseInnerSidePickItem>, Task<IReadOnlyList<FuseInnerSideHint>?>>? FuseInnerSidePickHandler { get; set; }

    public ICommand ToggleRestorationGroupExpandedCommand => _toggleRestorationGroupExpandedCommand;

    public ICommand ToggleAbutmentGroupExpandedCommand => _toggleAbutmentGroupExpandedCommand;

    public bool IsRestorationGroupExpanded
    {
        get => _isRestorationGroupExpanded;
        private set
        {
            if (_isRestorationGroupExpanded == value)
            {
                return;
            }

            _isRestorationGroupExpanded = value;
            OnPropertyChanged();
        }
    }

    public bool IsAbutmentGroupExpanded
    {
        get => _isAbutmentGroupExpanded;
        private set
        {
            if (_isAbutmentGroupExpanded == value)
            {
                return;
            }

            _isAbutmentGroupExpanded = value;
            OnPropertyChanged();
        }
    }

    public string ModelScanMaterialToggleTooltip => _useStoneForModelScans
        ? "Model scan material: Stone (click to switch to Model)"
        : "Model scan material: Model (click to switch to Stone)";

    public string FinishLabel => _isMatteFinish ? "Matte" : "Satin";

    public bool IsSectionMode
    {
        get => _isSectionMode;
        private set
        {
            if (_isSectionMode == value)
            {
                return;
            }

            _isSectionMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsMeasureMode));
        }
    }

    public bool IsMeasureMode => IsSectionMode;

    public bool HasSectionMeasurementPoint => MeasureStartSection is not null;

    public Point? MeasureStartSection
    {
        get => _measureStartSection;
        private set
        {
            if (_measureStartSection == value)
            {
                return;
            }

            _measureStartSection = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSectionMeasurementPoint));
        }
    }

    public Point? MeasureEndSection
    {
        get => _measureEndSection;
        private set
        {
            if (_measureEndSection == value)
            {
                return;
            }

            _measureEndSection = value;
            OnPropertyChanged();
        }
    }

    public double SectionOffset
    {
        get => _sectionOffset;
        set
        {
            var clamped = Math.Clamp(value, -1.0, 1.0);
            if (Math.Abs(_sectionOffset - clamped) < 0.0001)
            {
                return;
            }

            _sectionOffset = clamped;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<LoadedMeshItemViewModel> LoadedFiles => _loadedFiles;

    public ObservableElement3DCollection SceneItems => _sceneItems;

    public Point3D CameraPosition
    {
        get => _cameraPosition;
        private set
        {
            if (_cameraPosition == value)
            {
                return;
            }

            _cameraPosition = value;
            OnPropertyChanged();
        }
    }

    public Vector3D CameraLookDirection
    {
        get => _cameraLookDirection;
        private set
        {
            if (_cameraLookDirection == value)
            {
                return;
            }

            _cameraLookDirection = value;
            OnPropertyChanged();
        }
    }

    public Vector3D CameraUpDirection
    {
        get => _cameraUpDirection;
        private set
        {
            if (_cameraUpDirection == value)
            {
                return;
            }

            _cameraUpDirection = value;
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText == value)
            {
                return;
            }

            _statusText = value;
            OnPropertyChanged();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
            {
                return;
            }

            _isBusy = value;
            OnPropertyChanged();
            _openFileCommand.RaiseCanExecuteChanged();
            _exportVisibleMeshesCommand.RaiseCanExecuteChanged();
            _exportSeparatedMeshesCommand.RaiseCanExecuteChanged();
        }
    }

    public double LoadProgress
    {
        get => _loadProgress;
        private set
        {
            var clampedValue = Math.Clamp(value, 0.0, 1.0);
            if (Math.Abs(_loadProgress - clampedValue) < 0.0001)
            {
                return;
            }

            _loadProgress = clampedValue;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LoadProgressPercentText));
        }
    }

    public string LoadProgressPercentText => $"{LoadProgress:P0}";

    public CancellationToken BusyCancellationToken =>
        _busyCancellationSource?.Token ?? CancellationToken.None;

    public bool IsBusyCancellationRequested =>
        _busyCancellationSource?.IsCancellationRequested == true;

    public CancellationToken BeginCancellableBusy(string statusText, bool resetProgress = true)
    {
        EndBusy(suppressPropertyChange: false);
        _busyCancellationSource = new CancellationTokenSource();
        if (resetProgress)
        {
            LoadProgress = 0;
        }

        IsBusy = true;
        StatusText = statusText;
        return _busyCancellationSource.Token;
    }

    public void CancelBusyWork()
    {
        if (_busyCancellationSource is null && !IsBusy)
        {
            return;
        }

        try
        {
            _busyCancellationSource?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        StatusText = "Canceled.";
        EndBusy();
    }

    private void EndBusy(bool suppressPropertyChange = false)
    {
        if (_busyCancellationSource is not null)
        {
            _busyCancellationSource.Dispose();
            _busyCancellationSource = null;
        }

        if (!IsBusy)
        {
            return;
        }

        if (suppressPropertyChange)
        {
            _isBusy = false;
            return;
        }

        IsBusy = false;
    }

    public Task LoadFileAsync(string filePath)
        => LoadFilesAsync(new[] { filePath }, clearExisting: false);

    public void UpdateLightRigFromCamera(Vector3D lookDirection, Vector3D upDirection)
    {
        var look = NormalizeDirection(lookDirection);
        var up = NormalizeDirection(upDirection);

        var right = Vector3D.CrossProduct(look, up);
        if (right.LengthSquared < 1e-9)
        {
            right = new Vector3D(1, 0, 0);
        }
        right.Normalize();

        // Camera-following rig: primary light from viewer plus weaker fill/rim offsets.
        _keyLight.Direction = look;
        _fillLight.Direction = NormalizeDirection(look + (up * 0.38) - (right * 0.26));
        _rimLight.Direction = NormalizeDirection(look - (up * 0.55) + (right * 0.33));
        _backFillLight.Direction = NormalizeDirection(-look);
    }

    public async Task LoadFilesAsync(IEnumerable<string> filePaths, bool clearExisting, CancellationToken cancellationToken = default)
    {
        var normalizedPaths = filePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            // Load scans first so we can determine scan transform direction before CAD.
            .OrderByDescending(p => p.Contains(@"\Scans\", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (normalizedPaths.Count == 0)
        {
            StatusText = "No files selected.";
            return;
        }

        var managesBusyScope = false;
        try
        {
            if (IsBusy)
            {
                cancellationToken = cancellationToken == default
                    ? BusyCancellationToken
                    : cancellationToken;
                managesBusyScope = true;
                StatusText = "Loading scans..";
                LoadProgress = 0;
            }
            else if (cancellationToken == default)
            {
                cancellationToken = BeginCancellableBusy("Loading scans..");
                managesBusyScope = true;
            }
            else
            {
                _busyCancellationSource?.Dispose();
                _busyCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                IsBusy = true;
                LoadProgress = 0;
                cancellationToken = _busyCancellationSource.Token;
                managesBusyScope = true;
            }

            StatusText = "Loading scans..";

            if (clearExisting)
            {
                foreach (var loadedFile in _loadedFiles)
                {
                    loadedFile.PropertyChanged -= LoadedFileOnPropertyChanged;
                }

                _loadedFiles.Clear();
            }

            var loadedNow = 0;
            var failedNow = 0;
            var processedNow = 0;
            var totalFiles = normalizedPaths.Count;

            foreach (var filePath in normalizedPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                processedNow++;
                if (!File.Exists(filePath))
                {
                    LoadProgress = (double)processedNow / totalFiles;
                    continue;
                }

                if (_loadedFiles.Any(item => item.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                {
                    LoadProgress = (double)processedNow / totalFiles;
                    continue;
                }

                var fallbackCategory = ResolveMeshCategory(filePath, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
                var sceneTransformKind = ResolveSceneTransformKind(filePath, fallbackCategory);

                try
                {
                    var decodeMode = ResolveCoordinateDecodingMode(filePath);

                    var parsed = await Task.Run(
                        () => _parser.ParseFile(
                            filePath,
                            decodeMode,
                            applySceneTransform: sceneTransformKind != SceneTransformKind.None,
                            sceneTransformKind: sceneTransformKind),
                        cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();

                    var category = ResolveMeshCategory(filePath, parsed.Properties);
                    var (palette, textureName) = ResolveMaterialPalette(filePath, parsed.Properties, category);

                    var geometry = SharpDxMeshFactory.CreateGeometry(parsed.Mesh);
                    var meshModel = new MeshGeometryModel3D
                    {
                        Geometry = geometry
                    };

                    var scanArch = ResolveScanArch(filePath);
                    var loadedFile = new LoadedMeshItemViewModel(
                        filePath,
                        meshModel,
                        parsed.Mesh,
                        palette,
                        parsed.Bounds,
                        parsed.VertexCount,
                        parsed.TriangleCount,
                        parsed.IsEncrypted,
                        parsed.Properties.ContainsKey("PackageLockList"),
                        category,
                        scanArch,
                        appliedTextureName: textureName,
                        writeProfile: parsed.WriteProfile);

                    if (loadedFile.Category == MeshCategory.Restoration)
                    {
                        loadedFile.Opacity = _restorationGroupOpacity;
                        loadedFile.IsVisible = _restorationGroupVisible;
                    }
                    else if (loadedFile.Category == MeshCategory.Abutment)
                    {
                        loadedFile.Opacity = _abutmentGroupOpacity;
                        loadedFile.IsVisible = _abutmentGroupVisible;
                    }

                    loadedFile.SetSpecularIntensity(GetSpecularIntensity());
                    loadedFile.SetSpecularShininess(GetSpecularShininess());
                    loadedFile.PropertyChanged += LoadedFileOnPropertyChanged;
                    _loadedFiles.Add(loadedFile);
                    loadedNow++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    var isPackageLocked = ex.Message.Contains("encrypted", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains("3shape", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains("proprietary encryption", StringComparison.OrdinalIgnoreCase);
                    var failedFile = LoadedMeshItemViewModel.CreateFailed(
                        filePath,
                        ex.Message,
                        fallbackCategory,
                        ResolveScanArch(filePath),
                        isPackageLocked: isPackageLocked,
                        isEncrypted: isPackageLocked);
                    _loadedFiles.Add(failedFile);
                    failedNow++;
                }

                LoadProgress = (double)processedNow / totalFiles;
            }

            cancellationToken.ThrowIfCancellationRequested();

            LoadProgress = 1;
            RebuildSceneItems();
            FrameCameraToBounds(GetVisibleBounds());

            var visibleVertexCount = _loadedFiles.Where(item => item.IsVisible).Sum(item => item.VertexCount);
            var visibleTriangleCount = _loadedFiles.Where(item => item.IsVisible).Sum(item => item.TriangleCount);

            if (loadedNow == 0 && failedNow == 0)
            {
                StatusText = $"No new files loaded. Loaded files: {_loadedFiles.Count:N0}";
            }
            else
            {
                StatusText = $"Loaded {loadedNow:N0} file(s) | Failed: {failedNow:N0} | Visible files: {_loadedFiles.Count(item => item.IsVisible):N0} | Vertices: {visibleVertexCount:N0} | Triangles: {visibleTriangleCount:N0}";
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Canceled.";
            throw;
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to load file: {ex.Message}";
        }
        finally
        {
            if (managesBusyScope)
            {
                EndBusy();
            }
        }
    }

    public bool UnloadFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(filePath);
        var loadedFile = _loadedFiles.FirstOrDefault(item =>
            string.Equals(Path.GetFullPath(item.FilePath), fullPath, StringComparison.OrdinalIgnoreCase));

        if (loadedFile is null)
        {
            return false;
        }

        loadedFile.PropertyChanged -= LoadedFileOnPropertyChanged;
        var removed = _loadedFiles.Remove(loadedFile);
        if (!removed)
        {
            return false;
        }

        RebuildSceneItems();
        FrameCameraToBounds(GetVisibleBounds());
        var visibleFilesCount = _loadedFiles.Count(item => item.IsVisible);
        var visibleVertexCount = _loadedFiles.Where(item => item.IsVisible).Sum(item => item.VertexCount);
        var visibleTriangleCount = _loadedFiles.Where(item => item.IsVisible).Sum(item => item.TriangleCount);
        StatusText = $"Visible files: {visibleFilesCount:N0} | Vertices: {visibleVertexCount:N0} | Triangles: {visibleTriangleCount:N0}";
        return true;
    }

    private void LoadedFileOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(LoadedMeshItemViewModel.IsVisible), StringComparison.Ordinal) &&
            !string.Equals(e.PropertyName, nameof(LoadedMeshItemViewModel.Opacity), StringComparison.Ordinal))
        {
            return;
        }

        RequestVisualRefresh();
    }

    private void LoadedFilesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildCategoryFileLists();
        _exportVisibleMeshesCommand.RaiseCanExecuteChanged();
        _exportSeparatedMeshesCommand.RaiseCanExecuteChanged();
        NotifyCategoryGroupPropertiesChanged();
    }

    private void RebuildCategoryFileLists()
    {
        _restorationGroupFiles.Clear();
        _abutmentGroupFiles.Clear();
        _upperScanFiles.Clear();
        _lowerScanFiles.Clear();
        _miscScanFiles.Clear();
        _otherLoadedFiles.Clear();

        foreach (var item in _loadedFiles)
        {
            switch (item.Category)
            {
                case MeshCategory.Restoration:
                    _restorationGroupFiles.Add(item);
                    break;
                case MeshCategory.Abutment:
                    _abutmentGroupFiles.Add(item);
                    break;
                case MeshCategory.Scan:
                case MeshCategory.Model:
                    switch (item.ScanArch)
                    {
                        case ScanLayerArch.Upper:
                            _upperScanFiles.Add(item);
                            break;
                        case ScanLayerArch.Lower:
                            _lowerScanFiles.Add(item);
                            break;
                        case ScanLayerArch.Misc:
                            _miscScanFiles.Add(item);
                            break;
                        default:
                            _otherLoadedFiles.Add(item);
                            break;
                    }

                    break;
                default:
                    _otherLoadedFiles.Add(item);
                    break;
            }
        }

        OnPropertyChanged(nameof(HasUpperScanSection));
        OnPropertyChanged(nameof(HasLowerScanSection));
        OnPropertyChanged(nameof(HasMiscScanSection));
    }

    private void NotifyCategoryGroupPropertiesChanged()
    {
        OnPropertyChanged(nameof(HasRestorationGroup));
        OnPropertyChanged(nameof(HasAbutmentGroup));
        OnPropertyChanged(nameof(HasRestorationSection));
        OnPropertyChanged(nameof(HasAbutmentSection));
        OnPropertyChanged(nameof(ShowRestorationGroupHeader));
        OnPropertyChanged(nameof(ShowAbutmentGroupHeader));
        OnPropertyChanged(nameof(ShowRestorationGroupExpander));
        OnPropertyChanged(nameof(ShowAbutmentGroupExpander));
        OnPropertyChanged(nameof(RestorationGroupCount));
        OnPropertyChanged(nameof(AbutmentGroupCount));
        OnPropertyChanged(nameof(RestorationGroupUsesFailedStyle));
        OnPropertyChanged(nameof(AbutmentGroupUsesFailedStyle));
    }

    private async void OpenFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "3D Files (*.dcm;*.stl)|*.dcm;*.stl|3Shape DCM (*.dcm)|*.dcm|STL Mesh (*.stl)|*.stl|XML files (*.xml)|*.xml|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            await LoadFilesAsync(dialog.FileNames, clearExisting: false);
        }
    }

    private bool CanExportVisibleMeshes()
    {
        return !IsBusy && _loadedFiles.Any(item => item.IsVisible && item.MeshSnapshot is not null);
    }

    private static Window? GetDialogOwner()
    {
        return Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive)
            ?? Application.Current?.MainWindow;
    }

    private async void ExportVisibleMeshes()
    {
        var exportableFiles = _loadedFiles
            .Where(item => item.IsVisible && item.MeshSnapshot is not null)
            .ToList();

        if (exportableFiles.Count == 0)
        {
            StatusText = "No visible mesh is available to export.";
            return;
        }

        FuseExportChoice? exportChoice = null;
        if (exportableFiles.Count > 1)
        {
            exportChoice = FuseExportChoiceWindow.ShowDialog(GetDialogOwner(), exportableFiles.Count);
            if (exportChoice is null)
            {
                return;
            }
        }

        var fuseMode = exportChoice?.Mode ?? MeshFuseSettings.Load().Mode;
        var cleanupSavedStl = exportChoice?.CleanupSavedStlArtifacts ?? false;
        var cleanupStrength = exportChoice?.CleanupStrength ?? MeshFuseSettings.LoadCleanupStrength();

        IReadOnlyList<FuseInnerSideHint>? innerSideHints = null;
        if (fuseMode == MeshFuseMode.UnifiedShell && exportableFiles.Count > 1)
        {
            if (FuseInnerSidePickHandler is null)
            {
                StatusText = "Unified shell needs inner-side picks, but the viewer is not ready.";
                return;
            }

            var pickItems = exportableFiles
                .Select((item, index) => new FuseInnerSidePickItem(index, item.DisplayName, item))
                .ToList();

            innerSideHints = await FuseInnerSidePickHandler(pickItems);
            if (innerSideHints is null)
            {
                SetTransientStatus("Unified shell export canceled.");
                return;
            }
        }

        var suggestedFileName = exportableFiles.Count == 1
            ? Path.GetFileNameWithoutExtension(exportableFiles[0].DisplayName)
            : "fused-meshes";

        var dialog = new SaveFileDialog
        {
            Filter = "STL Mesh (*.stl)|*.stl",
            DefaultExt = ".stl",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = suggestedFileName
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var cancellationToken = BeginCancellableBusy(
            BuildExportStatusText(exportableFiles.Count, fuseMode, stage: "starting"));

        try
        {
            ReportBusyProgress(0, BuildExportStatusText(exportableFiles.Count, fuseMode, stage: "starting"));

            var snapshots = exportableFiles
                .Select(item => item.MeshSnapshot!)
                .ToArray();

            var fuseProgress = new Progress<double>(fuseFraction =>
            {
                var mapped = fuseMode switch
                {
                    MeshFuseMode.UnifiedShell => 0.05 + (fuseFraction * 0.78),
                    MeshFuseMode.VoxelEnvelope => 0.10 + (fuseFraction * 0.70),
                    _ => 0.15 + (fuseFraction * 0.55)
                };
                ReportBusyProgress(mapped, BuildExportStatusText(exportableFiles.Count, fuseMode, stage: "merging"));
            });

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fused = MeshFuseService.Fuse(snapshots, fuseMode, innerSideHints, fuseProgress);
                cancellationToken.ThrowIfCancellationRequested();

                if (cleanupSavedStl && fuseMode == MeshFuseMode.UnifiedShell)
                {
                    ReportBusyProgress(0.87, BuildExportStatusText(exportableFiles.Count, fuseMode, stage: "cleanup"));
                    var maxIslandTriangles = MeshFuseOptions.MapCleanupStrengthToMaxIslandTriangles(cleanupStrength);
                    fused = MeshIslandCleanup.RemoveTinyIslands(fused, maxIslandTriangles);
                }

                cancellationToken.ThrowIfCancellationRequested();
                ReportBusyProgress(0.93, BuildExportStatusText(exportableFiles.Count, fuseMode, stage: "writing"));
                MeshExportService.Export(dialog.FileName, new[] { fused });
            }, cancellationToken);

            ReportBusyProgress(1.0);
            SetTransientStatus(exportableFiles.Count == 1
                ? $"Exported {Path.GetFileName(dialog.FileName)}"
                : $"Fused and exported {exportableFiles.Count:N0} visible mesh(es) to {Path.GetFileName(dialog.FileName)}");
        }
        catch (OperationCanceledException)
        {
            SetTransientStatus("Canceled.");
        }
        catch (Exception ex)
        {
            SetTransientStatus($"Failed to export mesh: {ex.Message}");
        }
        finally
        {
            CompleteBusyWork();
        }
    }

    private static string BuildExportStatusText(int fileCount, MeshFuseMode fuseMode, string stage)
    {
        if (fileCount <= 1)
        {
            return stage switch
            {
                "writing" => "Writing STL file...",
                _ => "Exporting STL..."
            };
        }

        var modeLabel = fuseMode switch
        {
            MeshFuseMode.SolidCombine => "solid combine",
            MeshFuseMode.UnifiedShell => "unified shell",
            _ => "voxel envelope"
        };

        return stage switch
        {
            "starting" => $"Preparing {fileCount:N0} visible scans ({modeLabel})...",
            "merging" => $"Merging {fileCount:N0} visible scans ({modeLabel})...",
            "cleanup" => "Cleaning saved STL artifacts...",
            "writing" => "Writing STL file...",
            _ => $"Exporting {fileCount:N0} visible scans ({modeLabel})..."
        };
    }

    private void ReportBusyProgress(double progress, string? statusText = null)
    {
        var clamped = Math.Clamp(progress, 0, progress >= 1.0 ? 1.0 : 0.99);
        void Apply()
        {
            LoadProgress = clamped;
            if (!string.IsNullOrWhiteSpace(statusText))
            {
                StatusText = statusText;
            }
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            Apply();
            return;
        }

        dispatcher.Invoke(Apply);
    }

    private async void ExportSeparatedMeshes()
    {
        var exportableItems = _loadedFiles
            .Where(item => item.IsVisible && item.MeshSnapshot is not null)
            .Select(item => new MeshExportService.MeshExportItem(item.DisplayName, item.MeshSnapshot!))
            .ToList();

        if (exportableItems.Count == 0)
        {
            StatusText = "No visible mesh is available to export.";
            return;
        }

        var suggestedFileName = exportableItems.Count == 1
            ? Path.GetFileNameWithoutExtension(exportableItems[0].DisplayName)
            : "separate-meshes";

        var dialog = new SaveFileDialog
        {
            Filter = "STL Mesh (*.stl)|*.stl",
            DefaultExt = ".stl",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = suggestedFileName
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = "Exporting separate STLs...";

            await Task.Run(() => MeshExportService.ExportSeparateStl(dialog.FileName, exportableItems));

            SetTransientStatus($"Exported {exportableItems.Count:N0} visible mesh(es) as separate STL files.");
        }
        catch (Exception ex)
        {
            SetTransientStatus($"Failed to export mesh: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ToggleFinish()
    {
        _isMatteFinish = !_isMatteFinish;
        ApplyFinishProfile();
        OnPropertyChanged(nameof(FinishLabel));
    }

    private void ToggleSectionMode()
    {
        IsSectionMode = !IsSectionMode;
        if (IsSectionMode)
        {
            IsSculptMode = false;
        }

        if (!IsSectionMode)
        {
            ClearSectionMeasurement();
        }
    }

    private void ToggleModelScanMaterial()
    {
        _useStoneForModelScans = !_useStoneForModelScans;
        ApplyModelScanMaterialPalette();
        OnPropertyChanged(nameof(ModelScanMaterialToggleTooltip));
    }

    private void ToggleRestorationGroupExpanded()
    {
        if (!ShowRestorationGroupExpander)
        {
            return;
        }

        IsRestorationGroupExpanded = !IsRestorationGroupExpanded;
    }

    private void ToggleAbutmentGroupExpanded()
    {
        if (!ShowAbutmentGroupExpander)
        {
            return;
        }

        IsAbutmentGroupExpanded = !IsAbutmentGroupExpanded;
    }

    private void ToggleHideLayerLabels()
    {
        HideLayerLabels = !HideLayerLabels;
    }

    public void RegisterSectionMeasurementPoint(Point point)
    {
        if (MeasureStartSection is null || MeasureEndSection is not null)
        {
            MeasureStartSection = point;
            MeasureEndSection = null;
            return;
        }

        MeasureEndSection = point;
    }

    public void SetSectionMeasurementPoints(Point start, Point end)
    {
        MeasureStartSection = start;
        MeasureEndSection = end;
        OnPropertyChanged(nameof(MeasureStartSection));
        OnPropertyChanged(nameof(MeasureEndSection));
        OnPropertyChanged(nameof(HasSectionMeasurementPoint));
    }

    public void UpdateEncodeWorkflowStatus(string text) => StatusText = text;

    public void SetEncodeWorkflowBusy(bool busy)
    {
        if (busy)
        {
            BeginCancellableBusy(StatusText, resetProgress: false);
            return;
        }

        CompleteBusyWork();
    }

    public void CompleteBusyWork() => EndBusy();

    public void EnsureSectionModeForEncode()
    {
        if (!IsSectionMode)
        {
            IsSectionMode = true;
        }
    }

    private void ClearSectionMeasurement()
    {
        MeasureStartSection = null;
        MeasureEndSection = null;
    }

    private void ApplyModelScanMaterialPalette()
    {
        var scanPaletteName = _useStoneForModelScans ? "Stone" : "Model";
        var scanPalette = MaterialLibrary.Get(scanPaletteName);
        var intensity = GetSpecularIntensity();
        var shininess = GetSpecularShininess();

        foreach (var loadedFile in _loadedFiles)
        {
            if (loadedFile.IsLoadFailed || loadedFile.Category != MeshCategory.Scan)
            {
                continue;
            }

            if (PrepScanMaterialRules.IsPreopScan(loadedFile.FilePath))
            {
                loadedFile.SetMaterialPalette(MaterialLibrary.Get(PrepScanMaterialRules.TextureName), PrepScanMaterialRules.TextureName);
                loadedFile.SetSpecularIntensity(intensity);
                loadedFile.SetSpecularShininess(shininess);
                continue;
            }

            loadedFile.SetMaterialPalette(scanPalette, scanPaletteName);
            loadedFile.SetSpecularIntensity(intensity);
            loadedFile.SetSpecularShininess(shininess);
        }
    }

    private void RebuildSceneItems()
    {
        _sceneItems.Clear();
        _lightsAdded = false;
        EnsureLightsInScene();
        UpdateLightRigFromCamera(_cameraLookDirection, _cameraUpDirection);

        var meshes = _loadedFiles.Where(i => !i.IsLoadFailed).ToList();
        foreach (var item in meshes)
        {
            item.ApplyRenderState();
        }

        foreach (var item in meshes
                     .OrderBy(i => i.Model.RenderOrder)
                     .ThenBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            _sceneItems.Add(item.Model);
        }

        OnPropertyChanged(nameof(SceneItems));
    }

    private void EnsureLightsInScene()
    {
        if (_lightsAdded)
        {
            return;
        }

        _sceneItems.Add(_ambientLight);
        _sceneItems.Add(_keyLight);
        _sceneItems.Add(_fillLight);
        _sceneItems.Add(_rimLight);
        _sceneItems.Add(_backFillLight);
        _lightsAdded = true;
    }

    private void InitializeLightRig()
    {
        _keyLight.Direction = NormalizeDirection(new Vector3D(0, 0, -1));
        _fillLight.Direction = NormalizeDirection(new Vector3D(-0.26, 0.38, -1));
        _rimLight.Direction = NormalizeDirection(new Vector3D(0.33, -0.55, -1));
        _backFillLight.Direction = NormalizeDirection(new Vector3D(0, 0, 1));
    }

    public void ApplySectionPlane(Point3D planePoint, Vector3D planeNormal, bool enabled)
    {
        // 2D cross-section uses SectionGeometryService; do not clip meshes in the 3D viewport.
        if (enabled)
        {
            return;
        }

        foreach (var item in _loadedFiles.Where(i => i.IsVisible && !i.IsLoadFailed))
        {
            item.ApplySectionPlane(planePoint, planeNormal, false);
        }
    }

    private static Vector3D NormalizeDirection(Vector3D direction)
    {
        if (direction.LengthSquared < 1e-9)
        {
            direction = new Vector3D(0, 0, -1);
        }

        direction.Normalize();
        return direction;
    }

    private void ApplyLightingStrength()
    {
        _ambientLight.Color = ScaleColor(AmbientLightColor, _lightingStrength);
        _keyLight.Color = ScaleColor(KeyLightColor, _lightingStrength);
        _fillLight.Color = ScaleColor(FillLightColor, _lightingStrength);
        _rimLight.Color = ScaleColor(RimLightColor, _lightingStrength);
        _backFillLight.Color = ScaleColor(KeyLightColor, _lightingStrength * BackFillLightStrength);
    }

    private void ApplyFinishProfile()
    {
        var intensity = GetSpecularIntensity();
        var shininess = GetSpecularShininess();
        foreach (var loadedFile in _loadedFiles)
        {
            loadedFile.SetSpecularIntensity(intensity);
            loadedFile.SetSpecularShininess(shininess);
        }
    }

    private double GetSpecularIntensity() => _isMatteFinish ? MatteSpecularIntensity : SatinSpecularIntensity;

    private double GetSpecularShininess() => _isMatteFinish ? MatteSpecularShininess : SatinSpecularShininess;

    private (MaterialPalette Palette, string TextureName) ResolveMaterialPalette(
        string filePath,
        IReadOnlyDictionary<string, string> properties,
        MeshCategory category)
    {
        if (PrepScanMaterialRules.IsPreopScan(filePath))
        {
            return (MaterialLibrary.Get(PrepScanMaterialRules.TextureName), PrepScanMaterialRules.TextureName);
        }

        // Explicit per-file override from the host application (after prep-scan emax rule).
        if (TryGetHostOverride(TextureOverrides, filePath, out var overrideName) &&
            MaterialLibrary.Contains(overrideName))
        {
            return (MaterialLibrary.Get(overrideName), overrideName);
        }

        // 2. Category default: restorations should use Zirconia unless explicitly overridden.
        if (category == MeshCategory.Restoration)
        {
            return (MaterialLibrary.Get("Zirconia"), "Zirconia");
        }

        if (category == MeshCategory.Scan)
        {
            var scanPaletteName = _useStoneForModelScans ? "Stone" : "Model";
            return (MaterialLibrary.Get(scanPaletteName), scanPaletteName);
        }

        // 3. Metadata heuristics: scan-textured → Model, tooth/crown/bridge → Zirconia.
        if (LooksLikeTexturedScan(properties))
            return (MaterialLibrary.Get("Model"), "Model");

        if (LooksLikeToothFile(filePath, properties))
            return (MaterialLibrary.Get("Zirconia"), "Zirconia");

        // 4. Fallback to the current default texture.
        return (MaterialLibrary.Get(_defaultTextureName), _defaultTextureName);
    }

    private void ReapplyDefaultTexture(string previousTexture, string newTexture)
    {
        var newPalette = MaterialLibrary.Get(newTexture);
        var intensity = GetSpecularIntensity();
        var shininess = GetSpecularShininess();
        foreach (var loadedFile in _loadedFiles)
        {
            if (!TryGetHostOverride(TextureOverrides, loadedFile.FilePath, out _) &&
                string.Equals(loadedFile.AppliedTextureName, previousTexture, StringComparison.OrdinalIgnoreCase))
            {
                loadedFile.SetMaterialPalette(newPalette, newTexture);
                loadedFile.SetSpecularIntensity(intensity);
                loadedFile.SetSpecularShininess(shininess);
            }
        }
    }

    private MeshCategory ResolveMeshCategory(string filePath, IReadOnlyDictionary<string, string> properties)
    {
        if (TryGetHostOverride(CategoryOverrides, filePath, out var overrideValue) &&
            TryParseCategory(overrideValue, out var parsedCategory))
        {
            return parsedCategory;
        }

        // Without an explicit override, default to basic categories only.
        // Restoration/abutment grouping should be explicit via CategoryOverrides.
        return LooksLikeTexturedScan(properties) ? MeshCategory.Scan : MeshCategory.Model;
    }

    private ScanLayerArch ResolveScanArch(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (ArchOverrides.TryGetValue(fullPath, out var arch) && arch != ScanLayerArch.None)
        {
            return arch;
        }

        if (ArchOverrides.TryGetValue(Path.GetFileName(filePath), out arch) && arch != ScanLayerArch.None)
        {
            return arch;
        }

        return ScanLayerArchResolver.Resolve(filePath);
    }

    private static bool TryGetHostOverride(IReadOnlyDictionary<string, string> overrides, string filePath, out string value)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (overrides.TryGetValue(fullPath, out value!))
        {
            return true;
        }

        return overrides.TryGetValue(Path.GetFileName(filePath), out value!);
    }

    private static bool TryParseCategory(string? value, out MeshCategory category)
    {
        category = MeshCategory.Model;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "model" => (category = MeshCategory.Model) == MeshCategory.Model,
            "scan" => (category = MeshCategory.Scan) == MeshCategory.Scan,
            "restoration" => (category = MeshCategory.Restoration) == MeshCategory.Restoration,
            "abutment" => (category = MeshCategory.Abutment) == MeshCategory.Abutment,
            _ => false
        };
    }

    private void ApplyCategoryOpacity(MeshCategory category, double opacity)
    {
        BeginBulkVisualUpdate();
        try
        {
            foreach (var item in _loadedFiles)
            {
                if (item.Category == category && item.IsOpacityEnabled)
                {
                    item.Opacity = opacity;
                }
            }
        }
        finally
        {
            EndBulkVisualUpdate();
        }
    }

    private void ApplyCategoryVisibility(MeshCategory category, bool isVisible)
    {
        BeginBulkVisualUpdate();
        try
        {
            foreach (var item in _loadedFiles)
            {
                if (item.Category == category)
                {
                    item.IsVisible = isVisible;
                }
            }
        }
        finally
        {
            EndBulkVisualUpdate();
        }
    }

    private void BeginBulkVisualUpdate()
    {
        _bulkVisualUpdateNesting++;
    }

    private void EndBulkVisualUpdate()
    {
        if (_bulkVisualUpdateNesting <= 0)
        {
            _bulkVisualUpdateNesting = 0;
            return;
        }

        _bulkVisualUpdateNesting--;
        if (_bulkVisualUpdateNesting == 0 && _isVisualRefreshPending)
        {
            RefreshVisualState();
        }
    }

    private void RequestVisualRefresh()
    {
        if (_bulkVisualUpdateNesting > 0)
        {
            _isVisualRefreshPending = true;
            return;
        }

        RefreshVisualState();
    }

    private void RefreshVisualState()
    {
        _isVisualRefreshPending = false;

        foreach (var item in _loadedFiles.Where(i => !i.IsLoadFailed))
        {
            item.ApplyRenderState();
        }

        CancelStatusResetTimer();
        UpdateDefaultStatusText();

        _exportVisibleMeshesCommand.RaiseCanExecuteChanged();
        _exportSeparatedMeshesCommand.RaiseCanExecuteChanged();
        NotifyCategoryGroupPropertiesChanged();
    }

    internal void SetTransientStatus(string message)
    {
        CancelStatusResetTimer();
        StatusText = message;

        _statusResetTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(StatusResetDelaySeconds)
        };
        _statusResetTimer.Tick += StatusResetTimerOnTick;
        _statusResetTimer.Start();
    }

    private void StatusResetTimerOnTick(object? sender, EventArgs e)
    {
        CancelStatusResetTimer();
        UpdateDefaultStatusText();
    }

    private void CancelStatusResetTimer()
    {
        if (_statusResetTimer is null)
        {
            return;
        }

        _statusResetTimer.Tick -= StatusResetTimerOnTick;
        _statusResetTimer.Stop();
        _statusResetTimer = null;
    }

    internal void UpdateDefaultStatusText()
    {
        if (_loadedFiles.Count == 0)
        {
            StatusText = "";
            return;
        }

        var visibleFilesCount = _loadedFiles.Count(item => item.IsVisible);
        var visibleVertexCount = _loadedFiles.Where(item => item.IsVisible).Sum(item => item.VertexCount);
        var visibleTriangleCount = _loadedFiles.Where(item => item.IsVisible).Sum(item => item.TriangleCount);
        UpdateDefaultStatusText(visibleFilesCount, visibleVertexCount, visibleTriangleCount);
    }

    private void UpdateDefaultStatusText(int visibleFilesCount, int visibleVertexCount, int visibleTriangleCount)
    {
        StatusText = $"Visible files: {visibleFilesCount:N0} | Vertices: {visibleVertexCount:N0} | Triangles: {visibleTriangleCount:N0}";
    }

    private static bool LooksLikeToothFile(string filePath, IReadOnlyDictionary<string, string> properties)
    {
        foreach (var pair in properties)
        {
            if (!IsToothClassificationKey(pair.Key))
            {
                continue;
            }

            var value = pair.Value?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (ContainsToken(value, "tooth") ||
                ContainsToken(value, "teeth") ||
                ContainsToken(value, "crown") ||
                ContainsToken(value, "bridge") ||
                ContainsToken(value, "pontic"))
            {
                return true;
            }

            if (pair.Key.Contains("ToothElementType", StringComparison.OrdinalIgnoreCase))
            {
                if (ContainsToken(value, "abutment") ||
                    ContainsToken(value, "implant") ||
                    ContainsToken(value, "scanbody"))
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeTexturedScan(IReadOnlyDictionary<string, string> properties)
    {
        foreach (var pair in properties)
        {
            var key = pair.Key;
            var value = pair.Value?.Trim();

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (ContainsToken(key, "texture") || ContainsToken(value, "texture") ||
                ContainsToken(key, "scan") || ContainsToken(value, "scan") ||
                ContainsToken(value, "gingiva") || ContainsToken(value, "soft tissue"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsToothClassificationKey(string key)
        => ContainsToken(key, "ToothElementType") ||
           ContainsToken(key, "elementtype") ||
           ContainsToken(key, "restorationtype") ||
           ContainsToken(key, "objecttype") ||
           ContainsToken(key, "prosthesis");

    private static bool ContainsToken(string? text, string token)
        => !string.IsNullOrWhiteSpace(text) &&
           text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

    private static CoordinateDecodingMode ResolveCoordinateDecodingMode(string filePath)
    {
        return CoordinateDecodingMode.Auto;
    }

    private static SceneTransformKind ResolveSceneTransformKind(string filePath, MeshCategory category)
    {
        _ = category;

        // Legacy DCMViewer applied TryApplyBestCoordinateTransform to every DCM (scans, CAD, preop).
        // Gating on \Scans\ or skipping CAD caused 364/436-style misalignment vs the old viewer.
        return SceneTransformKind.Scan;
    }

    private static bool LooksLikeDesignedPartPath(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return name.Contains("Crown", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Abutment", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Bridge", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Inlay", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Onlay", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Veneer", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("UNN", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("LL", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("LR", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("UL", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("UR", StringComparison.OrdinalIgnoreCase);
    }

    private static Brush? CreateTextureBrush(byte[]? textureImageBytes, IReadOnlyDictionary<string, string> properties)
    {
        if (textureImageBytes is null || textureImageBytes.Length == 0)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(textureImageBytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            var brush = new ImageBrush(bitmap)
            {
                Stretch = Stretch.Fill,
                TileMode = TileMode.None
            };

            RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.HighQuality);
            return brush;
        }
        catch
        {
            return null;
        }
    }



    private static Color ScaleColor(Color color, double factor)
    {
        static byte ScaleComponent(byte component, double f)
        {
            var scaled = (int)Math.Round(component * f);
            return (byte)Math.Clamp(scaled, 0, 255);
        }

        return Color.FromRgb(
            ScaleComponent(color.R, factor),
            ScaleComponent(color.G, factor),
            ScaleComponent(color.B, factor));
    }

    private Rect3D GetVisibleBounds()
    {
        Rect3D? bounds = null;
        foreach (var item in _loadedFiles)
        {
            if (!item.IsVisible || item.Bounds.IsEmpty)
            {
                continue;
            }

            bounds = bounds.HasValue ? Rect3D.Union(bounds.Value, item.Bounds) : item.Bounds;
        }

        return bounds ?? Rect3D.Empty;
    }

    /// <summary>
    /// Frames the camera so the facial (buccal) side faces the viewer when +Z front is not already adequate.
    /// Upper/Lower folder scans are oriented so upper arch is on top of the view.
    /// </summary>
    public void ApplyFacialCameraToVisibleMeshes()
    {
        var bounds = GetVisibleBounds();
        if (bounds.IsEmpty)
        {
            return;
        }

        var center = new Point3D(
            bounds.X + (bounds.SizeX / 2.0),
            bounds.Y + (bounds.SizeY / 2.0),
            bounds.Z + (bounds.SizeZ / 2.0));

        var maxSize = Math.Max(bounds.SizeX, Math.Max(bounds.SizeY, bounds.SizeZ));
        if (maxSize <= 0)
        {
            maxSize = 1;
        }

        var distance = maxSize * 2.5;
        var orientationMeshes = CollectOrientationMeshes();
        var frontFacial = DentalCameraOrientationHelper.DefaultFrontFacialDirection;
        var useFrontView = orientationMeshes.Count == 0 ||
                           DentalCameraOrientationHelper.IsFrontViewBuccalAdequate(orientationMeshes, bounds, frontFacial);

        Vector3D facial;
        Vector3D up;
        if (useFrontView)
        {
            facial = frontFacial;
            up = TryGetArchUpDirection(out var archUp) ? archUp : new Vector3D(0, 1, 0);
        }
        else if (orientationMeshes.Count > 0 &&
                 DentalCameraOrientationHelper.TryEstimateFacialView(orientationMeshes, bounds, out facial, out up))
        {
            facial.Normalize();
            up.Normalize();
            if (TryGetArchUpDirection(out var archUp))
            {
                up = archUp;
            }
        }
        else
        {
            facial = frontFacial;
            up = TryGetArchUpDirection(out var archUp) ? archUp : new Vector3D(0, 1, 0);
        }

        facial.Normalize();
        up.Normalize();
        CameraPosition = center + (facial * distance);
        CameraLookDirection = center - CameraPosition;
        CameraUpDirection = DentalCameraOrientationHelper.OrthogonalizeUp(up, CameraLookDirection);
    }

    private bool TryGetArchUpDirection(out Vector3D archUp)
    {
        archUp = default;
        var upperCenter = GetArchBoundsCenter(ScanLayerArch.Upper);
        var lowerCenter = GetArchBoundsCenter(ScanLayerArch.Lower);
        if (!upperCenter.HasValue || !lowerCenter.HasValue)
        {
            return false;
        }

        archUp = upperCenter.Value - lowerCenter.Value;
        if (archUp.LengthSquared < 1e-9)
        {
            archUp = default;
            return false;
        }

        archUp.Normalize();
        return true;
    }

    private Point3D? GetArchBoundsCenter(ScanLayerArch arch)
    {
        Rect3D? combined = null;
        foreach (var item in _loadedFiles)
        {
            if (!item.IsVisible || item.IsLoadFailed || item.ScanArch != arch || item.Bounds.IsEmpty)
            {
                continue;
            }

            combined = combined.HasValue ? Rect3D.Union(combined.Value, item.Bounds) : item.Bounds;
        }

        if (!combined.HasValue || combined.Value.IsEmpty)
        {
            return null;
        }

        var bounds = combined.Value;
        return new Point3D(
            bounds.X + bounds.SizeX * 0.5,
            bounds.Y + bounds.SizeY * 0.5,
            bounds.Z + bounds.SizeZ * 0.5);
    }

    private List<MeshSnapshot> CollectOrientationMeshes()
    {
        var scanMeshes = _loadedFiles
            .Where(item => item.IsVisible &&
                           !item.IsLoadFailed &&
                           item.MeshSnapshot is not null &&
                           (item.Category == MeshCategory.Scan || item.Category == MeshCategory.Model))
            .Select(item => item.MeshSnapshot!)
            .ToList();

        if (scanMeshes.Count > 0)
        {
            return scanMeshes;
        }

        return _loadedFiles
            .Where(item => item.IsVisible && !item.IsLoadFailed && item.MeshSnapshot is not null)
            .Select(item => item.MeshSnapshot!)
            .ToList();
    }

    private void FrameCameraToBounds(Rect3D bounds)
    {
        if (_suppressAutomaticCameraFraming || bounds.IsEmpty)
        {
            return;
        }

        ApplyFacialCameraToVisibleMeshes();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
