using DCMViewer.Infrastructure;
using DCMViewer.Services;
using System.IO;
using System.Windows.Media.Media3D;

namespace DCMViewer.ViewModels;

public partial class MainViewModel
{
    private ViewerHostMode _hostMode;
    private string? _designOrderFolderPath;
    private string _designWorkflowStep = "Margin";
    private StatsDesignManifest _designManifest = new();

    public ViewerHostMode HostMode
    {
        get => _hostMode;
        set
        {
            if (_hostMode == value)
            {
                return;
            }

            _hostMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDesignHostMode));
            OnPropertyChanged(nameof(IsOrderInfoHostMode));
            OnPropertyChanged(nameof(SculptDesignablesOnly));
        }
    }

    public bool IsDesignHostMode => HostMode == ViewerHostMode.Design;

    public bool IsOrderInfoHostMode => HostMode == ViewerHostMode.OrderInfo;

    public bool SculptDesignablesOnly => IsDesignHostMode || IsDesignEditMode;

    public string DesignWorkflowStep
    {
        get => _designWorkflowStep;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "Margin" : value.Trim();
            if (string.Equals(_designWorkflowStep, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _designWorkflowStep = normalized;
            OnPropertyChanged();
            ApplyDesignWorkflowStepMode();
        }
    }

    public StatsDesignManifest DesignManifest
    {
        get => _designManifest;
        private set
        {
            _designManifest = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CementGapMm));
            OnPropertyChanged(nameof(ExtraCementGapMm));
            OnPropertyChanged(nameof(SmoothDistanceMm));
            OnPropertyChanged(nameof(MinimumThicknessMm));
            OnPropertyChanged(nameof(OffsetFromMarginMm));
            OnPropertyChanged(nameof(DesignName));
        }
    }

    public string DesignName
    {
        get => DesignManifest.DesignName;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "design" : value.Trim();
            if (string.Equals(DesignManifest.DesignName, normalized, StringComparison.Ordinal))
            {
                return;
            }

            DesignManifest.DesignName = normalized;
            OnPropertyChanged();
        }
    }

    public double CementGapMm
    {
        get => DesignManifest.CementGapMm;
        set
        {
            var clamped = Math.Clamp(value, 0, 0.5);
            if (Math.Abs(DesignManifest.CementGapMm - clamped) < 1e-6)
            {
                return;
            }

            DesignManifest.CementGapMm = clamped;
            OnPropertyChanged();
        }
    }

    public double ExtraCementGapMm
    {
        get => DesignManifest.ExtraCementGapMm;
        set
        {
            var clamped = Math.Clamp(value, 0, 2.0);
            if (Math.Abs(DesignManifest.ExtraCementGapMm - clamped) < 1e-6)
            {
                return;
            }

            DesignManifest.ExtraCementGapMm = clamped;
            OnPropertyChanged();
        }
    }

    public double SmoothDistanceMm
    {
        get => DesignManifest.SmoothDistanceMm;
        set
        {
            var clamped = Math.Clamp(value, 0, 1.0);
            if (Math.Abs(DesignManifest.SmoothDistanceMm - clamped) < 1e-6)
            {
                return;
            }

            DesignManifest.SmoothDistanceMm = clamped;
            OnPropertyChanged();
        }
    }

    public double MinimumThicknessMm
    {
        get => DesignManifest.MinimumThicknessMm;
        set
        {
            var clamped = Math.Clamp(value, 0.2, 3.0);
            if (Math.Abs(DesignManifest.MinimumThicknessMm - clamped) < 1e-6)
            {
                return;
            }

            DesignManifest.MinimumThicknessMm = clamped;
            OnPropertyChanged();
        }
    }

    public double OffsetFromMarginMm
    {
        get => DesignManifest.OffsetFromMarginMm;
        set
        {
            var clamped = Math.Clamp(value, 0, 3.0);
            if (Math.Abs(DesignManifest.OffsetFromMarginMm - clamped) < 1e-6)
            {
                return;
            }

            DesignManifest.OffsetFromMarginMm = clamped;
            OnPropertyChanged();
        }
    }

    public double OcclusalClearanceMm
    {
        get => DesignManifest.OcclusalClearanceMm;
        set
        {
            var clamped = Math.Clamp(value, 0, 0.5);
            if (Math.Abs(DesignManifest.OcclusalClearanceMm - clamped) < 1e-6)
            {
                return;
            }

            DesignManifest.OcclusalClearanceMm = clamped;
            OnPropertyChanged();
        }
    }

    public double EmergenceTransitionMm
    {
        get => DesignManifest.EmergenceTransitionMm;
        set
        {
            var clamped = Math.Clamp(value, 0.4, 4.0);
            if (Math.Abs(DesignManifest.EmergenceTransitionMm - clamped) < 1e-6)
            {
                return;
            }

            DesignManifest.EmergenceTransitionMm = clamped;
            OnPropertyChanged();
        }
    }

    public RelayCommand ExportDesignStlCommand { get; private set; } = null!;
    public RelayCommand<string> SetDesignWorkflowStepCommand { get; private set; } = null!;

    private void InitDesignCommands()
    {
        InitDesignNavigationCommands();
        InitDesignShapeCommands();
        InitDesignAnalysisCommands();
        InitDesignSafetyCommands();
        InitDesignAdvancedCommands();
        ExportDesignStlCommand = new RelayCommand(
            () => _ = ExportDesignStlAsync(),
            () => !IsBusy && IsDesignHostMode && HasDesignMeshes);
        SetDesignWorkflowStepCommand = new RelayCommand<string>(step =>
        {
            if (!string.IsNullOrWhiteSpace(step))
            {
                DesignWorkflowStep = step;
            }
        });
    }

    public bool HasDesignMeshes => HasClosedCrown;

    internal void ConfigureDesignHost(string orderFolderPath, string orderId)
    {
        _designOrderFolderPath = Path.GetFullPath(orderFolderPath);
        HostMode = ViewerHostMode.Design;
        DesignManifest = StatsDesignManifestStore.LoadOrCreate(_designOrderFolderPath, orderId);
        ApplyDesignUiDefaults();
        if (string.IsNullOrWhiteSpace(DesignManifest.DesignName) ||
            string.Equals(DesignManifest.DesignName, "design", StringComparison.OrdinalIgnoreCase))
        {
            DesignManifest.DesignName = orderId;
            OnPropertyChanged(nameof(DesignName));
        }
        SetSculptOrderFolder(StatsDesignPaths.GetSculptTreeRoot(_designOrderFolderPath));
        LoadMarginFromManifest();
        RefreshLibraryToothCatalog();
        RestoreDesignArtifactPaths();
        RefreshAvailableDesignFiles(_designOrderFolderPath);
        _loadedDesignPaths.Clear();
        foreach (var path in DesignManifest.LoadedDesignFilePaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                _loadedDesignPaths.Add(Path.GetFullPath(path));
            }
        }

        DesignWorkflowStep = "Margin";
        ApplyDesignWorkflowStepMode();
        IsSectionMode = false;
        ClearSectionMeasurement();
        PersistDesignManifest();
    }

    internal void ConfigureOrderInfoHost()
    {
        if (HostMode == ViewerHostMode.Design)
        {
            return;
        }

        HostMode = ViewerHostMode.OrderInfo;
    }

    internal bool CanSculptMesh(LoadedMeshItemViewModel item)
    {
        if (item.IsLoadFailed)
        {
            return false;
        }

        if (IsDesignEditMode)
        {
            return IsDesignEditableMesh(item);
        }

        if (!SculptDesignablesOnly)
        {
            return true;
        }

        if (StatsDesignShellService.IsInnerShellPath(item.FilePath))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(_libraryToothPath) &&
            string.Equals(Path.GetFullPath(item.FilePath), Path.GetFullPath(_libraryToothPath), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (HasClosedCrown)
        {
            return StatsDesignShellService.IsCrownPath(item.FilePath) ||
                   (item.Category is MeshCategory.Restoration &&
                    !StatsDesignShellService.IsInnerShellPath(item.FilePath));
        }

        return false;
    }

    private void PersistDesignManifest()
    {
        if (string.IsNullOrWhiteSpace(_designOrderFolderPath) || !Directory.Exists(_designOrderFolderPath))
        {
            return;
        }

        StatsDesignManifestStore.Save(_designOrderFolderPath, DesignManifest);
    }

    internal void ApplyDesignUiDefaults()
    {
        if (StatsClient.MVVM.Core.StatsDesignDefaults.ApplyToExistingManifestIfNeeded(DesignManifest))
        {
            PersistDesignManifest();
        }

        NotifyDesignManifestNumericProperties();
    }

    private void NotifyDesignManifestNumericProperties()
    {
        OnPropertyChanged(nameof(CementGapMm));
        OnPropertyChanged(nameof(ExtraCementGapMm));
        OnPropertyChanged(nameof(SmoothDistanceMm));
        OnPropertyChanged(nameof(MinimumThicknessMm));
        OnPropertyChanged(nameof(OffsetFromMarginMm));
        OnPropertyChanged(nameof(LibraryOffsetXmm));
        OnPropertyChanged(nameof(LibraryOffsetYmm));
        OnPropertyChanged(nameof(LibraryOffsetZmm));
        OnPropertyChanged(nameof(LibraryScaleX));
        OnPropertyChanged(nameof(LibraryScaleY));
        OnPropertyChanged(nameof(LibraryScaleZ));
        OnPropertyChanged(nameof(LibraryUniformScale));
    }

    private async Task ExportDesignStlAsync()
    {
        if (!IsDesignHostMode)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_designOrderFolderPath))
        {
            SetTransientStatus("Cannot resolve order folder for export.");
            return;
        }

        var orderFolder = _designOrderFolderPath;
        if (!HasClosedCrown || string.IsNullOrWhiteSpace(_crownPath))
        {
            SetTransientStatus("Connect to margin to create the closed crown before export.");
            return;
        }

        var crownItem = FindLoadedFile(_crownPath);
        if (crownItem?.MeshSnapshot is null)
        {
            SetTransientStatus("Load the closed crown mesh before export.");
            return;
        }

        _ = BeginCancellableBusy("Validating crown...");
        try
        {
            var report = await RunCrownValidationAsync(showStatus: false);
            if (report is not null && !report.PassedAllChecks)
            {
                ValidationStatusText = report.Summary;
                SetTransientStatus($"Export blocked — {report.Summary}");
                return;
            }

            var designMeshes = new[] { crownItem.MeshSnapshot };
            var outputPath = await Task.Run(() =>
                StatsDesignExportService.ExportDesignStl(orderFolder, DesignManifest, designMeshes));
            PersistDesignManifest();
            DesignWorkflowStep = "Export";
            SetTransientStatus($"Exported {Path.GetFileName(outputPath)}");
        }
        catch (Exception ex)
        {
            SetTransientStatus($"Export failed: {ex.Message}");
        }
        finally
        {
            CompleteBusyWork();
        }
    }

}
