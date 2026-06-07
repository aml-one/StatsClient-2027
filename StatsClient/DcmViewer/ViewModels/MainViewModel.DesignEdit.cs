using DCMViewer.Infrastructure;
using DCMViewer.Services;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace DCMViewer.ViewModels;

public sealed class DesignEditStepListItem
{
    public int StepNumber { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
}

public partial class MainViewModel
{
    private const int MaxDesignEditUndoSteps = 50;

    private sealed class DesignEditHistoryEntry
    {
        public required DesignEditStepType Type { get; init; }
        public required LoadedMeshItemViewModel Target { get; init; }
        public required DesignEditStepRecord Record { get; init; }
        public Point3D[]? SculptBefore { get; init; }
        public Point3D[]? SculptAfter { get; init; }
        public MeshSnapshot? MeshBefore { get; init; }
        public MeshSnapshot? MeshAfter { get; init; }
    }

    private bool _isDesignEditMode;
    private bool _isCutPlaneMode;
    private bool _cutPlaneRemovePositiveSide = true;
    private bool _canUndoDesignEdit;
    private bool _canRedoDesignEdit;
    private bool _canUndoAllDesignEdit;
    private bool _hasCutPlanePreview;
    private double _cutPlaneOffset;
    private string? _designEditOrderFolderPath;
    private StatsDesignEditStepStore? _designEditStepStore;
    private readonly Dictionary<string, MeshSnapshot> _designEditOriginalMeshes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Stack<DesignEditHistoryEntry> _designEditUndoStack = new();
    private readonly Stack<DesignEditHistoryEntry> _designEditRedoStack = new();
    private LoadedMeshItemViewModel? _cutPlaneTarget;
    private Point3D _cutPlanePoint;
    private Vector3D _cutPlaneNormal = new(0, 0, 1);

    public bool IsDesignEditMode
    {
        get => _isDesignEditMode;
        private set
        {
            if (_isDesignEditMode == value)
            {
                return;
            }

            _isDesignEditMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SculptDesignablesOnly));
            OnPropertyChanged(nameof(ShowDesignEditPanel));
        }
    }

    public bool ShowDesignEditPanel => IsDesignEditMode && IsOrderInfoHostMode;

    public bool IsCutPlaneMode
    {
        get => _isCutPlaneMode;
        private set
        {
            if (_isCutPlaneMode == value)
            {
                return;
            }

            _isCutPlaneMode = value;
            OnPropertyChanged();
        }
    }

    public bool CutPlaneRemovePositiveSide
    {
        get => _cutPlaneRemovePositiveSide;
        set
        {
            if (_cutPlaneRemovePositiveSide == value)
            {
                return;
            }

            _cutPlaneRemovePositiveSide = value;
            OnPropertyChanged();
            RefreshCutPlanePreview();
        }
    }

    public bool HasCutPlaneReady => _cutPlaneTarget is not null && _hasCutPlanePreview;

    public double CutPlaneOffset
    {
        get => _cutPlaneOffset;
        set
        {
            var clamped = Math.Clamp(value, -1.0, 1.0);
            if (Math.Abs(_cutPlaneOffset - clamped) < 0.0001)
            {
                return;
            }

            _cutPlaneOffset = clamped;
            OnPropertyChanged();
        }
    }

    public bool CanUndoDesignEdit
    {
        get => _canUndoDesignEdit;
        private set
        {
            if (_canUndoDesignEdit == value)
            {
                return;
            }

            _canUndoDesignEdit = value;
            OnPropertyChanged();
        }
    }

    public bool CanRedoDesignEdit
    {
        get => _canRedoDesignEdit;
        private set
        {
            if (_canRedoDesignEdit == value)
            {
                return;
            }

            _canRedoDesignEdit = value;
            OnPropertyChanged();
        }
    }

    public bool CanUndoAllDesignEdit
    {
        get => _canUndoAllDesignEdit;
        private set
        {
            if (_canUndoAllDesignEdit == value)
            {
                return;
            }

            _canUndoAllDesignEdit = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<DesignEditStepListItem> DesignEditSteps { get; } = [];

    public ICommand ToggleDesignEditModeCommand { get; private set; } = null!;
    public ICommand ToggleCutPlaneModeCommand { get; private set; } = null!;
    public ICommand FlipCutPlaneSideCommand { get; private set; } = null!;
    public ICommand ApplyCutPlaneCommand { get; private set; } = null!;
    public ICommand UndoDesignEditCommand { get; private set; } = null!;
    public ICommand RedoDesignEditCommand { get; private set; } = null!;
    public ICommand UndoAllDesignEditCommand { get; private set; } = null!;
    public ICommand SaveDesignEditStlCommand { get; private set; } = null!;

    public event EventHandler? DesignEditCutPlaneVisualsChanged;

    private void InitDesignEditCommands()
    {
        ToggleDesignEditModeCommand = new RelayCommand(ToggleDesignEditMode);
        ToggleCutPlaneModeCommand = new RelayCommand(ToggleCutPlaneMode, () => IsDesignEditMode && !IsBusy);
        FlipCutPlaneSideCommand = new RelayCommand(FlipCutPlaneSide, () => IsDesignEditMode && HasCutPlaneReady);
        ApplyCutPlaneCommand = new RelayCommand(ApplyCutPlane, () => IsDesignEditMode && HasCutPlaneReady && !IsBusy);
        UndoDesignEditCommand = new RelayCommand(UndoDesignEdit, () => CanUndoDesignEdit);
        RedoDesignEditCommand = new RelayCommand(RedoDesignEdit, () => CanRedoDesignEdit);
        UndoAllDesignEditCommand = new RelayCommand(UndoAllDesignEdit, () => CanUndoAllDesignEdit);
        SaveDesignEditStlCommand = new RelayCommand(SaveDesignEditStl, () => IsDesignEditMode && HasEditableRestorations && !IsBusy);
    }

    private bool HasEditableRestorations =>
        _loadedFiles.Any(item => !item.IsLoadFailed && IsDesignEditableMesh(item));

    internal void ConfigureDesignEditOrderFolder(string? orderFolderPath)
    {
        _designEditOrderFolderPath = string.IsNullOrWhiteSpace(orderFolderPath)
            ? null
            : Path.GetFullPath(orderFolderPath);
        ReloadDesignEditStepStore();
    }

    internal void ReloadDesignEditStepStore()
    {
        _designEditStepStore = string.IsNullOrWhiteSpace(_designEditOrderFolderPath)
            ? null
            : StatsDesignEditStepStore.Open(_designEditOrderFolderPath);
    }

    private void EnsureDesignEditStepStore()
    {
        if (_designEditStepStore is not null || string.IsNullOrWhiteSpace(_designEditOrderFolderPath))
        {
            return;
        }

        ReloadDesignEditStepStore();
    }

    private string GetDesignEditMeshKey(string meshFullPath)
    {
        if (string.IsNullOrWhiteSpace(_designEditOrderFolderPath))
        {
            return Path.GetFullPath(meshFullPath);
        }

        return StatsDesignEditStepStore.GetRelativeMeshKey(_designEditOrderFolderPath, meshFullPath);
    }

    internal void ResetDesignEditSessionCache()
    {
        _designEditOriginalMeshes.Clear();
    }

    internal void CaptureDesignEditOriginals()
    {
        if (string.IsNullOrWhiteSpace(_designEditOrderFolderPath))
        {
            return;
        }

        foreach (var item in _loadedFiles.Where(IsDesignEditableMesh))
        {
            var snapshot = item.MeshSnapshot;
            if (snapshot is null)
            {
                continue;
            }

            var key = GetDesignEditMeshKey(item.FilePath);
            if (!_designEditOriginalMeshes.ContainsKey(key))
            {
                _designEditOriginalMeshes[key] = snapshot;
                _designEditStepStore?.EnsureOriginalFromMesh(_designEditOrderFolderPath, item.FilePath, snapshot);
            }
        }
    }

    internal void RestoreRestorationsToOriginalsIfNeeded()
    {
        if (IsDesignEditMode)
        {
            return;
        }

        RestoreRestorationsToOriginals();
    }

    private void ToggleDesignEditMode()
    {
        if (IsDesignEditMode)
        {
            ExitDesignEditMode();
        }
        else
        {
            EnterDesignEditMode();
        }
    }

    internal void EnterDesignEditMode()
    {
        if (IsDesignHostMode || string.IsNullOrWhiteSpace(_designEditOrderFolderPath))
        {
            return;
        }

        ReloadDesignEditStepStore();
        CaptureDesignEditOriginals();
        RestoreRestorationsToOriginals();
        var replayedSteps = ReplayDesignEditSteps();

        IsDesignEditMode = true;
        IsCutPlaneMode = false;
        ClearCutPlaneState();

        if (!IsSculptMode)
        {
            IsSculptMode = true;
        }

        if (IsSectionMode)
        {
            IsSectionMode = false;
            ClearSectionMeasurement();
        }

        SetTransientStatus(replayedSteps > 0
            ? $"Design edit — restored {replayedSteps} saved step(s). Sculpt affects restorations only."
            : "Design edit — sculpt affects restorations only. Section mode still works on scans.");
    }

    internal void ExitDesignEditMode()
    {
        IsDesignEditMode = false;
        IsCutPlaneMode = false;
        ClearCutPlaneState();

        if (IsSculptMode)
        {
            IsSculptMode = false;
        }

        RestoreRestorationsToOriginals();
        _designEditUndoStack.Clear();
        _designEditRedoStack.Clear();
        RefreshDesignEditStepList();
        UpdateDesignEditUndoCommands();
        SetTransientStatus("Design edit closed — showing original 3Shape design.");
    }

    private void ToggleCutPlaneMode()
    {
        if (!IsDesignEditMode)
        {
            return;
        }

        IsCutPlaneMode = !IsCutPlaneMode;
        if (!IsCutPlaneMode)
        {
            ClearCutPlaneState();
            NotifyDesignEditCutPlaneVisualsChanged();
            SetTransientStatus("Cut plane tool off.");
            return;
        }

        if (IsSectionMode)
        {
            IsSectionMode = false;
            ClearSectionMeasurement();
        }

        if (IsSculptMode)
        {
            IsSculptMode = false;
        }

        SetTransientStatus("Cut plane — click a restoration to place the plane, choose side, then Apply.");
    }

    private void FlipCutPlaneSide()
    {
        CutPlaneRemovePositiveSide = !CutPlaneRemovePositiveSide;
    }

    internal bool TryPlaceCutPlane(Point3D hitPoint, Vector3D planeNormal, LoadedMeshItemViewModel target)
    {
        if (!IsDesignEditMode || !IsCutPlaneMode || !IsDesignEditableMesh(target))
        {
            return false;
        }

        _cutPlaneTarget = target;
        _cutPlanePoint = hitPoint;
        _cutPlaneNormal = planeNormal;
        if (_cutPlaneNormal.LengthSquared < 1e-9)
        {
            _cutPlaneNormal = new Vector3D(0, 0, 1);
        }
        else
        {
            _cutPlaneNormal.Normalize();
        }

        RefreshCutPlanePreview();
        OnPropertyChanged(nameof(HasCutPlaneReady));
        RaiseDesignEditCommandStates();
        SetTransientStatus("Cut plane placed — red shows the side that will be removed.");
        return true;
    }

    internal void UpdateActiveCutPlane(Point3D planePoint, Vector3D planeNormal)
    {
        if (_cutPlaneTarget is null || !IsCutPlaneMode)
        {
            return;
        }

        _cutPlanePoint = planePoint;
        _cutPlaneNormal = planeNormal;
        RefreshCutPlanePreview();
    }

    internal void NotifyCutPlaneGeometryChanged()
    {
        if (_cutPlaneTarget is null || !IsCutPlaneMode)
        {
            return;
        }

        RefreshCutPlanePreview();
    }

    private void RefreshCutPlanePreview()
    {
        var hadPreview = _hasCutPlanePreview;

        if (_cutPlaneTarget?.MeshSnapshot is null || !IsCutPlaneMode)
        {
            _hasCutPlanePreview = false;
            if (hadPreview)
            {
                OnPropertyChanged(nameof(HasCutPlaneReady));
            }

            RaiseDesignEditCommandStates();
            return;
        }

        var previewColors = StatsDesignEditCutService.BuildCutPreviewColors(
            _cutPlaneTarget.EditableMesh,
            _cutPlanePoint,
            _cutPlaneNormal,
            CutPlaneRemovePositiveSide,
            _cutPlaneTarget.GetMaterialDiffuseColor4());
        _cutPlaneTarget.ApplyCutPlanePreview(previewColors);
        _hasCutPlanePreview = true;
        if (!hadPreview)
        {
            OnPropertyChanged(nameof(HasCutPlaneReady));
        }

        RaiseDesignEditCommandStates();
        RequestVisualRefresh();
    }

    private void ApplyCutPlane()
    {
        if (_cutPlaneTarget?.MeshSnapshot is null || !HasCutPlaneReady || string.IsNullOrWhiteSpace(_designEditOrderFolderPath))
        {
            return;
        }

        try
        {
            EnsureDesignEditStepStore();
            if (_designEditStepStore is null)
            {
                SetTransientStatus("Cut could not be saved — order folder is unavailable.");
                return;
            }

            var target = _cutPlaneTarget!;
            var before = target.MeshSnapshot!;
            var after = StatsDesignEditCutService.CutAndCap(
                before,
                _cutPlanePoint,
                _cutPlaneNormal,
                keepPositiveSide: !CutPlaneRemovePositiveSide);
            ValidateCutResult(after);

            target.ClearCutPlanePreview();
            ClearCutPlaneState();
            target.ReplaceMeshGeometry(after);
            IsCutPlaneMode = false;

            var record = BuildCutStepRecord(target, before, after);
            _designEditStepStore.RecordCutStep(
                _designEditOrderFolderPath,
                target.FilePath,
                before,
                after,
                _cutPlanePoint,
                _cutPlaneNormal,
                CutPlaneRemovePositiveSide);

            PushDesignEditHistory(new DesignEditHistoryEntry
            {
                Type = DesignEditStepType.Cut,
                Target = target,
                Record = record,
                MeshBefore = before,
                MeshAfter = after
            });

            NotifyDesignEditCutPlaneVisualsChanged();
            RequestVisualRefresh();
            SetTransientStatus("Cut applied — restoration sealed with a flat cap.");
        }
        catch (Exception ex)
        {
            SetTransientStatus($"Cut failed: {ex.Message}");
        }
    }

    private DesignEditStepRecord BuildCutStepRecord(
        LoadedMeshItemViewModel target,
        MeshSnapshot before,
        MeshSnapshot after)
    {
        return new DesignEditStepRecord
        {
            Id = _designEditStepStore?.Steps.LastOrDefault()?.Id ?? 0,
            Type = DesignEditStepType.Cut,
            MeshRelativePath = StatsDesignEditStepStore.GetRelativeMeshKey(_designEditOrderFolderPath!, target.FilePath),
            PlanePointX = _cutPlanePoint.X,
            PlanePointY = _cutPlanePoint.Y,
            PlanePointZ = _cutPlanePoint.Z,
            PlaneNormalX = _cutPlaneNormal.X,
            PlaneNormalY = _cutPlaneNormal.Y,
            PlaneNormalZ = _cutPlaneNormal.Z,
            RemovePositiveSide = CutPlaneRemovePositiveSide
        };
    }

    private static void ValidateCutResult(MeshSnapshot mesh)
    {
        if (mesh.Positions.Length < 3 || mesh.TriangleIndices.Length < 3)
        {
            throw new InvalidOperationException("Cut produced an empty mesh.");
        }
    }

    private bool TryApplyCutStep(
        LoadedMeshItemViewModel target,
        DesignEditStepRecord step,
        out MeshSnapshot? before,
        out MeshSnapshot? after)
    {
        before = null;
        after = null;
        if (target.MeshSnapshot is null)
        {
            return false;
        }

        try
        {
            before = target.MeshSnapshot;
            after = StatsDesignEditCutService.CutAndCap(
                before,
                new Point3D(step.PlanePointX, step.PlanePointY, step.PlanePointZ),
                new Vector3D(step.PlaneNormalX, step.PlaneNormalY, step.PlaneNormalZ),
                keepPositiveSide: !step.RemovePositiveSide);
            ValidateCutResult(after);
            target.ReplaceMeshGeometry(after);
            target.ClearVertexColorOverlay();
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal void RecordDesignEditSculptStroke(
        LoadedMeshItemViewModel target,
        Point3D[] beforePositions,
        Point3D[] afterPositions)
    {
        if (!IsDesignEditMode || string.IsNullOrWhiteSpace(_designEditOrderFolderPath))
        {
            return;
        }

        EnsureDesignEditStepStore();
        if (_designEditStepStore is null)
        {
            return;
        }

        _designEditStepStore.RecordSculptStep(
            _designEditOrderFolderPath,
            target.FilePath,
            beforePositions,
            afterPositions,
            CurrentSculptTool,
            SculptBrushRadiusMm,
            SculptBrushStrength);

        var record = _designEditStepStore.Steps.LastOrDefault();
        if (record is null)
        {
            return;
        }

        PushDesignEditHistory(new DesignEditHistoryEntry
        {
            Type = DesignEditStepType.Sculpt,
            Target = target,
            Record = record,
            SculptBefore = beforePositions,
            SculptAfter = afterPositions
        });
    }

    private void PushDesignEditHistory(DesignEditHistoryEntry entry)
    {
        _designEditUndoStack.Push(entry);
        _designEditRedoStack.Clear();

        while (_designEditUndoStack.Count > MaxDesignEditUndoSteps)
        {
            _designEditUndoStack.TryPop(out _);
        }

        RefreshDesignEditStepList();
        UpdateDesignEditUndoCommands();
    }

    public bool TryUndoDesignEdit()
    {
        if (_designEditUndoStack.Count == 0)
        {
            return false;
        }

        UndoDesignEdit();
        return true;
    }

    public bool TryRedoDesignEdit()
    {
        if (_designEditRedoStack.Count == 0)
        {
            return false;
        }

        RedoDesignEdit();
        return true;
    }

    private void UndoDesignEdit()
    {
        if (_designEditUndoStack.Count == 0)
        {
            return;
        }

        var entry = _designEditUndoStack.Pop();
        ApplyDesignEditState(entry.Target, entry.Type, entry.SculptBefore, entry.MeshBefore);
        _designEditRedoStack.Push(entry);
        _designEditStepStore?.TryPopLastStep(out _);
        RefreshDesignEditStepList();
        RequestVisualRefresh();
        UpdateDesignEditUndoCommands();
        SetTransientStatus("Design edit step undone.");
    }

    private void UndoAllDesignEdit()
    {
        if (!IsDesignEditMode || _designEditUndoStack.Count == 0)
        {
            return;
        }

        RestoreRestorationsToOriginals();
        _designEditStepStore?.ClearAll();
        _designEditUndoStack.Clear();
        _designEditRedoStack.Clear();
        RefreshDesignEditStepList();
        UpdateDesignEditUndoCommands();
        RequestVisualRefresh();
        SetTransientStatus("All design edit steps removed — showing original restoration.");
    }

    private void RedoDesignEdit()
    {
        if (_designEditRedoStack.Count == 0 || _designEditStepStore is null || string.IsNullOrWhiteSpace(_designEditOrderFolderPath))
        {
            return;
        }

        var entry = _designEditRedoStack.Pop();
        DesignEditHistoryEntry entryToPush = entry;
        if (entry.Type == DesignEditStepType.Cut)
        {
            if (!TryApplyCutStep(entry.Target, entry.Record, out var before, out var after) ||
                before is null ||
                after is null)
            {
                _designEditRedoStack.Push(entry);
                SetTransientStatus("Cut could not be redone.");
                return;
            }

            entryToPush = new DesignEditHistoryEntry
            {
                Type = DesignEditStepType.Cut,
                Target = entry.Target,
                Record = entry.Record,
                MeshBefore = before,
                MeshAfter = after
            };
        }
        else
        {
            ApplyDesignEditState(entry.Target, entry.Type, entry.SculptAfter, entry.MeshAfter);
        }

        RePersistDesignEditStep(entryToPush);
        _designEditUndoStack.Push(entryToPush);
        RefreshDesignEditStepList();
        RequestVisualRefresh();
        UpdateDesignEditUndoCommands();
        SetTransientStatus("Design edit step redone.");
    }

    private void RePersistDesignEditStep(DesignEditHistoryEntry entry)
    {
        if (string.IsNullOrWhiteSpace(_designEditOrderFolderPath))
        {
            return;
        }

        if (entry.Type == DesignEditStepType.Sculpt &&
            entry.SculptBefore is not null &&
            entry.SculptAfter is not null)
        {
            _designEditStepStore?.RecordSculptStep(
                _designEditOrderFolderPath,
                entry.Target.FilePath,
                entry.SculptBefore,
                entry.SculptAfter,
                CurrentSculptTool,
                entry.Record.Radius,
                entry.Record.Strength);
            return;
        }

        if (entry.Type == DesignEditStepType.Cut &&
            entry.MeshBefore is not null &&
            entry.MeshAfter is not null)
        {
            _designEditStepStore.RecordCutStep(
                _designEditOrderFolderPath,
                entry.Target.FilePath,
                entry.MeshBefore,
                entry.MeshAfter,
                new Point3D(entry.Record.PlanePointX, entry.Record.PlanePointY, entry.Record.PlanePointZ),
                new Vector3D(entry.Record.PlaneNormalX, entry.Record.PlaneNormalY, entry.Record.PlaneNormalZ),
                entry.Record.RemovePositiveSide);
        }
    }

    private void ApplyDesignEditState(
        LoadedMeshItemViewModel target,
        DesignEditStepType type,
        Point3D[]? sculptPositions,
        MeshSnapshot? meshSnapshot)
    {
        if (type == DesignEditStepType.Sculpt && sculptPositions is not null)
        {
            target.RestoreSculptPositions(sculptPositions);
            target.ClearVertexColorOverlay();
            return;
        }

        if (type == DesignEditStepType.Cut && meshSnapshot is not null)
        {
            target.ReplaceMeshGeometry(meshSnapshot);
            target.ClearVertexColorOverlay();
        }
    }

    private int ReplayDesignEditSteps()
    {
        RestoreRestorationsToOriginals();
        if (_designEditStepStore is null || !_designEditStepStore.HasSteps)
        {
            _designEditUndoStack.Clear();
            _designEditRedoStack.Clear();
            UpdateDesignEditUndoCommands();
            return 0;
        }

        _designEditUndoStack.Clear();
        _designEditRedoStack.Clear();
        var appliedCount = 0;

        foreach (var step in _designEditStepStore.Steps)
        {
            var target = FindDesignEditMesh(step.MeshRelativePath);
            if (target is null)
            {
                continue;
            }

            if (step.Type == DesignEditStepType.Sculpt)
            {
                var after = _designEditStepStore.ReadSculptAfter(step);
                var before = _designEditStepStore.ReadSculptBefore(step);
                if (after is null || before is null)
                {
                    continue;
                }

                if (target.MeshSnapshot is null || after.Length != target.MeshSnapshot.Positions.Length)
                {
                    continue;
                }

                target.RestoreSculptPositions(after);
                _designEditUndoStack.Push(new DesignEditHistoryEntry
                {
                    Type = DesignEditStepType.Sculpt,
                    Target = target,
                    Record = step,
                    SculptBefore = before,
                    SculptAfter = after
                });
                appliedCount++;
            }
            else if (step.Type == DesignEditStepType.Cut)
            {
                if (!TryApplyCutStep(target, step, out var before, out var after) ||
                    before is null ||
                    after is null)
                {
                    continue;
                }

                _designEditUndoStack.Push(new DesignEditHistoryEntry
                {
                    Type = DesignEditStepType.Cut,
                    Target = target,
                    Record = step,
                    MeshBefore = before,
                    MeshAfter = after
                });
                appliedCount++;
            }
        }

        RefreshDesignEditStepList();
        RequestVisualRefresh();
        UpdateDesignEditUndoCommands();
        return appliedCount;
    }

    private void RefreshDesignEditStepList()
    {
        DesignEditSteps.Clear();
        if (_designEditStepStore is null)
        {
            return;
        }

        var stepNumber = 1;
        foreach (var step in _designEditStepStore.Steps)
        {
            DesignEditSteps.Add(CreateDesignEditStepListItem(step, stepNumber++));
        }
    }

    private static DesignEditStepListItem CreateDesignEditStepListItem(DesignEditStepRecord step, int stepNumber)
    {
        var meshName = Path.GetFileName(step.MeshRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(meshName))
        {
            meshName = step.MeshRelativePath;
        }

        if (step.Type == DesignEditStepType.Cut)
        {
            return new DesignEditStepListItem
            {
                StepNumber = stepNumber,
                Title = $"Cut plane — {meshName}",
                Detail = "Plane cut with flat cap"
            };
        }

        var tool = string.IsNullOrWhiteSpace(step.Tool) ? "Sculpt" : step.Tool;
        return new DesignEditStepListItem
        {
            StepNumber = stepNumber,
            Title = $"Sculpt {tool} — {meshName}",
            Detail = $"Radius {step.Radius:0.00} mm · strength {step.Strength:0.00}"
        };
    }

    private void RestoreRestorationsToOriginals()
    {
        foreach (var item in _loadedFiles.Where(IsDesignEditableMesh))
        {
            var key = GetDesignEditMeshKey(item.FilePath);
            if (_designEditOriginalMeshes.TryGetValue(key, out var original))
            {
                item.ReplaceMeshGeometry(original);
                item.ClearVertexColorOverlay();
                continue;
            }

            if (_designEditStepStore is not null &&
                string.IsNullOrWhiteSpace(_designEditOrderFolderPath) == false &&
                _designEditStepStore.TryGetOriginalMesh(key, out var storedOriginal) &&
                storedOriginal is not null)
            {
                item.ReplaceMeshGeometry(storedOriginal);
                item.ClearVertexColorOverlay();
            }
        }

        RequestVisualRefresh();
    }

    private LoadedMeshItemViewModel? FindDesignEditMesh(string meshRelativePath)
    {
        if (string.IsNullOrWhiteSpace(meshRelativePath))
        {
            return null;
        }

        var normalizedStored = StatsDesignEditStepStore.NormalizeMeshRelativeKey(meshRelativePath);
        var editableMeshes = _loadedFiles.Where(IsDesignEditableMesh).ToList();

        var exact = editableMeshes.FirstOrDefault(item =>
            string.Equals(GetDesignEditMeshKey(item.FilePath), normalizedStored, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(GetDesignEditMeshKey(item.FilePath), meshRelativePath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFullPath(item.FilePath), Path.GetFullPath(meshRelativePath), StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var fileName = Path.GetFileName(normalizedStored);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var fileNameMatches = editableMeshes
            .Where(item => string.Equals(Path.GetFileName(item.FilePath), fileName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return fileNameMatches.Count == 1 ? fileNameMatches[0] : null;
    }

    internal bool IsDesignEditableMesh(LoadedMeshItemViewModel item) =>
        !item.IsLoadFailed &&
        item.Category is MeshCategory.Restoration or MeshCategory.Abutment;

    internal bool CanSculptInDesignEdit(LoadedMeshItemViewModel item) =>
        IsDesignEditMode && IsDesignEditableMesh(item);

    private void NotifyDesignEditCutPlaneVisualsChanged() =>
        DesignEditCutPlaneVisualsChanged?.Invoke(this, EventArgs.Empty);

    internal bool TryGetActiveCutPlaneTarget([NotNullWhen(true)] out LoadedMeshItemViewModel? target)
    {
        target = _cutPlaneTarget;
        return target is not null && IsCutPlaneMode;
    }

    private void ClearCutPlaneState()
    {
        _cutPlaneTarget?.ClearCutPlanePreview();
        _cutPlaneTarget = null;
        _hasCutPlanePreview = false;
        OnPropertyChanged(nameof(HasCutPlaneReady));
        RaiseDesignEditCommandStates();
    }

    private void SaveDesignEditStl()
    {
        if (string.IsNullOrWhiteSpace(_designEditOrderFolderPath))
        {
            return;
        }

        var targets = _loadedFiles.Where(item => !item.IsLoadFailed && IsDesignEditableMesh(item)).ToList();
        if (targets.Count == 0)
        {
            SetTransientStatus("No restoration mesh is loaded to export.");
            return;
        }

        try
        {
            Directory.CreateDirectory(StatsDesignEditPaths.GetExportsFolder(_designEditOrderFolderPath));
            var saved = 0;
            foreach (var target in targets)
            {
                var snapshot = target.MeshSnapshot;
                if (snapshot is null)
                {
                    continue;
                }

                var outputPath = StatsDesignEditPaths.BuildExportStlPath(_designEditOrderFolderPath, target.FilePath);
                MeshExportService.Export(outputPath, [snapshot]);
                saved++;
            }

            SetTransientStatus(saved == 1
                ? $"Saved edited restoration STL to StatsDesignEdit/Exports (DCM untouched)."
                : $"Saved {saved} edited restoration STL files to StatsDesignEdit/Exports (DCM untouched).");
        }
        catch (Exception ex)
        {
            SetTransientStatus($"STL export failed: {ex.Message}");
        }
    }

    private void UpdateDesignEditUndoCommands()
    {
        CanUndoDesignEdit = IsDesignEditMode && _designEditUndoStack.Count > 0;
        CanRedoDesignEdit = IsDesignEditMode && _designEditRedoStack.Count > 0;
        CanUndoAllDesignEdit = IsDesignEditMode && _designEditUndoStack.Count > 0;
        UpdateSculptUndoCommands();
        RaiseDesignEditCommandStates();
    }

    private void RaiseDesignEditCommandStates()
    {
        if (ToggleCutPlaneModeCommand is RelayCommand cutToggle)
        {
            cutToggle.RaiseCanExecuteChanged();
        }

        if (FlipCutPlaneSideCommand is RelayCommand flipSide)
        {
            flipSide.RaiseCanExecuteChanged();
        }

        if (ApplyCutPlaneCommand is RelayCommand applyCut)
        {
            applyCut.RaiseCanExecuteChanged();
        }

        if (UndoDesignEditCommand is RelayCommand undo)
        {
            undo.RaiseCanExecuteChanged();
        }

        if (RedoDesignEditCommand is RelayCommand redo)
        {
            redo.RaiseCanExecuteChanged();
        }

        if (UndoAllDesignEditCommand is RelayCommand undoAll)
        {
            undoAll.RaiseCanExecuteChanged();
        }

        if (SaveDesignEditStlCommand is RelayCommand saveStl)
        {
            saveStl.RaiseCanExecuteChanged();
        }
    }
}
