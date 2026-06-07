using DCMViewer.Infrastructure;
using DCMViewer.Services;
using System.IO;
using System.Windows.Media.Media3D;

namespace DCMViewer.ViewModels;

public partial class MainViewModel
{
    private bool _isContactHeatmapVisible;
    private bool _isThicknessBreachOverlayVisible;
    private string _safetyStatusText = "Enforce minimum thickness and review contacts on the closed crown.";
    private string _validationStatusText = "Run validation before export to confirm watertight shell, wall thickness, and seat path.";

    public bool IsContactHeatmapVisible
    {
        get => _isContactHeatmapVisible;
        private set
        {
            if (_isContactHeatmapVisible == value)
            {
                return;
            }

            _isContactHeatmapVisible = value;
            OnPropertyChanged();
            ToggleContactHeatmapCommand.RaiseCanExecuteChanged();
        }
    }

    public string SafetyStatusText
    {
        get => _safetyStatusText;
        private set
        {
            if (string.Equals(_safetyStatusText, value, StringComparison.Ordinal))
            {
                return;
            }

            _safetyStatusText = value;
            OnPropertyChanged();
        }
    }

    public string ValidationStatusText
    {
        get => _validationStatusText;
        private set
        {
            if (string.Equals(_validationStatusText, value, StringComparison.Ordinal))
            {
                return;
            }

            _validationStatusText = value;
            OnPropertyChanged();
        }
    }

    public bool IsThicknessBreachOverlayVisible
    {
        get => _isThicknessBreachOverlayVisible;
        private set
        {
            if (_isThicknessBreachOverlayVisible == value)
            {
                return;
            }

            _isThicknessBreachOverlayVisible = value;
            OnPropertyChanged();
            ToggleThicknessBreachOverlayCommand.RaiseCanExecuteChanged();
        }
    }

    public RelayCommand EnforceMinimumThicknessCommand { get; private set; } = null!;
    public RelayCommand ToggleContactHeatmapCommand { get; private set; } = null!;
    public RelayCommand ToggleThicknessBreachOverlayCommand { get; private set; } = null!;
    public RelayCommand RunCrownValidationCommand { get; private set; } = null!;

    private void InitDesignSafetyCommands()
    {
        EnforceMinimumThicknessCommand = new RelayCommand(
            () => _ = EnforceMinimumThicknessAsync(),
            () => !IsBusy && IsDesignHostMode && HasClosedCrown);
        ToggleContactHeatmapCommand = new RelayCommand(
            () => _ = ToggleContactHeatmapAsync(),
            () => !IsBusy && IsDesignHostMode && HasClosedCrown);
        ToggleThicknessBreachOverlayCommand = new RelayCommand(
            () => _ = ToggleThicknessBreachOverlayAsync(),
            () => !IsBusy && IsDesignHostMode && HasClosedCrown);
        RunCrownValidationCommand = new RelayCommand(
            () => _ = RunCrownValidationAsync(showStatus: true),
            () => !IsBusy && IsDesignHostMode && HasClosedCrown && IsMarginClosed);
    }

    internal bool IsDesignCrownItem(LoadedMeshItemViewModel item)
    {
        if (string.IsNullOrWhiteSpace(_crownPath))
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(item.FilePath),
            Path.GetFullPath(_crownPath),
            StringComparison.OrdinalIgnoreCase);
    }

    private LoadedMeshItemViewModel? GetClosedCrownItem() =>
        string.IsNullOrWhiteSpace(_crownPath) ? null : FindLoadedFile(_crownPath);

    private MeshSnapshot? PickPrimaryPrepMesh()
    {
        if (!IsMarginClosed || MarginPointCount < 3)
        {
            return null;
        }

        var marginLoop = BuildMarginDesignLoop();
        return StatsDesignPrepMeshResolver.PickPrimary(_loadedFiles, marginLoop, DesignManifest.MarginHostFilePath);
    }

    private IReadOnlyList<MeshSnapshot> CollectContactReferenceMeshes(MeshSnapshot primaryPrep)
    {
        return _loadedFiles
            .Where(item =>
                StatsDesignPrepMeshResolver.CanHostMargin(item) &&
                !ReferenceEquals(item.MeshSnapshot, primaryPrep))
            .Select(item => item.MeshSnapshot!)
            .ToList();
    }

    private async Task EnforceMinimumThicknessAsync()
    {
        var crownItem = GetClosedCrownItem();
        if (crownItem?.MeshSnapshot is null)
        {
            SetTransientStatus("Load the closed crown before enforcing thickness.");
            return;
        }

        var prep = PickPrimaryPrepMesh();
        if (prep is null)
        {
            SetTransientStatus("No arch scan available for thickness enforcement. Show the scan that contains the preparation.");
            return;
        }

        var minThickness = MinimumThicknessMm;
        var crownSnapshot = crownItem.MeshSnapshot;
        _ = BeginCancellableBusy("Enforcing minimum wall thickness...");
        try
        {
            var result = await Task.Run(() =>
                StatsDesignThicknessEnforcer.Enforce(crownSnapshot, prep, minThickness));

            crownItem.ReplaceMeshGeometry(result.Mesh);
            if (!string.IsNullOrWhiteSpace(_crownPath))
            {
                await Task.Run(() => MeshExportService.Export(_crownPath, new[] { result.Mesh }));
            }

            SafetyStatusText = result.AdjustedVertexCount > 0
                ? $"Thickness: pushed {result.AdjustedVertexCount} vertices to ≥ {minThickness:F2} mm (was {result.MinimumObservedThicknessMm:F2} mm min)."
                : $"Thickness OK — minimum observed {result.MinimumObservedThicknessMm:F2} mm (target ≥ {minThickness:F2} mm).";
            SetTransientStatus(SafetyStatusText);

            if (IsThicknessBreachOverlayVisible)
            {
                await RefreshThicknessBreachOverlayAsync(showStatus: false);
            }
            else if (IsContactHeatmapVisible)
            {
                await RefreshContactHeatmapAsync(showStatus: false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            SetTransientStatus($"Thickness enforcement failed: {ex.Message}");
        }
        finally
        {
            CompleteBusyWork();
        }
    }

    private async Task ToggleContactHeatmapAsync()
    {
        if (IsContactHeatmapVisible)
        {
            GetClosedCrownItem()?.ClearVertexColorOverlay();
            IsContactHeatmapVisible = false;
            SafetyStatusText = "Contact heatmap hidden — Zirconia shading restored.";
            SetTransientStatus(SafetyStatusText);
            return;
        }

        if (IsThicknessBreachOverlayVisible)
        {
            GetClosedCrownItem()?.ClearVertexColorOverlay();
            IsThicknessBreachOverlayVisible = false;
        }

        await RefreshContactHeatmapAsync(showStatus: true);
        IsContactHeatmapVisible = true;
    }

    private async Task ToggleThicknessBreachOverlayAsync()
    {
        if (IsThicknessBreachOverlayVisible)
        {
            GetClosedCrownItem()?.ClearVertexColorOverlay();
            IsThicknessBreachOverlayVisible = false;
            SafetyStatusText = "Thickness breach overlay hidden — Zirconia shading restored.";
            SetTransientStatus(SafetyStatusText);
            return;
        }

        if (IsContactHeatmapVisible)
        {
            GetClosedCrownItem()?.ClearVertexColorOverlay();
            IsContactHeatmapVisible = false;
        }

        await RefreshThicknessBreachOverlayAsync(showStatus: true);
        IsThicknessBreachOverlayVisible = true;
    }

    internal async Task RefreshThicknessBreachOverlayAsync(bool showStatus = false)
    {
        var crownItem = GetClosedCrownItem();
        if (crownItem?.MeshSnapshot is null)
        {
            return;
        }

        var prep = PickPrimaryPrepMesh();
        if (prep is null)
        {
            if (showStatus)
            {
                SetTransientStatus("No arch scan — cannot evaluate wall thickness.");
            }

            return;
        }

        var crownSnapshot = crownItem.MeshSnapshot;
        var minThickness = MinimumThicknessMm;
        var overlay = await Task.Run(() =>
            StatsDesignThicknessBreachVisualizer.BuildBreachOverlay(crownSnapshot, prep, minThickness));
        crownItem.ApplyThicknessBreachOverlay(overlay.VertexColors);
        SafetyStatusText = StatsDesignThicknessBreachVisualizer.Summarize(overlay, minThickness);
        if (showStatus)
        {
            SetTransientStatus(SafetyStatusText);
        }
    }

    internal async Task<StatsDesignCrownValidationEngine.ValidationReport?> RunCrownValidationAsync(bool showStatus = true)
    {
        var crownItem = GetClosedCrownItem();
        if (crownItem?.MeshSnapshot is null)
        {
            return null;
        }

        var prep = PickPrimaryPrepMesh();
        if (prep is null)
        {
            ValidationStatusText = "Validation skipped — no visible arch scan for the preparation.";
            if (showStatus)
            {
                SetTransientStatus(ValidationStatusText);
            }

            return null;
        }

        var marginLoop = BuildMarginDesignLoop();
        var axis = ResolveDesignInsertionAxis();
        var crownSnapshot = crownItem.MeshSnapshot;
        var report = await Task.Run(() =>
            StatsDesignCrownValidationEngine.Inspect(
                crownSnapshot,
                prep,
                marginLoop,
                axis,
                MinimumThicknessMm));

        ValidationStatusText = report.Summary;
        if (showStatus)
        {
            SetTransientStatus(ValidationStatusText);
        }

        if (IsThicknessBreachOverlayVisible)
        {
            await RefreshThicknessBreachOverlayAsync(showStatus: false);
        }

        return report;
    }

    private async Task RefreshContactHeatmapAsync(bool showStatus)
    {
        var crownItem = GetClosedCrownItem();
        if (crownItem?.MeshSnapshot is null)
        {
            return;
        }

        var prep = PickPrimaryPrepMesh();
        if (prep is null)
        {
            SetTransientStatus("No arch scan — cannot classify contacts.");
            return;
        }

        var contacts = CollectContactReferenceMeshes(prep);
        if (contacts.Count == 0)
        {
            SetTransientStatus("Load additional arch scans to compare proximal/occlusal contacts.");
            return;
        }

        var crownSnapshot = crownItem.MeshSnapshot;
        var colors = await Task.Run(() => StatsDesignContactHeatmap.BuildVertexColors(crownSnapshot, contacts));
        crownItem.ApplyContactHeatmap(colors);
        SafetyStatusText = StatsDesignContactHeatmap.Summarize(crownSnapshot, contacts);
        if (showStatus)
        {
            SetTransientStatus(SafetyStatusText);
        }
    }

    internal void ClearDesignSafetyVisualization()
    {
        GetClosedCrownItem()?.ClearVertexColorOverlay();
        IsContactHeatmapVisible = false;
        IsThicknessBreachOverlayVisible = false;
    }

    private void RaiseDesignSafetyCommandStates()
    {
        EnforceMinimumThicknessCommand.RaiseCanExecuteChanged();
        ToggleContactHeatmapCommand.RaiseCanExecuteChanged();
        ToggleThicknessBreachOverlayCommand.RaiseCanExecuteChanged();
        RunCrownValidationCommand.RaiseCanExecuteChanged();
        RaiseDesignAdvancedCommandStates();
    }
}
