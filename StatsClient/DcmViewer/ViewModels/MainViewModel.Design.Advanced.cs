using DCMViewer.Infrastructure;
using DCMViewer.Services;
using System.IO;
using System.Windows.Media.Media3D;

namespace DCMViewer.ViewModels;

public partial class MainViewModel
{
    public RelayCommand BlockInnerUndercutsCommand { get; private set; } = null!;
    public RelayCommand BlockCrownSeatUndercutsCommand { get; private set; } = null!;
    public RelayCommand AdaptOcclusionCommand { get; private set; } = null!;
    public RelayCommand GenerateEmergenceProfileCommand { get; private set; } = null!;
    public RelayCommand TrimProximalContactsCommand { get; private set; } = null!;

    private void InitDesignAdvancedCommands()
    {
        BlockInnerUndercutsCommand = new RelayCommand(
            () => _ = BlockInnerUndercutsAsync(),
            () => !IsBusy && IsDesignHostMode && HasInnerShell && IsMarginClosed);
        BlockCrownSeatUndercutsCommand = new RelayCommand(
            () => _ = BlockCrownSeatUndercutsAsync(),
            () => !IsBusy && IsDesignHostMode && HasClosedCrown && IsMarginClosed);
        AdaptOcclusionCommand = new RelayCommand(
            () => _ = AdaptOcclusionAsync(),
            () => !IsBusy && IsDesignHostMode && HasClosedCrown && IsMarginClosed);
        GenerateEmergenceProfileCommand = new RelayCommand(
            () => _ = GenerateEmergenceProfileAsync(),
            () => !IsBusy && IsDesignHostMode && HasClosedCrown && IsMarginClosed);
        TrimProximalContactsCommand = new RelayCommand(
            () => _ = TrimProximalContactsAsync(),
            () => !IsBusy && IsDesignHostMode && HasClosedCrown && IsMarginClosed);
    }

    private void RaiseDesignAdvancedCommandStates()
    {
        BlockInnerUndercutsCommand.RaiseCanExecuteChanged();
        BlockCrownSeatUndercutsCommand.RaiseCanExecuteChanged();
        AdaptOcclusionCommand.RaiseCanExecuteChanged();
        GenerateEmergenceProfileCommand.RaiseCanExecuteChanged();
        TrimProximalContactsCommand.RaiseCanExecuteChanged();
    }

    private Vector3D ResolveDesignInsertionAxis()
    {
        var axis = InsertionAxis;
        if (axis.LengthSquared < 1e-12 && IsMarginClosed && MarginPointCount >= 3)
        {
            axis = StatsDesignInsertionAxis.Calculate(_marginPoints);
        }

        if (axis.LengthSquared < 1e-12)
        {
            axis = new Vector3D(0, 0, 1);
        }
        else
        {
            axis.Normalize();
        }

        return axis;
    }

    private ScanLayerArch InferPrepArch()
    {
        foreach (var item in _loadedFiles)
        {
            if (item.IsLoadFailed || !item.IsVisible || item.Category != MeshCategory.Scan)
            {
                continue;
            }

            var name = Path.GetFileName(item.FilePath);
            if (name.Contains("preparation", StringComparison.OrdinalIgnoreCase) &&
                item.ScanArch != ScanLayerArch.None)
            {
                return item.ScanArch;
            }
        }

        var upperCount = _loadedFiles.Count(item =>
            !item.IsLoadFailed && item.IsVisible && item.Category == MeshCategory.Scan && item.ScanArch == ScanLayerArch.Upper);
        var lowerCount = _loadedFiles.Count(item =>
            !item.IsLoadFailed && item.IsVisible && item.Category == MeshCategory.Scan && item.ScanArch == ScanLayerArch.Lower);
        return upperCount >= lowerCount ? ScanLayerArch.Upper : ScanLayerArch.Lower;
    }

    private IReadOnlyList<MeshSnapshot> CollectOpposingArchMeshes()
    {
        var prepArch = InferPrepArch();
        var opposingArch = prepArch switch
        {
            ScanLayerArch.Upper => ScanLayerArch.Lower,
            ScanLayerArch.Lower => ScanLayerArch.Upper,
            _ => ScanLayerArch.None
        };

        var meshes = new List<MeshSnapshot>();
        foreach (var item in _loadedFiles)
        {
            if (item.IsLoadFailed || !item.IsVisible || item.Category != MeshCategory.Scan || item.MeshSnapshot is null)
            {
                continue;
            }

            if (opposingArch != ScanLayerArch.None && item.ScanArch == opposingArch)
            {
                meshes.Add(item.MeshSnapshot);
                continue;
            }

            var name = Path.GetFileName(item.FilePath);
            if (name.Contains("antagonist", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("opposing", StringComparison.OrdinalIgnoreCase))
            {
                meshes.Add(item.MeshSnapshot);
            }
        }

        return meshes;
    }

    private async Task PersistCrownMeshAsync(MeshSnapshot mesh)
    {
        var crownItem = GetClosedCrownItem();
        crownItem?.ReplaceMeshGeometry(mesh);
        if (!string.IsNullOrWhiteSpace(_crownPath))
        {
            await Task.Run(() => MeshExportService.Export(_crownPath, new[] { mesh }));
        }
    }

    private async Task PersistInnerMeshAsync(MeshSnapshot mesh)
    {
        var innerItem = string.IsNullOrWhiteSpace(_innerShellPath) ? null : FindLoadedFile(_innerShellPath);
        innerItem?.ReplaceMeshGeometry(mesh);
        if (!string.IsNullOrWhiteSpace(_innerShellPath))
        {
            await Task.Run(() => MeshExportService.Export(_innerShellPath, new[] { mesh }));
        }
    }

    private async Task BlockInnerUndercutsAsync()
    {
        var innerItem = string.IsNullOrWhiteSpace(_innerShellPath) ? null : FindLoadedFile(_innerShellPath);
        if (innerItem?.MeshSnapshot is null)
        {
            SetTransientStatus("Generate the inner shell first.");
            return;
        }

        var marginLoop = BuildMarginDesignLoop();
        var axis = ResolveDesignInsertionAxis();
        var snapshot = innerItem.MeshSnapshot;
        _ = BeginCancellableBusy("Blocking seat-path undercuts on inner shell...");
        try
        {
            var result = await Task.Run(() =>
                StatsDesignUndercutRemover.RemoveSeatPathUndercuts(snapshot, marginLoop, axis));
            await PersistInnerMeshAsync(result.Mesh);
            SafetyStatusText = $"Inner undercuts: adjusted {result.AdjustedVertexCount} vertices in {result.IterationsUsed} pass(es).";
            SetTransientStatus(SafetyStatusText);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            SetTransientStatus($"Inner undercut block failed: {ex.Message}");
        }
        finally
        {
            CompleteBusyWork();
        }
    }

    private async Task BlockCrownSeatUndercutsAsync()
    {
        var crownItem = GetClosedCrownItem();
        if (crownItem?.MeshSnapshot is null)
        {
            return;
        }

        var marginLoop = BuildMarginDesignLoop();
        var axis = ResolveDesignInsertionAxis();
        var snapshot = crownItem.MeshSnapshot;
        _ = BeginCancellableBusy("Blocking seat-path undercuts on crown...");
        try
        {
            var result = await Task.Run(() =>
                StatsDesignUndercutRemover.RemoveSeatPathUndercuts(snapshot, marginLoop, axis));
            await PersistCrownMeshAsync(result.Mesh);
            SafetyStatusText = $"Crown seat path: adjusted {result.AdjustedVertexCount} vertices in {result.IterationsUsed} pass(es).";
            SetTransientStatus(SafetyStatusText);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            SetTransientStatus($"Crown undercut block failed: {ex.Message}");
        }
        finally
        {
            CompleteBusyWork();
        }
    }

    private async Task AdaptOcclusionAsync()
    {
        var crownItem = GetClosedCrownItem();
        if (crownItem?.MeshSnapshot is null)
        {
            return;
        }

        var opposing = CollectOpposingArchMeshes();
        if (opposing.Count == 0)
        {
            SetTransientStatus("Load the opposing arch scan (or antagonist) to adapt occlusion.");
            return;
        }

        var marginLoop = BuildMarginDesignLoop();
        var axis = ResolveDesignInsertionAxis();
        var snapshot = crownItem.MeshSnapshot;
        _ = BeginCancellableBusy("Adapting occlusal clearance...");
        try
        {
            var result = await Task.Run(() =>
                StatsDesignOcclusalAdapter.Adapt(
                    snapshot,
                    opposing,
                    marginLoop,
                    axis,
                    OcclusalClearanceMm));
            await PersistCrownMeshAsync(result.Mesh);
            SafetyStatusText = $"Occlusion: shaved {result.AdjustedVertexCount} cusp vertices ({OcclusalClearanceMm:F2} mm clearance).";
            SetTransientStatus(SafetyStatusText);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            SetTransientStatus($"Occlusal adaptation failed: {ex.Message}");
        }
        finally
        {
            CompleteBusyWork();
        }
    }

    private async Task GenerateEmergenceProfileAsync()
    {
        var crownItem = GetClosedCrownItem();
        if (crownItem?.MeshSnapshot is null)
        {
            return;
        }

        var marginLoop = BuildMarginDesignLoop();
        var axis = ResolveDesignInsertionAxis();
        var snapshot = crownItem.MeshSnapshot;
        _ = BeginCancellableBusy("Generating emergence profile...");
        try
        {
            var result = await Task.Run(() =>
                StatsDesignEmergenceProfile.Generate(snapshot, marginLoop, axis, EmergenceTransitionMm));
            await PersistCrownMeshAsync(result.Mesh);
            SafetyStatusText = $"Emergence profile: shaped {result.AdjustedVertexCount} collar vertices over {EmergenceTransitionMm:F1} mm.";
            SetTransientStatus(SafetyStatusText);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            SetTransientStatus($"Emergence profile failed: {ex.Message}");
        }
        finally
        {
            CompleteBusyWork();
        }
    }

    private async Task TrimProximalContactsAsync()
    {
        var crownItem = GetClosedCrownItem();
        if (crownItem?.MeshSnapshot is null)
        {
            return;
        }

        var prep = PickPrimaryPrepMesh();
        if (prep is null)
        {
            SetTransientStatus("No arch scan for proximal trimming. Show the scan that contains the preparation.");
            return;
        }

        var adjacent = CollectContactReferenceMeshes(prep);
        if (adjacent.Count == 0)
        {
            SetTransientStatus("Load adjacent arch scans to trim proximal contacts.");
            return;
        }

        var axis = ResolveDesignInsertionAxis();
        var snapshot = crownItem.MeshSnapshot;
        _ = BeginCancellableBusy("Trimming proximal contacts...");
        try
        {
            var result = await Task.Run(() =>
                StatsDesignProximalContactTrimmer.Trim(snapshot, adjacent, axis, safetyBufferMm: 0.01));
            await PersistCrownMeshAsync(result.Mesh);
            SafetyStatusText = $"Proximal trim: relaxed {result.TrimmedVertexCount} side-wall vertices.";
            SetTransientStatus(SafetyStatusText);
            if (IsContactHeatmapVisible)
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
            SetTransientStatus($"Proximal trim failed: {ex.Message}");
        }
        finally
        {
            CompleteBusyWork();
        }
    }
}
