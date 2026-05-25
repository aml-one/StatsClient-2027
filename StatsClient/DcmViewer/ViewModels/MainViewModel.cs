using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using DCMViewer.Infrastructure;
using DCMViewer.Services;
using Microsoft.Win32;

namespace DCMViewer.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private const double SatinSpecularIntensity = 1.0;
    private const double MatteSpecularIntensity = 0.35;

    private static readonly Color AmbientLightColor = Color.FromRgb(26, 26, 26);
    private static readonly Color KeyLightColor = Color.FromRgb(78, 78, 78);
    private static readonly Color FillLightColor = Color.FromRgb(58, 58, 58);
    private static readonly Color RimLightColor = Color.FromRgb(34, 34, 34);

    private readonly DcmParser _parser;
    private readonly RelayCommand _openFileCommand;
    private readonly RelayCommand _exportVisibleMeshesCommand;
    private readonly RelayCommand _exportSeparatedMeshesCommand;
    private readonly RelayCommand _toggleFinishCommand;
    private readonly RelayCommand _toggleSectionModeCommand;
    private readonly RelayCommand _clearSectionMeasurementCommand;
    private readonly RelayCommand _toggleModelScanMaterialCommand;
    private readonly ObservableCollection<LoadedMeshItemViewModel> _loadedFiles = new();
    private readonly AmbientLight _ambientLight = new();
    private readonly DirectionalLight _keyLight = new();
    private readonly DirectionalLight _fillLight = new();
    private readonly DirectionalLight _rimLight = new();
    private readonly Model3DGroup _sharedLightRig;

    private Model3DGroup _modelGroup = new();
    private Point3D _cameraPosition = new(0, 0, 500);
    private Vector3D _cameraLookDirection = new(0, 0, -500);
    private Vector3D _cameraUpDirection = new(0, 1, 0);
    private string _statusText = "Select a .dcm or .stl file to load a mesh.";
    private bool _isBusy;
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
    private bool _isExternalFileDropEnabled = true;
    private bool _useStoneForModelScans;
    private int _bulkVisualUpdateNesting;
    private bool _isVisualRefreshPending;

    public MainViewModel(DcmParser parser)
    {
        _parser = parser;
        _openFileCommand = new RelayCommand(OpenFile, () => !IsBusy);
        _exportVisibleMeshesCommand = new RelayCommand(ExportVisibleMeshes, CanExportVisibleMeshes);
        _exportSeparatedMeshesCommand = new RelayCommand(ExportSeparatedMeshes, CanExportVisibleMeshes);
        _toggleFinishCommand = new RelayCommand(ToggleFinish);
        _toggleSectionModeCommand = new RelayCommand(ToggleSectionMode);
        _clearSectionMeasurementCommand = new RelayCommand(ClearSectionMeasurement);
        _toggleModelScanMaterialCommand = new RelayCommand(ToggleModelScanMaterial);
        _loadedFiles.CollectionChanged += LoadedFilesOnCollectionChanged;

        _sharedLightRig = CreateSharedLightRig(_ambientLight, _keyLight, _fillLight, _rimLight);
        ApplyLightingStrength();
        UpdateLightRigFromCamera(_cameraLookDirection, _cameraUpDirection);
        RebuildModelGroup();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand OpenFileCommand => _openFileCommand;

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

    public bool HasRestorationGroup => _loadedFiles.Any(item => !item.IsLoadFailed && item.Category == MeshCategory.Restoration);

    public bool HasAbutmentGroup => _loadedFiles.Any(item => !item.IsLoadFailed && item.Category == MeshCategory.Abutment);

    public int RestorationGroupCount => _loadedFiles.Count(item => !item.IsLoadFailed && item.Category == MeshCategory.Restoration);

    public int AbutmentGroupCount => _loadedFiles.Count(item => !item.IsLoadFailed && item.Category == MeshCategory.Abutment);

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

    public Model3DGroup ModelGroup
    {
        get => _modelGroup;
        private set
        {
            if (ReferenceEquals(_modelGroup, value))
            {
                return;
            }

            _modelGroup = value;
            OnPropertyChanged();
        }
    }

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
    }

    public async Task LoadFilesAsync(IEnumerable<string> filePaths, bool clearExisting)
    {
        var normalizedPaths = filePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedPaths.Count == 0)
        {
            StatusText = "No files selected.";
            return;
        }

        try
        {
            IsBusy = true;
            LoadProgress = 0;
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

                try
                {
                    var decodeMode = ResolveCoordinateDecodingMode(filePath);
                    var parsed = await Task.Run(() => _parser.ParseFile(filePath, decodeMode));

                    var category = ResolveMeshCategory(filePath, parsed.Properties);
                    var (palette, textureName) = ResolveMaterialPalette(filePath, parsed.Properties, category);

                    var frontBrush = new SolidColorBrush(palette.FrontDiffuse);
                    var backBrush = new SolidColorBrush(palette.BackDiffuse);
                    var frontSpecularBrush = new SolidColorBrush(palette.FrontSpecular);

                    var frontMaterial = new MaterialGroup();
                    frontMaterial.Children.Add(new DiffuseMaterial(frontBrush));
                    frontMaterial.Children.Add(new SpecularMaterial(frontSpecularBrush, 7));

                    var backMaterial = new MaterialGroup();
                    backMaterial.Children.Add(new DiffuseMaterial(backBrush));

                    var geometryModel = new GeometryModel3D
                    {
                        Geometry = parsed.Mesh,
                        Material = frontMaterial,
                        BackMaterial = backMaterial
                    };

                    var loadedFile = new LoadedMeshItemViewModel(
                        filePath,
                        geometryModel,
                        new[] { frontBrush, backBrush, frontSpecularBrush },
                        new[] { frontSpecularBrush },
                        parsed.Bounds,
                        parsed.VertexCount,
                        parsed.TriangleCount,
                        parsed.IsEncrypted,
                        parsed.Properties.ContainsKey("PackageLockList"),
                        category,
                        appliedTextureName: textureName);

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
                    loadedFile.PropertyChanged += LoadedFileOnPropertyChanged;
                    _loadedFiles.Add(loadedFile);
                    loadedNow++;
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
                        isPackageLocked: isPackageLocked,
                        isEncrypted: isPackageLocked);
                    _loadedFiles.Add(failedFile);
                    failedNow++;
                }

                LoadProgress = (double)processedNow / totalFiles;
            }

            LoadProgress = 1;
            RebuildModelGroup();
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
        catch (Exception ex)
        {
            StatusText = $"Failed to load file: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
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

        RebuildModelGroup();
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
        _exportVisibleMeshesCommand.RaiseCanExecuteChanged();
        _exportSeparatedMeshesCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(HasRestorationGroup));
        OnPropertyChanged(nameof(HasAbutmentGroup));
        OnPropertyChanged(nameof(RestorationGroupCount));
        OnPropertyChanged(nameof(AbutmentGroupCount));
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
        return !IsBusy && _loadedFiles.Any(item => item.IsVisible && item.MeshGeometry is not null);
    }

    private async void ExportVisibleMeshes()
    {
        var exportableItems = _loadedFiles
            .Where(item => item.IsVisible && item.MeshGeometry is not null)
            .Select(item => new
            {
                item.DisplayName,
                Mesh = item.MeshGeometry!
            })
            .ToList();

        if (exportableItems.Count == 0)
        {
            StatusText = "No visible mesh is available to export.";
            return;
        }

        var suggestedFileName = exportableItems.Count == 1
            ? Path.GetFileNameWithoutExtension(exportableItems[0].DisplayName)
            : "merged-meshes";

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
            StatusText = "Exporting merged STL...";

            var snapshots = exportableItems
                .Select(item => MeshExportService.CreateSnapshot(item.Mesh))
                .ToArray();

            await Task.Run(() => MeshExportService.Export(dialog.FileName, snapshots));

            StatusText = $"Exported {snapshots.Length:N0} visible mesh(es) as one STL to {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to export mesh: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void ExportSeparatedMeshes()
    {
        var exportableItems = _loadedFiles
            .Where(item => item.IsVisible && item.MeshGeometry is not null)
            .Select(item => new MeshExportService.MeshExportItem(item.DisplayName, MeshExportService.CreateSnapshot(item.MeshGeometry!)))
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

            StatusText = $"Exported {exportableItems.Count:N0} visible mesh(es) as separate STL files.";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to export mesh: {ex.Message}";
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

        foreach (var loadedFile in _loadedFiles)
        {
            if (loadedFile.IsLoadFailed || loadedFile.Category != MeshCategory.Scan)
            {
                continue;
            }

            if (ShouldUseEmaxForModelScanFile(loadedFile.FilePath))
            {
                loadedFile.SetMaterialPalette(MaterialLibrary.Get("Emax"), "Emax");
                loadedFile.SetSpecularIntensity(intensity);
                continue;
            }

            loadedFile.SetMaterialPalette(scanPalette, scanPaletteName);
            loadedFile.SetSpecularIntensity(intensity);
        }
    }

    private void RebuildModelGroup()
    {
        var group = new Model3DGroup();
        group.Children.Add(_sharedLightRig);

        foreach (var item in _loadedFiles.Where(i => i.IsVisible && i.Opacity >= 0.999))
        {
            group.Children.Add(item.Model);
        }

        // Draw transparent meshes last so opaque geometry remains visible underneath.
        foreach (var item in _loadedFiles.Where(i => i.IsVisible && i.Opacity < 0.999))
        {
            group.Children.Add(item.Model);
        }

        ModelGroup = group;
    }

    private static Model3DGroup CreateSharedLightRig(
        AmbientLight ambientLight,
        DirectionalLight keyLight,
        DirectionalLight fillLight,
        DirectionalLight rimLight)
    {
        var group = new Model3DGroup();

        keyLight.Direction = NormalizeDirection(new Vector3D(0, 0, -1));
        fillLight.Direction = NormalizeDirection(new Vector3D(-0.26, 0.38, -1));
        rimLight.Direction = NormalizeDirection(new Vector3D(0.33, -0.55, -1));

        group.Children.Add(ambientLight);
        group.Children.Add(keyLight);
        group.Children.Add(fillLight);
        group.Children.Add(rimLight);

        return group;
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
    }

    private void ApplyFinishProfile()
    {
        var intensity = GetSpecularIntensity();
        foreach (var loadedFile in _loadedFiles)
        {
            loadedFile.SetSpecularIntensity(intensity);
        }
    }

    private double GetSpecularIntensity() => _isMatteFinish ? MatteSpecularIntensity : SatinSpecularIntensity;

    private (MaterialPalette Palette, string TextureName) ResolveMaterialPalette(
        string filePath,
        IReadOnlyDictionary<string, string> properties,
        MeshCategory category)
    {
        // 1. Explicit per-file override from the host application.
        if (TryGetHostOverride(TextureOverrides, filePath, out var overrideName) &&
            MaterialLibrary.Contains(overrideName))
        {
            return (MaterialLibrary.Get(overrideName), overrideName);
        }

        if (ShouldUseEmaxForModelScanFile(filePath))
        {
            return (MaterialLibrary.Get("Emax"), "Emax");
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
        foreach (var loadedFile in _loadedFiles)
        {
            if (!TryGetHostOverride(TextureOverrides, loadedFile.FilePath, out _) &&
                string.Equals(loadedFile.AppliedTextureName, previousTexture, StringComparison.OrdinalIgnoreCase))
            {
                loadedFile.SetMaterialPalette(newPalette, newTexture);
                loadedFile.SetSpecularIntensity(intensity);
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
                if (!item.IsLoadFailed && item.Category == category)
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
                if (!item.IsLoadFailed && item.Category == category)
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
        RebuildModelGroup();

        var visibleFilesCount = _loadedFiles.Count(item => item.IsVisible);
        var visibleVertexCount = _loadedFiles.Where(item => item.IsVisible).Sum(item => item.VertexCount);
        var visibleTriangleCount = _loadedFiles.Where(item => item.IsVisible).Sum(item => item.TriangleCount);
        StatusText = $"Visible files: {visibleFilesCount:N0} | Vertices: {visibleVertexCount:N0} | Triangles: {visibleTriangleCount:N0}";

        _exportVisibleMeshesCommand.RaiseCanExecuteChanged();
        _exportSeparatedMeshesCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(HasRestorationGroup));
        OnPropertyChanged(nameof(HasAbutmentGroup));
        OnPropertyChanged(nameof(RestorationGroupCount));
        OnPropertyChanged(nameof(AbutmentGroupCount));
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

    private static bool ShouldUseEmaxForModelScanFile(string filePath)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        return fileNameWithoutExtension.Contains("PrePreparationScan", StringComparison.OrdinalIgnoreCase)
            || fileNameWithoutExtension.Contains("GenericDoublePrepScan", StringComparison.OrdinalIgnoreCase);
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

    private void FrameCameraToBounds(Rect3D bounds)
    {
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
        CameraPosition = new Point3D(center.X, center.Y, center.Z + distance);
        CameraLookDirection = new Vector3D(center.X - CameraPosition.X, center.Y - CameraPosition.Y, center.Z - CameraPosition.Z);
        CameraUpDirection = new Vector3D(0, 1, 0);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
