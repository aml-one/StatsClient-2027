using DCMViewer.Infrastructure;
using DCMViewer.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace DCMViewer.ViewModels;

public partial class MainViewModel
{
    private readonly List<Point3D> _marginPoints = [];
    private readonly List<Point3D> _marginRedrawStroke = [];
    private List<Point3D> _marginEditBaseLoop = [];
    private int _marginEditStartIndex;
    private int _marginEditEndIndex;
    private bool _marginEditBaseIsClosed;
    private bool _isMarginMode;
    private bool _isMarginClosed;
    private bool _isMarginRedrawStrokeActive;
    private int _marginGeometryRevision;
    private DispatcherTimer? _marginManifestPersistTimer;
    private DateTime _marginInteractiveUntilUtc;
    private readonly HashSet<string> _loadedDesignPaths = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Raised when margin polyline geometry changes (host should refresh the line visual).</summary>
    internal event Action? MarginVisualRefreshRequested;

    public bool IsMarginMode
    {
        get => _isMarginMode;
        private set
        {
            if (_isMarginMode == value)
            {
                return;
            }

            _isMarginMode = value;
            OnPropertyChanged();
        }
    }

    public bool IsMarginClosed
    {
        get => _isMarginClosed;
        private set
        {
            if (_isMarginClosed == value)
            {
                return;
            }

            _isMarginClosed = value;
            DesignManifest.IsMarginClosed = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MarginStatusText));
        }
    }

    public int MarginPointCount => _marginPoints.Count;

    public string MarginStatusText =>
        MarginPointCount == 0
            ? "Click the arch scan that contains the preparation to place margin points."
            : IsMarginClosed
                ? $"{MarginPointCount} points — loop closed. Drag on the scan to reshape the margin."
                : $"{MarginPointCount} point(s) — add more or close loop.";

    public int MarginGeometryRevision => _marginGeometryRevision;

    internal bool IsMarginRedrawStrokeActive => _isMarginRedrawStrokeActive;

    internal bool CanPaintMarginAsPencil =>
        IsMarginMode && (IsMarginClosed || MarginPointCount >= 3);

    internal bool ShouldUseFastMarginVisual =>
        IsMarginRedrawStrokeActive || DateTime.UtcNow < _marginInteractiveUntilUtc;

    public IReadOnlyList<Point3D> MarginPoints => _marginPoints;

    public Vector3D InsertionAxis =>
        new(DesignManifest.InsertionAxisX, DesignManifest.InsertionAxisY, DesignManifest.InsertionAxisZ);

    /// <summary>Smooth closed loop for shell/bridge math (Catmull-Rom).</summary>
    internal IReadOnlyList<Point3D> BuildMarginDesignLoop()
    {
        if (_marginPoints.Count < 3)
        {
            return _marginPoints.ToList();
        }

        return StatsDesignMarginSpline.DensifyClosed(_marginPoints);
    }

    internal IReadOnlyList<MeshSnapshot> GetPrepSnapshotsForMarginVisual() =>
        StatsDesignPrepMeshResolver.CollectSnapshotsForMargin(_loadedFiles, BuildMarginDesignLoop());

    /// <summary>Polyline points for the orange margin visual in the viewport.</summary>
    internal IReadOnlyList<Point3D> BuildMarginDisplayPolyline()
    {
        if (_isMarginRedrawStrokeActive && _marginRedrawStroke.Count >= 1 && _marginEditBaseLoop.Count >= 2)
        {
            var spliced = StatsDesignMarginLoopEditor.BuildSplicedPolyline(
                _marginEditBaseLoop,
                _marginEditStartIndex,
                _marginEditEndIndex,
                _marginRedrawStroke,
                _marginEditBaseIsClosed);

            if (_marginEditBaseIsClosed && spliced.Count > 0)
            {
                spliced.Add(spliced[0]);
            }

            return spliced;
        }

        if (_isMarginRedrawStrokeActive && _marginRedrawStroke.Count >= 2)
        {
            // Keep interactive preview lightweight.
            return StatsDesignMarginSpline.DensifyOpen(_marginRedrawStroke, samplesPerSegment: 6);
        }

        if (_marginPoints.Count < 2)
        {
            return [];
        }

        if (IsMarginClosed)
        {
            // Keep the on-screen polyline lightweight; dense loop is used for shell math separately.
            var loop = StatsDesignMarginSpline.DensifyClosed(_marginPoints, targetSpacingMm: 1.2, samplesPerSegment: 8);
            if (loop.Count > 0)
            {
                loop.Add(loop[0]);
            }

            return loop;
        }

        return StatsDesignMarginSpline.DensifyOpen(_marginPoints, samplesPerSegment: 6);
    }

    internal StatsDesignMarginGeometry CreateMarginGeometry() =>
        StatsDesignMarginGeometry.Create(BuildMarginDesignLoop(), ResolveInsertionAxis());

    public ObservableCollection<StatsDesignFileOption> AvailableDesignFiles { get; } = [];

    private StatsDesignFileOption? _selectedDesignFile;

    public StatsDesignFileOption? SelectedDesignFile
    {
        get => _selectedDesignFile;
        set
        {
            if (ReferenceEquals(_selectedDesignFile, value))
            {
                return;
            }

            _selectedDesignFile = value;
            OnPropertyChanged();
            RaiseDesignLoadCommandStates();
        }
    }

    public RelayCommand UndoMarginPointCommand { get; private set; } = null!;
    public RelayCommand ClearMarginCommand { get; private set; } = null!;
    public RelayCommand CloseMarginLoopCommand { get; private set; } = null!;
    public RelayCommand LoadSelectedDesignCommand { get; private set; } = null!;
    public RelayCommand UnloadDesignCommand { get; private set; } = null!;

    private void InitMarginCommands()
    {
        UndoMarginPointCommand = new RelayCommand(
            UndoLastMarginPoint,
            () => IsDesignHostMode && IsMarginMode && MarginPointCount > 0);
        ClearMarginCommand = new RelayCommand(
            ClearMargin,
            () => IsDesignHostMode && IsMarginMode && (MarginPointCount > 0 || _isMarginRedrawStrokeActive));
        CloseMarginLoopCommand = new RelayCommand(
            CloseMarginLoop,
            () => IsDesignHostMode && MarginPointCount >= 3 && !IsMarginClosed);
        LoadSelectedDesignCommand = new RelayCommand(
            () => _ = LoadSelectedDesignAsync(),
            () => !IsBusy && IsDesignHostMode && SelectedDesignFile is { IsLoaded: false });
        UnloadDesignCommand = new RelayCommand(
            () => _ = UnloadActiveDesignMeshesAsync(),
            () => !IsBusy && IsDesignHostMode && HasLoadedDesignMeshes);
    }

    public bool HasLoadedDesignMeshes => _loadedDesignPaths.Count > 0;

    internal bool CanPickMarginOnMesh(LoadedMeshItemViewModel item) =>
        IsDesignHostMode &&
        IsMarginMode &&
        StatsDesignPrepMeshResolver.CanHostMargin(item);

    internal void ApplyDesignWorkflowStepMode()
    {
        if (!IsDesignHostMode)
        {
            IsMarginMode = false;
            return;
        }

        var phase = StatsDesignWorkflowCoordinator.Parse(DesignWorkflowStep);
        OnDesignWorkflowStepEntered(phase);
        IsMarginMode = StatsDesignWorkflowCoordinator.EnablesMarginPicking(phase);
        if (IsMarginMode)
        {
            IsSculptMode = false;
            IsSectionMode = false;
            ClearSectionMeasurement();
        }
        else if (StatsDesignWorkflowCoordinator.EnablesSculpting(phase, HasClosedCrown))
        {
            IsSculptMode = true;
        }
        else
        {
            IsSculptMode = false;
        }

        OnPropertyChanged(nameof(IsSculptMode));
        RaiseMarginCommandStates();
        RaiseDesignLoadCommandStates();
        RaiseDesignSafetyCommandStates();
        RaiseDesignNavigationCommandStates();
    }

    internal void LoadMarginFromManifest()
    {
        _marginPoints.Clear();
        foreach (var point in DesignManifest.MarginPoints)
        {
            _marginPoints.Add(new Point3D(point.X, point.Y, point.Z));
        }

        _isMarginClosed = DesignManifest.IsMarginClosed;
        OnPropertyChanged(nameof(IsMarginClosed));
        OnPropertyChanged(nameof(MarginPointCount));
        OnPropertyChanged(nameof(MarginStatusText));
        OnPropertyChanged(nameof(InsertionAxis));

        if (_isMarginClosed && _marginPoints.Count >= 3 &&
            Math.Abs(DesignManifest.InsertionAxisX) < 1e-9 &&
            Math.Abs(DesignManifest.InsertionAxisY) < 1e-9 &&
            Math.Abs(DesignManifest.InsertionAxisZ - 1.0) < 1e-9)
        {
            UpdateInsertionAxisFromPrep();
        }

        _marginGeometryRevision++;
        OnPropertyChanged(nameof(MarginGeometryRevision));
        RaiseMarginCommandStates();
    }

    internal bool TryAddMarginPoint(Point3D point)
    {
        if (!IsMarginMode || IsMarginClosed)
        {
            return false;
        }

        _marginPoints.Add(point);
        NotifyMarginGeometryChanged(persistManifest: true);
        SetTransientStatus($"Margin point {MarginPointCount} placed.");
        return true;
    }

    internal bool TryBeginMarginRedraw(Point3D point)
    {
        if (!CanPaintMarginAsPencil)
        {
            return false;
        }

        PrepareMarginEditSession();
        if (_marginEditBaseLoop.Count < 2)
        {
            return false;
        }

        var closest = StatsDesignMarginLoopEditor.FindClosestPoint(
            _marginEditBaseLoop,
            point,
            _marginEditBaseIsClosed);

        var anchor = closest.Point;

        _marginEditStartIndex = closest.SegmentStartIndex;
        _marginEditEndIndex = closest.SegmentStartIndex;
        _marginRedrawStroke.Clear();
        _marginRedrawStroke.Add(anchor);
        _isMarginRedrawStrokeActive = true;
        NotifyMarginGeometryChanged(persistManifest: false);
        return true;
    }

    internal bool TryExtendMarginRedraw(Point3D point, double minSpacingMm = 0.18)
    {
        if (!_isMarginRedrawStrokeActive || _marginRedrawStroke.Count == 0 || _marginEditBaseLoop.Count < 2)
        {
            return false;
        }

        var last = _marginRedrawStroke[^1];
        var delta = point - last;
        if (delta.LengthSquared < minSpacingMm * minSpacingMm)
        {
            return false;
        }

        _marginRedrawStroke.Add(point);
        var endClosest = StatsDesignMarginLoopEditor.FindClosestPoint(
            _marginEditBaseLoop,
            point,
            _marginEditBaseIsClosed);
        _marginEditEndIndex = endClosest.SegmentStartIndex;
        NotifyMarginGeometryChanged(persistManifest: false);
        return true;
    }

    internal bool TryCommitMarginRedraw()
    {
        if (!_isMarginRedrawStrokeActive)
        {
            return false;
        }

        _isMarginRedrawStrokeActive = false;
        if (_marginRedrawStroke.Count < 2 || _marginEditBaseLoop.Count < 2)
        {
            ClearMarginEditSession();
            NotifyMarginGeometryChanged(persistManifest: false);
            SetTransientStatus("Draw a longer stroke along the margin.");
            return false;
        }

        var mergedDense = StatsDesignMarginLoopEditor.BuildSplicedPolyline(
            _marginEditBaseLoop,
            _marginEditStartIndex,
            _marginEditEndIndex,
            _marginRedrawStroke,
            _marginEditBaseIsClosed);

        if (mergedDense.Count < 3)
        {
            ClearMarginEditSession();
            NotifyMarginGeometryChanged(persistManifest: false);
            SetTransientStatus("Could not update the margin — try a longer stroke.");
            return false;
        }

        var keepClosed = _marginEditBaseIsClosed;

        _marginPoints.Clear();
        foreach (var control in StatsDesignMarginLoopEditor.DecimateToControlPoints(mergedDense))
        {
            _marginPoints.Add(control);
        }

        ClearMarginEditSession();
        if (MarginPointCount >= 3)
        {
            if (keepClosed)
            {
                IsMarginClosed = true;
            }

            UpdateInsertionAxisFromPrep();
            ClearUndercutHotspots();
            UndercutStatusText = "Click Analyze undercuts to check insertion axis.";
        }

        NotifyMarginGeometryChanged(persistManifest: true);
        SetTransientStatus($"Margin updated ({MarginPointCount} control points).");
        return true;
    }

    internal void CancelMarginRedraw()
    {
        if (!_isMarginRedrawStrokeActive && _marginRedrawStroke.Count == 0)
        {
            return;
        }

        _isMarginRedrawStrokeActive = false;
        ClearMarginEditSession();
        NotifyMarginGeometryChanged(persistManifest: false);
    }

    private void PrepareMarginEditSession()
    {
        _marginEditBaseIsClosed = IsMarginClosed && _marginPoints.Count >= 3;
        _marginEditBaseLoop = _marginEditBaseIsClosed
            ? StatsDesignMarginSpline.DensifyClosed(_marginPoints, targetSpacingMm: 1.2, samplesPerSegment: 8)
            : StatsDesignMarginSpline.DensifyOpen(_marginPoints, samplesPerSegment: 6);
    }

    private void ClearMarginEditSession()
    {
        _marginRedrawStroke.Clear();
        _marginEditBaseLoop = [];
        _marginEditStartIndex = 0;
        _marginEditEndIndex = 0;
    }

    private void UndoLastMarginPoint()
    {
        if (_marginPoints.Count == 0)
        {
            return;
        }

        _marginPoints.RemoveAt(_marginPoints.Count - 1);
        if (_marginPoints.Count < 3)
        {
            IsMarginClosed = false;
        }

        NotifyMarginGeometryChanged(persistManifest: true);
    }

    internal void ClearMargin()
    {
        if (!WpfUiThread.CheckAccess())
        {
            WpfUiThread.Invoke(ClearMargin);
            return;
        }

        CancelMarginRedraw();
        _marginPoints.Clear();
        _isMarginClosed = false;
        DesignManifest.IsMarginClosed = false;
        DesignManifest.MarginPoints = [];
        DesignManifest.InsertionAxisX = 0;
        DesignManifest.InsertionAxisY = 0;
        DesignManifest.InsertionAxisZ = 1;
        ClearUndercutHotspots();
        UndercutStatusText = "Close the margin to analyze undercuts.";
        FlushMarginManifestPersist();
        NotifyMarginGeometryChanged(persistManifest: true);
        SetTransientStatus("Margin cleared.");
    }

    private void CloseMarginLoop()
    {
        if (_marginPoints.Count < 3)
        {
            SetTransientStatus("Need at least 3 points to close the margin loop.");
            return;
        }

        IsMarginClosed = true;
        UpdateInsertionAxisFromPrep();
        ClearUndercutHotspots();
        UndercutStatusText = "Click Analyze undercuts to check insertion axis.";
        _ = RealignPlacedLibraryToothAsync();
        NotifyMarginGeometryChanged(persistManifest: true);
        SetTransientStatus("Margin loop closed. Use Next to open Inner shell, or stay here to analyze undercuts.");
        DesignWorkflowStep = "Inner";
    }

    private void UpdateInsertionAxisFromPrep()
    {
        if (_marginPoints.Count < 3)
        {
            return;
        }

        var marginLoop = BuildMarginDesignLoop();
        var prep = StatsDesignPrepMeshResolver.PickPrimary(_loadedFiles, marginLoop, DesignManifest.MarginHostFilePath);

        Point3D? occlusalHint = null;
        if (prep is not null)
        {
            occlusalHint = StatsDesignInsertionAxis.EstimateOcclusalHint(_marginPoints, prep);
        }

        var axis = StatsDesignInsertionAxis.Calculate(_marginPoints, occlusalHint);
        DesignManifest.InsertionAxisX = axis.X;
        DesignManifest.InsertionAxisY = axis.Y;
        DesignManifest.InsertionAxisZ = axis.Z;
        PersistDesignManifest();
        OnPropertyChanged(nameof(InsertionAxis));
    }

    private Vector3D? ResolveInsertionAxis()
    {
        var axis = InsertionAxis;
        return axis.LengthSquared > 1e-12 ? axis : null;
    }

    private void SyncMarginToManifestFields()
    {
        DesignManifest.MarginPoints = _marginPoints
            .Select(p => new StatsDesignMarginPoint { X = p.X, Y = p.Y, Z = p.Z })
            .ToList();
        DesignManifest.IsMarginClosed = _isMarginClosed;
        if (_marginPoints.Count >= 3)
        {
            var host = StatsDesignPrepMeshResolver.ResolveMarginHost(_loadedFiles, _marginPoints);
            DesignManifest.MarginHostFilePath = host?.FilePath;
        }
        else
        {
            DesignManifest.MarginHostFilePath = null;
        }
    }

    private void ScheduleMarginManifestPersist()
    {
        if (!IsDesignHostMode)
        {
            return;
        }

        _marginManifestPersistTimer ??= new DispatcherTimer(
            TimeSpan.FromMilliseconds(450),
            DispatcherPriority.Background,
            (_, _) => FlushMarginManifestPersist(),
            WpfUiThread.RequireDispatcher());
        _marginManifestPersistTimer.Stop();
        _marginManifestPersistTimer.Start();
    }

    private void FlushMarginManifestPersist()
    {
        _marginManifestPersistTimer?.Stop();
        if (!IsDesignHostMode)
        {
            return;
        }

        SyncMarginToManifestFields();
        PersistDesignManifest();
    }

    private void NotifyMarginGeometryChanged(bool persistManifest)
    {
        _marginInteractiveUntilUtc = DateTime.UtcNow.AddMilliseconds(700);
        SyncMarginToManifestFields();
        if (persistManifest)
        {
            FlushMarginManifestPersist();
        }
        else
        {
            ScheduleMarginManifestPersist();
        }

        _marginGeometryRevision++;
        OnPropertyChanged(nameof(MarginGeometryRevision));
        OnPropertyChanged(nameof(MarginPointCount));
        OnPropertyChanged(nameof(MarginStatusText));
        OnPropertyChanged(nameof(IsMarginClosed));
        RaiseMarginCommandStates();
        MarginVisualRefreshRequested?.Invoke();
    }

    private static IReadOnlyList<Point3D> SimplifyMarginStroke(IReadOnlyList<Point3D> stroke, int maxControlPoints = 48)
    {
        if (stroke.Count <= maxControlPoints)
        {
            return stroke.ToList();
        }

        var result = new List<Point3D>(maxControlPoints);
        var step = (double)(stroke.Count - 1) / (maxControlPoints - 1);
        for (var i = 0; i < maxControlPoints; i++)
        {
            var index = (int)Math.Round(i * step);
            index = Math.Clamp(index, 0, stroke.Count - 1);
            if (result.Count == 0 || result[^1] != stroke[index])
            {
                result.Add(stroke[index]);
            }
        }

        return result;
    }

    private void RaiseMarginCommandStates()
    {
        UndoMarginPointCommand.RaiseCanExecuteChanged();
        ClearMarginCommand.RaiseCanExecuteChanged();
        CloseMarginLoopCommand.RaiseCanExecuteChanged();
        GenerateInnerShellCommand.RaiseCanExecuteChanged();
        AnalyzeUndercutsCommand.RaiseCanExecuteChanged();
        RaiseDesignNavigationCommandStates();
    }

    private void RaiseDesignLoadCommandStates()
    {
        LoadSelectedDesignCommand.RaiseCanExecuteChanged();
        UnloadDesignCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(HasLoadedDesignMeshes));
        OnPropertyChanged(nameof(HasDesignMeshes));
    }

    internal void RefreshAvailableDesignFiles(string orderFolder)
    {
        AvailableDesignFiles.Clear();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddCandidate(string path, bool isGeneratedShell)
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath) || !seen.Add(fullPath))
            {
                return;
            }

            var ext = Path.GetExtension(fullPath);
            if (!ext.Equals(".dcm", StringComparison.OrdinalIgnoreCase) &&
                !ext.Equals(".stl", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            AvailableDesignFiles.Add(new StatsDesignFileOption
            {
                FilePath = fullPath,
                DisplayName = Path.GetFileName(fullPath),
                IsGeneratedShell = isGeneratedShell,
                IsLoaded = _loadedDesignPaths.Contains(fullPath)
            });
        }

        var cadFolder = Path.Combine(orderFolder, "CAD");
        if (Directory.Exists(cadFolder))
        {
            foreach (var file in Directory.EnumerateFiles(cadFolder, "*.*", SearchOption.AllDirectories))
            {
                AddCandidate(file, isGeneratedShell: false);
            }
        }

        var statsCad = StatsDesignPaths.GetCadFolder(orderFolder);
        if (Directory.Exists(statsCad))
        {
            foreach (var file in Directory.EnumerateFiles(statsCad, "*.stl", SearchOption.TopDirectoryOnly))
            {
                AddCandidate(file, isGeneratedShell: file.Contains("_shell", StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    internal static IEnumerable<StatsClient.MVVM.Core.DCMFileItem> FilterScanOnlyCaseFiles(
        IEnumerable<StatsClient.MVVM.Core.DCMFileItem> files,
        string orderFolder)
    {
        var cadRoot = Path.GetFullPath(Path.Combine(orderFolder, "CAD"));
        var statsCad = Path.GetFullPath(StatsDesignPaths.GetCadFolder(orderFolder));

        foreach (var item in files)
        {
            if (string.IsNullOrWhiteSpace(item.FilePath))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(item.FilePath);
            if (!StatsClient.MVVM.Core.CaseScanDiscoveryRules.ShouldIncludeInCaseDiscovery(fullPath))
            {
                continue;
            }

            if (item.SourceKind == StatsClient.MVVM.Core.DCMFileSourceKind.DesignedElement ||
                item.IsDesigned ||
                fullPath.StartsWith(cadRoot, StringComparison.OrdinalIgnoreCase) ||
                fullPath.StartsWith(statsCad, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return item;
        }
    }

    private async Task LoadSelectedDesignAsync()
    {
        if (SelectedDesignFile is null || SelectedDesignFile.IsLoaded)
        {
            return;
        }

        await LoadDesignFileAsync(SelectedDesignFile.FilePath, SelectedDesignFile.IsGeneratedShell);
    }

    internal async Task LoadDesignFileAsync(string filePath, bool isGeneratedShell = false, bool manageBusyScope = true)
    {
        if (!IsDesignHostMode || string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return;
        }

        if (manageBusyScope)
        {
            _ = BeginCancellableBusy($"Loading {Path.GetFileName(filePath)}...");
        }

        try
        {
            await LoadDesignFileCoreAsync(filePath);
            SetTransientStatus($"Loaded design: {Path.GetFileName(filePath)}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            SetTransientStatus($"Could not load design: {ex.Message}");
        }
        finally
        {
            if (manageBusyScope)
            {
                CompleteBusyWork();
            }
        }
    }

    private async Task LoadDesignFileCoreAsync(string filePath, bool advanceWorkflow = true)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (_loadedDesignPaths.Contains(fullPath))
        {
            return;
        }

        if (StatsDesignShellService.IsCrownPath(fullPath))
        {
            _crownPath = fullPath;
        }
        else if (StatsDesignShellService.IsInnerShellPath(fullPath))
        {
            _innerShellPath = fullPath;
        }

        CategoryOverrides[fullPath] = StatsDesignShellService.IsInnerShellPath(fullPath)
            ? "abutment"
            : "restoration";
        TextureOverrides[fullPath] = StatsDesignShellService.IsInnerShellPath(fullPath)
            ? "DesignInner"
            : "Zirconia";
        await LoadFilesAsync(new[] { fullPath }, clearExisting: false, BusyCancellationToken);

        foreach (var item in _loadedFiles.Where(f =>
                     string.Equals(Path.GetFullPath(f.FilePath), fullPath, StringComparison.OrdinalIgnoreCase)))
        {
            item.IsVisible = true;
        }

        _loadedDesignPaths.Add(fullPath);
        DesignManifest.LoadedDesignFilePaths = _loadedDesignPaths.ToList();
        PersistDesignManifest();

        if (!string.IsNullOrWhiteSpace(_designOrderFolderPath))
        {
            RefreshAvailableDesignFiles(_designOrderFolderPath);
        }

        MarkDesignFileLoaded(fullPath, true);
        if (advanceWorkflow)
        {
            DesignWorkflowStep = "Sculpt";
            ApplyDesignWorkflowStepMode();
        }

        OnPropertyChanged(nameof(HasDesignMeshes));
        RaiseDesignLoadCommandStates();
    }

    internal async Task UnloadActiveDesignMeshesAsync()
    {
        if (!IsDesignHostMode || _loadedDesignPaths.Count == 0)
        {
            return;
        }

        _ = BeginCancellableBusy("Unloading design meshes...");
        try
        {
            var paths = _loadedDesignPaths.ToList();
            foreach (var path in paths)
            {
                UnloadFile(path);
                _loadedDesignPaths.Remove(path);
                MarkDesignFileLoaded(path, false);
            }

            DesignManifest.LoadedDesignFilePaths = [];
            _innerShellPath = null;
            _crownPath = null;
            _libraryToothPath = null;
            _librarySourceDcmPath = null;
            _libraryBaselineMesh = null;
            PersistDesignManifest();
            OnPropertyChanged(nameof(HasInnerShell));
            OnPropertyChanged(nameof(HasClosedCrown));
            OnPropertyChanged(nameof(HasLibraryToothPlaced));
            OnPropertyChanged(nameof(HasDesignMeshes));
            RaiseDesignLoadCommandStates();
            RaiseDesignShapeCommandStates();
            SetTransientStatus("Design mesh unloaded.");
            await Task.CompletedTask;
        }
        finally
        {
            CompleteBusyWork();
        }
    }

    private void MarkDesignFileLoaded(string fullPath, bool isLoaded)
    {
        foreach (var option in AvailableDesignFiles)
        {
            if (string.Equals(option.FilePath, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                option.IsLoaded = isLoaded;
            }
        }

        if (SelectedDesignFile is not null &&
            string.Equals(SelectedDesignFile.FilePath, fullPath, StringComparison.OrdinalIgnoreCase))
        {
            SelectedDesignFile.IsLoaded = isLoaded;
        }
    }

    internal Task RestoreLoadedDesignFilesAsync()
    {
        if (!WpfUiThread.CheckAccess())
        {
            return WpfUiThread.RunAsync(RestoreLoadedDesignFilesAsync);
        }

        return RestoreLoadedDesignFilesCoreAsync();
    }

    private async Task RestoreLoadedDesignFilesCoreAsync()
    {
        if (!IsDesignHostMode || string.IsNullOrWhiteSpace(_designOrderFolderPath))
        {
            return;
        }

        foreach (var path in DesignManifest.LoadedDesignFilePaths.ToList())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            await LoadDesignFileCoreAsync(path, advanceWorkflow: false);
        }

        if (_loadedDesignPaths.Count > 0)
        {
            DesignWorkflowStep = "Sculpt";
            ApplyDesignWorkflowStepMode();
            SetTransientStatus("Restored previously loaded design mesh(es).");
        }
    }
}

public sealed class StatsDesignFileOption : INotifyPropertyChanged
{
    private bool _isLoaded;

    public required string FilePath { get; init; }
    public required string DisplayName { get; init; }
    public bool IsGeneratedShell { get; init; }

    public bool IsLoaded
    {
        get => _isLoaded;
        set
        {
            if (_isLoaded == value)
            {
                return;
            }

            _isLoaded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoaded)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
