using DCMViewer.Infrastructure;
using DCMViewer.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Media.Media3D;

namespace DCMViewer.ViewModels;

public partial class MainViewModel
{
    private MeshSnapshot? _libraryBaselineMesh;
    private string? _librarySourceDcmPath;
    private string? _libraryToothPath;
    private string? _innerShellPath;
    private string? _crownPath;

    public ObservableCollection<StatsDesignToothLibraryEntry> LibraryTeeth { get; } = [];

    private StatsDesignToothLibraryEntry? _selectedLibraryTooth;

    public StatsDesignToothLibraryEntry? SelectedLibraryTooth
    {
        get => _selectedLibraryTooth;
        set
        {
            if (ReferenceEquals(_selectedLibraryTooth, value))
            {
                return;
            }

            _selectedLibraryTooth = value;
            OnPropertyChanged();
            LoadLibraryToothCommand.RaiseCanExecuteChanged();
        }
    }

    public int LibraryToothNumber
    {
        get => SelectedLibraryTooth?.ToothNumber ?? 0;
        set
        {
            var entry = StatsDesignLibraryService.FindByToothNumber(value);
            if (entry is not null)
            {
                SelectedLibraryTooth = entry;
            }
        }
    }

    public double LibraryOffsetXmm
    {
        get => DesignManifest.LibraryOffsetXmm;
        set
        {
            if (Math.Abs(DesignManifest.LibraryOffsetXmm - value) < 1e-6)
            {
                return;
            }

            DesignManifest.LibraryOffsetXmm = value;
            OnPropertyChanged();
            ApplyLibraryPlacementTransform();
        }
    }

    public double LibraryOffsetYmm
    {
        get => DesignManifest.LibraryOffsetYmm;
        set
        {
            if (Math.Abs(DesignManifest.LibraryOffsetYmm - value) < 1e-6)
            {
                return;
            }

            DesignManifest.LibraryOffsetYmm = value;
            OnPropertyChanged();
            ApplyLibraryPlacementTransform();
        }
    }

    public double LibraryOffsetZmm
    {
        get => DesignManifest.LibraryOffsetZmm;
        set
        {
            if (Math.Abs(DesignManifest.LibraryOffsetZmm - value) < 1e-6)
            {
                return;
            }

            DesignManifest.LibraryOffsetZmm = value;
            OnPropertyChanged();
            ApplyLibraryPlacementTransform();
        }
    }

    public double LibraryScaleX
    {
        get => DesignManifest.LibraryScaleX;
        set
        {
            var clamped = Math.Clamp(value, 0.2, 3.0);
            if (Math.Abs(DesignManifest.LibraryScaleX - clamped) < 1e-6)
            {
                return;
            }

            DesignManifest.LibraryScaleX = clamped;
            OnPropertyChanged();
            ApplyLibraryPlacementTransform();
        }
    }

    public double LibraryScaleY
    {
        get => DesignManifest.LibraryScaleY;
        set
        {
            var clamped = Math.Clamp(value, 0.2, 3.0);
            if (Math.Abs(DesignManifest.LibraryScaleY - clamped) < 1e-6)
            {
                return;
            }

            DesignManifest.LibraryScaleY = clamped;
            OnPropertyChanged();
            ApplyLibraryPlacementTransform();
        }
    }

    public double LibraryScaleZ
    {
        get => DesignManifest.LibraryScaleZ;
        set
        {
            var clamped = Math.Clamp(value, 0.2, 3.0);
            if (Math.Abs(DesignManifest.LibraryScaleZ - clamped) < 1e-6)
            {
                return;
            }

            DesignManifest.LibraryScaleZ = clamped;
            OnPropertyChanged();
            ApplyLibraryPlacementTransform();
        }
    }

    public double LibraryUniformScale
    {
        get => DesignManifest.LibraryUniformScale;
        set
        {
            var clamped = Math.Clamp(value, 0.2, 3.0);
            if (Math.Abs(DesignManifest.LibraryUniformScale - clamped) < 1e-6)
            {
                return;
            }

            DesignManifest.LibraryUniformScale = clamped;
            OnPropertyChanged();
            ApplyLibraryPlacementTransform();
        }
    }

    public bool HasInnerShell => !string.IsNullOrWhiteSpace(_innerShellPath) && File.Exists(_innerShellPath);

    public string InnerShellStatusText { get; private set; } = "Set cement values, then generate the open inner shell.";
    public bool HasLibraryToothPlaced => !string.IsNullOrWhiteSpace(_libraryToothPath) && File.Exists(_libraryToothPath);
    public bool HasClosedCrown => !string.IsNullOrWhiteSpace(_crownPath) && File.Exists(_crownPath);

    public RelayCommand GenerateInnerShellCommand { get; private set; } = null!;
    public RelayCommand LoadLibraryToothCommand { get; private set; } = null!;
    public RelayCommand ConnectToMarginCommand { get; private set; } = null!;

    private void InitDesignShapeCommands()
    {
        GenerateInnerShellCommand = new RelayCommand(
            () => _ = GenerateInnerShellAsync(),
            () => !IsBusy && IsDesignHostMode);
        LoadLibraryToothCommand = new RelayCommand(
            () => _ = LoadLibraryToothAsync(),
            () => !IsBusy && IsDesignHostMode && SelectedLibraryTooth is not null && HasInnerShell);
        ConnectToMarginCommand = new RelayCommand(
            () => _ = ConnectToMarginAsync(),
            () => !IsBusy && IsDesignHostMode && HasInnerShell && HasLibraryToothPlaced && IsMarginClosed);
    }

    private void RefreshLibraryToothCatalog()
    {
        LibraryTeeth.Clear();
        try
        {
            foreach (var entry in StatsDesignLibraryService.ListToothEntries())
            {
                LibraryTeeth.Add(entry);
            }

            var root = StatsDesignLibraryService.ResolveLibraryRoot();
            LibraryCatalogStatus = LibraryTeeth.Count == 0
                ? $"No teeth found under {root}"
                : $"{LibraryTeeth.Count} teeth — {root}";
        }
        catch (Exception ex)
        {
            LibraryCatalogStatus = $"Library unavailable: {ex.Message}";
            SetTransientStatus($"Tooth library: {ex.Message}");
        }

        OnPropertyChanged(nameof(LibraryCatalogStatus));

        if (SelectedLibraryTooth is null && LibraryTeeth.Count > 0)
        {
            SelectedLibraryTooth = LibraryTeeth[0];
        }
        else if (SelectedLibraryTooth is not null &&
                 !LibraryTeeth.Any(e => e.ToothNumber == SelectedLibraryTooth.ToothNumber && e.IsOad == SelectedLibraryTooth.IsOad))
        {
            SelectedLibraryTooth = LibraryTeeth.FirstOrDefault(e => e.ToothNumber == SelectedLibraryTooth.ToothNumber)
                                   ?? LibraryTeeth.FirstOrDefault();
        }

        LoadLibraryToothCommand.RaiseCanExecuteChanged();
    }

    internal Task RestoreDesignArtifactsAsync()
    {
        if (!WpfUiThread.CheckAccess())
        {
            return WpfUiThread.RunAsync(RestoreDesignArtifactsAsync);
        }

        return RestoreDesignArtifactsCoreAsync();
    }

    private async Task RestoreDesignArtifactsCoreAsync()
    {
        RestoreDesignArtifactPaths();
        if (!string.IsNullOrWhiteSpace(_innerShellPath) && File.Exists(_innerShellPath))
        {
            await LoadDesignMeshAsync(_innerShellPath, DesignMeshRole.InnerShell);
        }

        if (!string.IsNullOrWhiteSpace(_crownPath) && File.Exists(_crownPath))
        {
            await LoadDesignMeshAsync(_crownPath, DesignMeshRole.Crown);
            DesignWorkflowStep = "Sculpt";
            ApplyDesignWorkflowStepMode();
        }
        else if (HasInnerShell)
        {
            DesignWorkflowStep = HasLibraryToothPlaced ? "Shape" : "Inner";
            ApplyDesignWorkflowStepMode();
        }

        OnPropertyChanged(nameof(HasInnerShell));
        OnPropertyChanged(nameof(HasClosedCrown));
        OnPropertyChanged(nameof(HasLibraryToothPlaced));
        OnPropertyChanged(nameof(HasDesignMeshes));
        RaiseDesignShapeCommandStates();
        RaiseDesignNavigationCommandStates();
    }

    internal void RestoreDesignArtifactPaths()
    {
        _innerShellPath = null;
        _crownPath = null;
        _libraryToothPath = null;

        if (string.IsNullOrWhiteSpace(_designOrderFolderPath))
        {
            return;
        }

        var cad = StatsDesignPaths.GetCadFolder(_designOrderFolderPath);
        if (!string.IsNullOrWhiteSpace(DesignManifest.InnerShellRelativePath))
        {
            _innerShellPath = Path.GetFullPath(Path.Combine(cad, DesignManifest.InnerShellRelativePath));
        }

        if (!string.IsNullOrWhiteSpace(DesignManifest.CrownRelativePath))
        {
            _crownPath = Path.GetFullPath(Path.Combine(cad, DesignManifest.CrownRelativePath));
        }
    }

    private void SetInnerShellStatus(string message)
    {
        InnerShellStatusText = message;
        OnPropertyChanged(nameof(InnerShellStatusText));
        SetTransientStatus(message);
    }

    private IReadOnlyList<MeshSnapshot> CollectPrepSnapshotsForInnerShell()
    {
        if (!IsMarginClosed || MarginPointCount < 3)
        {
            return [];
        }

        var marginLoop = BuildMarginDesignLoop();
        var hostSnapshot = StatsDesignPrepMeshResolver.ResolveMarginHostSnapshot(
            _loadedFiles,
            marginLoop,
            DesignManifest.MarginHostFilePath);
        return hostSnapshot is null ? [] : [hostSnapshot];
    }

    private async Task GenerateInnerShellAsync()
    {
        if (!IsDesignHostMode || string.IsNullOrWhiteSpace(_designOrderFolderPath))
        {
            return;
        }

        if (!IsMarginClosed || MarginPointCount < 3)
        {
            SetInnerShellStatus("Close the margin loop on the Margin step first (at least 3 points).");
            DesignWorkflowStep = "Margin";
            ApplyDesignWorkflowStepMode();
            return;
        }

        var orderFolder = _designOrderFolderPath;
        FlushMarginManifestPersist();
        SyncMarginToManifestFields();
        if (!DesignManifest.IsMarginClosed)
        {
            DesignManifest.IsMarginClosed = true;
        }

        var insertionAxis = ResolveDesignInsertionAxis();
        DesignManifest.InsertionAxisX = insertionAxis.X;
        DesignManifest.InsertionAxisY = insertionAxis.Y;
        DesignManifest.InsertionAxisZ = insertionAxis.Z;

        _ = BeginCancellableBusy("Generating inner shell...");
        SetInnerShellStatus("Generating inner shell...");
        try
        {
            var prepSnapshots = CollectPrepSnapshotsForInnerShell();

            if (prepSnapshots.Count == 0)
            {
                SetInnerShellStatus("No scan surface found for the margin. Turn on the mesh you drew the margin on in the file list on the right.");
                return;
            }

            await UnloadInnerShellAsync(silent: true);

            var marginLoop = BuildMarginDesignLoop();
            if (marginLoop.Count < 3)
            {
                SetInnerShellStatus("Margin loop is too short. Close the margin on the Margin step.");
                return;
            }

            var axis = insertionAxis;
            var innerMesh = await Task.Run(() =>
            {
                var shell = StatsDesignShellService.CreateOpenInnerShell(prepSnapshots, marginLoop, DesignManifest);
                if (shell.TriangleCount == 0)
                {
                    throw new InvalidOperationException(
                        "Inner shell builder returned an empty mesh. Re-draw the margin on the scan that contains the preparation.");
                }

                return StatsDesignUndercutRemover.RemoveSeatPathUndercuts(shell, marginLoop, axis).Mesh;
            });

            if (innerMesh.TriangleCount == 0)
            {
                SetInnerShellStatus("Inner shell is empty after undercut removal. Try a smaller smooth distance or re-draw the margin.");
                return;
            }

            var stlPath = StatsDesignPaths.BuildInnerShellStlPath(orderFolder, DesignManifest.DesignName);
            Directory.CreateDirectory(Path.GetDirectoryName(stlPath)!);
            await Task.Run(() => MeshExportService.Export(stlPath, new[] { innerMesh }));

            if (!File.Exists(stlPath) || new FileInfo(stlPath).Length < 80)
            {
                SetInnerShellStatus("Inner shell file was not written. Check disk space and folder permissions under StatsCAD.");
                return;
            }

            _innerShellPath = stlPath;
            DesignManifest.InnerShellRelativePath = StatsDesignPaths.TryGetRelativeCadPath(orderFolder, stlPath);
            PersistDesignManifest();

            await LoadDesignMeshAsync(stlPath, DesignMeshRole.InnerShell);
            DesignWorkflowStep = "Shape";
            ApplyDesignWorkflowStepMode();
            OnPropertyChanged(nameof(HasInnerShell));
            RaiseDesignShapeCommandStates();
            var hostName = StatsDesignPrepMeshResolver.ResolveMarginHost(_loadedFiles, marginLoop)?.DisplayName;
            var hostNote = string.IsNullOrWhiteSpace(hostName) ? string.Empty : $" on {hostName}";
            SetInnerShellStatus($"Inner shell ready{hostNote} ({Path.GetFileName(stlPath)}, {innerMesh.TriangleCount:N0} triangles). Use Next for Library.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var detail = ex.GetBaseException().Message;
            SetInnerShellStatus($"Inner shell failed: {detail}");
            LogDesignInnerShellFailure(ex);
        }
        finally
        {
            CompleteBusyWork();
        }
    }

    private static void LogDesignInnerShellFailure(Exception ex)
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Stats_Client",
                "stats-design-inner-shell.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"[{DateTime.Now:O}] {ex}\r\n");
        }
        catch
        {
            // ignore logging failures
        }
    }

    private async Task LoadLibraryToothAsync()
    {
        if (SelectedLibraryTooth is null || string.IsNullOrWhiteSpace(_designOrderFolderPath))
        {
            return;
        }

        _ = BeginCancellableBusy($"Loading tooth #{SelectedLibraryTooth.ToothNumber}...");
        try
        {
            await UnloadLibraryToothAsync(silent: true);

            _librarySourceDcmPath = SelectedLibraryTooth.FilePath;
            var rawMesh = await Task.Run(() => StatsDesignLibraryService.LoadLibraryToothMesh(_librarySourceDcmPath));
            DesignManifest.LibraryToothFileName = Path.GetFileName(_librarySourceDcmPath);
            DesignManifest.LibraryUniformScale = 1.0;
            DesignManifest.LibraryScaleX = 1.0;
            DesignManifest.LibraryScaleY = 1.0;
            DesignManifest.LibraryScaleZ = 1.0;

            var marginCentroid = new Point3D(
                MarginPoints.Average(p => p.X),
                MarginPoints.Average(p => p.Y),
                MarginPoints.Average(p => p.Z));

            var insertionAxis = InsertionAxis;
            if (insertionAxis.LengthSquared < 1e-12 && IsMarginClosed)
            {
                insertionAxis = StatsDesignInsertionAxis.Calculate(_marginPoints);
            }

            _libraryBaselineMesh = await Task.Run(() =>
                StatsDesignLibraryService.PrepareLibraryBaseline(
                    rawMesh,
                    DesignManifest,
                    insertionAxis,
                    marginCentroid));
            OnPropertyChanged(nameof(LibraryOffsetXmm));
            OnPropertyChanged(nameof(LibraryOffsetYmm));
            OnPropertyChanged(nameof(LibraryOffsetZmm));
            OnPropertyChanged(nameof(LibraryScaleX));
            OnPropertyChanged(nameof(LibraryScaleY));
            OnPropertyChanged(nameof(LibraryScaleZ));
            OnPropertyChanged(nameof(LibraryUniformScale));

            var placed = StatsDesignLibraryService.ApplyPlacementTransform(_libraryBaselineMesh, DesignManifest);
            var tempPath = Path.Combine(
                StatsDesignPaths.GetCadFolder(_designOrderFolderPath),
                $"{DesignManifest.DesignName}_library_{SelectedLibraryTooth.ToothNumber}.stl");
            Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);
            await Task.Run(() => MeshExportService.Export(tempPath, new[] { placed }));

            _libraryToothPath = tempPath;
            await LoadDesignMeshAsync(tempPath, DesignMeshRole.LibraryPreview);
            PersistDesignManifest();
            OnPropertyChanged(nameof(HasLibraryToothPlaced));
            RaiseDesignShapeCommandStates();
            SetTransientStatus($"Tooth #{SelectedLibraryTooth.ToothNumber} placed — adjust position/scale, then Connect to margin.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            SetTransientStatus($"Could not load library tooth: {ex.Message}");
        }
        finally
        {
            CompleteBusyWork();
        }
    }

    private void ApplyLibraryPlacementTransform()
    {
        if (_libraryBaselineMesh is null || string.IsNullOrWhiteSpace(_libraryToothPath))
        {
            PersistDesignManifest();
            return;
        }

        var transformed = StatsDesignLibraryService.ApplyPlacementTransform(_libraryBaselineMesh, DesignManifest);
        var item = FindLoadedFile(_libraryToothPath);
        item?.ReplaceMeshGeometry(transformed);
        PersistDesignManifest();
    }

    private async Task ConnectToMarginAsync()
    {
        if (!HasInnerShell || !HasLibraryToothPlaced || string.IsNullOrWhiteSpace(_designOrderFolderPath))
        {
            return;
        }

        var innerItem = FindLoadedFile(_innerShellPath!);
        var libraryItem = FindLoadedFile(_libraryToothPath!);
        if (innerItem?.MeshSnapshot is null || libraryItem?.MeshSnapshot is null)
        {
            SetTransientStatus("Inner shell and library tooth must be visible in the viewer.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_librarySourceDcmPath) || !File.Exists(_librarySourceDcmPath))
        {
            SetTransientStatus("Reload the library tooth — source DCM path is missing.");
            return;
        }

        _ = BeginCancellableBusy("Connecting crown to margin...");
        try
        {
            var userMarginLoop = BuildMarginDesignLoop();
            var libraryMarginWorld = await Task.Run(() =>
                StatsDesignLibraryService.ReadWorldMarginLoop(_librarySourceDcmPath!, DesignManifest));

            var axis = InsertionAxis;
            if (axis.LengthSquared < 1e-12)
            {
                axis = StatsDesignInsertionAxis.Calculate(_marginPoints);
            }

            var stitchedLibrary = await Task.Run(() =>
                StatsDesignCrownStitcher.SnapCollarToUserMargin(
                    libraryItem.MeshSnapshot,
                    userMarginLoop,
                    axis));

            var crownMesh = await Task.Run(() =>
                StatsDesignConnectService.BuildClosedCrown(
                    stitchedLibrary,
                    innerItem.MeshSnapshot,
                    userMarginLoop,
                    libraryMarginWorld,
                    DesignManifest));

            var crownPath = StatsDesignPaths.BuildCrownStlPath(_designOrderFolderPath, DesignManifest.DesignName);
            Directory.CreateDirectory(Path.GetDirectoryName(crownPath)!);
            await Task.Run(() => MeshExportService.Export(crownPath, new[] { crownMesh }));

            await UnloadInnerShellAsync(silent: true);
            await UnloadLibraryToothAsync(silent: true);

            _crownPath = crownPath;
            DesignManifest.CrownRelativePath = StatsDesignPaths.TryGetRelativeCadPath(_designOrderFolderPath, crownPath);
            PersistDesignManifest();

            await LoadDesignMeshAsync(crownPath, DesignMeshRole.Crown);
            DesignWorkflowStep = "Sculpt";
            ApplyDesignWorkflowStepMode();
            OnPropertyChanged(nameof(HasClosedCrown));
            OnPropertyChanged(nameof(HasDesignMeshes));
            ClearDesignSafetyVisualization();
            RaiseDesignShapeCommandStates();
            SetTransientStatus($"Closed crown created ({Path.GetFileName(crownPath)}). Zirconia — sculpt and export when ready.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            SetTransientStatus($"Connect failed: {ex.Message}");
        }
        finally
        {
            CompleteBusyWork();
        }
    }

    private async Task UnloadInnerShellAsync(bool silent = false)
    {
        if (string.IsNullOrWhiteSpace(_innerShellPath))
        {
            return;
        }

        UnloadDesignPath(_innerShellPath);
        _innerShellPath = null;
        OnPropertyChanged(nameof(HasInnerShell));
        if (!silent)
        {
            SetTransientStatus("Inner shell unloaded.");
        }

        await Task.CompletedTask;
    }

    private async Task UnloadLibraryToothAsync(bool silent = false)
    {
        if (string.IsNullOrWhiteSpace(_libraryToothPath))
        {
            return;
        }

        UnloadDesignPath(_libraryToothPath);
        _libraryToothPath = null;
        _librarySourceDcmPath = null;
        _libraryBaselineMesh = null;
        OnPropertyChanged(nameof(HasLibraryToothPlaced));
        if (!silent)
        {
            SetTransientStatus("Library tooth unloaded.");
        }

        await Task.CompletedTask;
    }

    private void UnloadDesignPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (_loadedDesignPaths.Contains(fullPath))
        {
            UnloadFile(fullPath);
            _loadedDesignPaths.Remove(fullPath);
            MarkDesignFileLoaded(fullPath, false);
        }
    }

    private LoadedMeshItemViewModel? FindLoadedFile(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return _loadedFiles.FirstOrDefault(f =>
            string.Equals(Path.GetFullPath(f.FilePath), fullPath, StringComparison.OrdinalIgnoreCase));
    }

    private void RaiseDesignShapeCommandStates()
    {
        GenerateInnerShellCommand.RaiseCanExecuteChanged();
        LoadLibraryToothCommand.RaiseCanExecuteChanged();
        ConnectToMarginCommand.RaiseCanExecuteChanged();
        RaiseDesignSafetyCommandStates();
        RaiseDesignAdvancedCommandStates();
    }

    private async Task RealignPlacedLibraryToothAsync()
    {
        if (string.IsNullOrWhiteSpace(_librarySourceDcmPath) ||
            !File.Exists(_librarySourceDcmPath) ||
            string.IsNullOrWhiteSpace(_libraryToothPath))
        {
            return;
        }

        if (!IsMarginClosed || MarginPointCount < 3)
        {
            return;
        }

        try
        {
            var rawMesh = await Task.Run(() => StatsDesignLibraryService.LoadLibraryToothMesh(_librarySourceDcmPath));
            var marginCentroid = new Point3D(
                MarginPoints.Average(p => p.X),
                MarginPoints.Average(p => p.Y),
                MarginPoints.Average(p => p.Z));

            var insertionAxis = InsertionAxis;
            if (insertionAxis.LengthSquared < 1e-12)
            {
                insertionAxis = StatsDesignInsertionAxis.Calculate(_marginPoints);
            }

            _libraryBaselineMesh = await Task.Run(() =>
                StatsDesignLibraryService.PrepareLibraryBaseline(
                    rawMesh,
                    DesignManifest,
                    insertionAxis,
                    marginCentroid));

            ApplyLibraryPlacementTransform();
            SetTransientStatus("Library tooth re-aligned to closed margin and insertion axis.");
        }
        catch (Exception ex)
        {
            SetTransientStatus($"Library re-align failed: {ex.Message}");
        }
    }

    private enum DesignMeshRole
    {
        InnerShell,
        LibraryPreview,
        Crown,
        Legacy
    }

    private async Task LoadDesignMeshAsync(string filePath, DesignMeshRole role, bool advanceWorkflow = false)
    {
        var fullPath = Path.GetFullPath(filePath);
        CategoryOverrides[fullPath] = role switch
        {
            DesignMeshRole.InnerShell => "abutment",
            DesignMeshRole.LibraryPreview => "abutment",
            _ => "restoration"
        };
        TextureOverrides[fullPath] = role switch
        {
            DesignMeshRole.InnerShell => "DesignInner",
            DesignMeshRole.LibraryPreview => "LibraryPreview",
            _ => "Zirconia"
        };

        if (_loadedDesignPaths.Contains(fullPath))
        {
            foreach (var item in _loadedFiles.Where(f =>
                         string.Equals(Path.GetFullPath(f.FilePath), fullPath, StringComparison.OrdinalIgnoreCase)))
            {
                item.IsVisible = true;
            }

            return;
        }

        await LoadFilesAsync(new[] { fullPath }, clearExisting: false, BusyCancellationToken);
        foreach (var item in _loadedFiles.Where(f =>
                     string.Equals(Path.GetFullPath(f.FilePath), fullPath, StringComparison.OrdinalIgnoreCase)))
        {
            item.IsVisible = true;
        }

        _loadedDesignPaths.Add(fullPath);
        DesignManifest.LoadedDesignFilePaths = _loadedDesignPaths.ToList();
        if (!string.IsNullOrWhiteSpace(_designOrderFolderPath))
        {
            RefreshAvailableDesignFiles(_designOrderFolderPath);
        }

        MarkDesignFileLoaded(fullPath, true);
        if (advanceWorkflow)
        {
            DesignWorkflowStep = role == DesignMeshRole.Crown ? "Sculpt" : "Shape";
            ApplyDesignWorkflowStepMode();
        }

        OnPropertyChanged(nameof(HasDesignMeshes));
        RaiseDesignLoadCommandStates();
        RaiseDesignShapeCommandStates();
        RaiseDesignNavigationCommandStates();
    }
}
