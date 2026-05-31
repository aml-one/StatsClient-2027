using DCMViewer.Infrastructure;
using DCMViewer.Services;
using StatsClient.MVVM.View;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using static StatsClient.MVVM.Core.Enums;
using NotificationIcon = StatsClient.MVVM.ViewModel.MainViewModel.NotificationIcon;

namespace DCMViewer.ViewModels;

public partial class MainViewModel
{
    private const int MaxSculptUndoSteps = 50;

    private sealed class SculptHistoryEntry
    {
        public required LoadedMeshItemViewModel Target { get; init; }
        public required Point3D[] Before { get; init; }
        public required Point3D[] After { get; init; }
        public required SculptBrushTool Tool { get; init; }
        public required double Radius { get; init; }
        public required double Strength { get; init; }
    }

    private bool _isSculptMode;
    private string _sculptToolName = nameof(SculptBrushTool.Grab);
    private double _sculptBrushRadiusMm = 2.0;
    private double _sculptBrushStrength = 0.35;
    private bool _canUndoSculpt;
    private bool _canUndoAllSculpt;
    private bool _canRedoSculpt;
    private string? _sculptOrderFolderPath;
    private SculptTreeStore? _sculptTree;
    private readonly Stack<SculptHistoryEntry> _sculptUndoStack = new();
    private readonly Stack<SculptHistoryEntry> _sculptRedoStack = new();
    private LoadedMeshItemViewModel? _sculptStrokeTarget;
    private Point3D[]? _sculptStrokeStartPositions;
    private bool _sculptStrokeHadChanges;
    private bool _sculptPendingGeometryRefresh;
    private long _sculptLastGeometryRefreshTicks;
    private const long SculptGeometryRefreshMinIntervalTicks = 33 * TimeSpan.TicksPerMillisecond;

    public bool IsSculptMode
    {
        get => _isSculptMode;
        private set
        {
            if (_isSculptMode == value)
            {
                return;
            }

            _isSculptMode = value;
            OnPropertyChanged();
        }
    }

    public string SculptToolName
    {
        get => _sculptToolName;
        set
        {
            var normalized = value ?? nameof(SculptBrushTool.Grab);
            if (string.Equals(_sculptToolName, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _sculptToolName = normalized;
            OnPropertyChanged();
        }
    }

    public double SculptBrushRadiusMm
    {
        get => _sculptBrushRadiusMm;
        set
        {
            var clamped = Math.Clamp(value, 0.20, 3.0);
            if (Math.Abs(_sculptBrushRadiusMm - clamped) < 1e-6)
            {
                return;
            }

            _sculptBrushRadiusMm = clamped;
            OnPropertyChanged();
        }
    }

    public double SculptBrushStrength
    {
        get => _sculptBrushStrength;
        set
        {
            var clamped = Math.Clamp(value, 0.05, 1.0);
            if (Math.Abs(_sculptBrushStrength - clamped) < 1e-6)
            {
                return;
            }

            _sculptBrushStrength = clamped;
            OnPropertyChanged();
        }
    }

    public ICommand ToggleSculptModeCommand { get; private set; } = null!;
    public ICommand SelectSculptToolCommand { get; private set; } = null!;
    public ICommand UndoSculptCommand { get; private set; } = null!;
    public ICommand UndoAllSculptCommand { get; private set; } = null!;
    public ICommand RedoSculptCommand { get; private set; } = null!;
    public ICommand SaveSculptedDcmCommand { get; private set; } = null!;

    public bool CanSaveSculptedDcm =>
        !IsBusy && _loadedFiles.Any(item => item.CanSaveSculptedDcm);

    public bool CanUndoSculpt
    {
        get => _canUndoSculpt;
        private set
        {
            if (_canUndoSculpt == value)
            {
                return;
            }

            _canUndoSculpt = value;
            OnPropertyChanged();
        }
    }

    public bool CanUndoAllSculpt
    {
        get => _canUndoAllSculpt;
        private set
        {
            if (_canUndoAllSculpt == value)
            {
                return;
            }

            _canUndoAllSculpt = value;
            OnPropertyChanged();
        }
    }

    public bool CanRedoSculpt
    {
        get => _canRedoSculpt;
        private set
        {
            if (_canRedoSculpt == value)
            {
                return;
            }

            _canRedoSculpt = value;
            OnPropertyChanged();
        }
    }

    private void InitSculptCommands()
    {
        ToggleSculptModeCommand = new RelayCommand(ToggleSculptMode);
        SelectSculptToolCommand = new RelayCommand<string>(SelectSculptTool);
        UndoSculptCommand = new RelayCommand(UndoSculpt, () => CanUndoSculpt);
        UndoAllSculptCommand = new RelayCommand(UndoAllSculpt, () => CanUndoAllSculpt);
        RedoSculptCommand = new RelayCommand(RedoSculpt, () => CanRedoSculpt);
        SaveSculptedDcmCommand = new RelayCommand(SaveSculptedDcm, () => CanSaveSculptedDcm);
    }

    internal void SetSculptOrderFolder(string? orderFolderPath)
    {
        _sculptOrderFolderPath = string.IsNullOrWhiteSpace(orderFolderPath) ? null : Path.GetFullPath(orderFolderPath);
        _sculptTree = SculptTreeStore.Open(_sculptOrderFolderPath);
    }

    internal void ApplyPersistedSculptTree()
    {
        if (_sculptTree is null || !_sculptTree.HasSteps)
        {
            _sculptUndoStack.Clear();
            _sculptRedoStack.Clear();
            UpdateSculptUndoCommands();
            CaptureAllSculptBaselines();
            return;
        }

        foreach (var (meshRelativePath, afterPositions) in _sculptTree.GetLatestAfterPositionsByMesh())
        {
            var target = FindLoadedFileByMeshKey(meshRelativePath);
            target?.RestoreSculptPositions(afterPositions);
        }

        _sculptUndoStack.Clear();
        _sculptRedoStack.Clear();
        foreach (var step in _sculptTree.Steps)
        {
            var target = FindLoadedFileByMeshKey(step.MeshRelativePath);
            var before = _sculptTree.ReadStepBefore(step);
            var after = _sculptTree.ReadStepAfter(step);
            if (target is null || before is null || after is null)
            {
                continue;
            }

            if (!Enum.TryParse(step.Tool, out SculptBrushTool tool))
            {
                tool = SculptBrushTool.Grab;
            }

            _sculptUndoStack.Push(new SculptHistoryEntry
            {
                Target = target,
                Before = before,
                After = after,
                Tool = tool,
                Radius = step.Radius,
                Strength = step.Strength
            });
        }

        RequestVisualRefresh();
        UpdateSculptUndoCommands();
        CaptureAllSculptBaselines();
    }

    internal void CaptureAllSculptBaselines()
    {
        foreach (var item in _loadedFiles)
        {
            if (!item.IsLoadFailed)
            {
                item.CaptureSculptBaseline();
            }
        }

        UpdateSculptSaveCommand();
    }

    private void UpdateSculptSaveCommand()
    {
        OnPropertyChanged(nameof(CanSaveSculptedDcm));
        if (SaveSculptedDcmCommand is RelayCommand relay)
        {
            relay.RaiseCanExecuteChanged();
        }
    }

    private void SelectSculptTool(string? toolName)
    {
        SculptToolName = string.IsNullOrWhiteSpace(toolName)
            ? nameof(SculptBrushTool.Grab)
            : toolName;
    }

    private void ToggleSculptMode()
    {
        IsSculptMode = !IsSculptMode;
        if (IsSculptMode)
        {
            if (IsSectionMode)
            {
                IsSectionMode = false;
                ClearSectionMeasurement();
            }

            SetTransientStatus("Sculpt mode — drag on a visible mesh. Changes stay in memory until export; strokes are logged to StatsSculpTree.");
        }
        else
        {
            UpdateDefaultStatusText();
        }
    }

    internal SculptBrushTool CurrentSculptTool =>
        Enum.TryParse<SculptBrushTool>(_sculptToolName, out var tool) ? tool : SculptBrushTool.Grab;

    internal void BeginSculptStroke(LoadedMeshItemViewModel target)
    {
        _sculptStrokeTarget = target;
        _sculptStrokeStartPositions = target.CaptureSculptPositions();
        _sculptStrokeHadChanges = false;
        _sculptPendingGeometryRefresh = false;
        _sculptLastGeometryRefreshTicks = 0;
    }

    internal void CommitSculptStroke()
    {
        if (_sculptPendingGeometryRefresh && _sculptStrokeTarget is not null)
        {
            _sculptStrokeTarget.RefreshGeometryAfterSculpt();
            RequestVisualRefresh();
            _sculptPendingGeometryRefresh = false;
        }

        if (_sculptStrokeTarget is not null &&
            _sculptStrokeStartPositions is not null &&
            _sculptStrokeHadChanges)
        {
            var afterPositions = _sculptStrokeTarget.CaptureSculptPositions();
            _sculptUndoStack.Push(new SculptHistoryEntry
            {
                Target = _sculptStrokeTarget,
                Before = _sculptStrokeStartPositions,
                After = afterPositions,
                Tool = CurrentSculptTool,
                Radius = SculptBrushRadiusMm,
                Strength = SculptBrushStrength
            });
            _sculptRedoStack.Clear();
            PersistSculptStroke(_sculptStrokeTarget, _sculptStrokeStartPositions, afterPositions);

            while (_sculptUndoStack.Count > MaxSculptUndoSteps)
            {
                TrimOldestSculptUndoEntry();
            }
        }

        _sculptStrokeTarget = null;
        _sculptStrokeStartPositions = null;
        _sculptStrokeHadChanges = false;
        UpdateSculptUndoCommands();
    }

    public bool TryUndoSculpt()
    {
        if (_sculptUndoStack.Count == 0)
        {
            return false;
        }

        UndoSculpt();
        return true;
    }

    public bool TryRedoSculpt()
    {
        if (_sculptRedoStack.Count == 0)
        {
            return false;
        }

        RedoSculpt();
        return true;
    }

    private void UndoSculpt()
    {
        if (_sculptUndoStack.Count == 0)
        {
            return;
        }

        var entry = _sculptUndoStack.Pop();
        entry.Target.RestoreSculptPositions(entry.Before);
        _sculptRedoStack.Push(entry);
        _sculptTree?.TryPopLastStep(out _);
        RequestVisualRefresh();
        UpdateSculptUndoCommands();
        UpdateSculptSaveCommand();
        SetTransientStatus("Sculpt stroke undone.");
    }

    private void RedoSculpt()
    {
        if (_sculptRedoStack.Count == 0)
        {
            return;
        }

        var entry = _sculptRedoStack.Pop();
        entry.Target.RestoreSculptPositions(entry.After);
        _sculptUndoStack.Push(entry);
        PersistSculptStroke(entry.Target, entry.Before, entry.After);
        RequestVisualRefresh();
        UpdateSculptUndoCommands();
        UpdateSculptSaveCommand();
        SetTransientStatus("Sculpt stroke redone.");
    }

    private void UndoAllSculpt()
    {
        if (_sculptTree is null || !_sculptTree.HasSteps)
        {
            return;
        }

        foreach (var meshKey in _sculptTree.GetAffectedMeshKeys())
        {
            var target = FindLoadedFileByMeshKey(meshKey);
            if (target is null)
            {
                continue;
            }

            if (!_sculptTree.TryGetOriginalPositions(meshKey, out var original) &&
                !_sculptTree.TryGetFirstBeforePositions(meshKey, out original))
            {
                continue;
            }

            if (original is not null)
            {
                target.RestoreSculptPositions(original);
            }
        }

        _sculptTree.ClearAll();
        _sculptUndoStack.Clear();
        _sculptRedoStack.Clear();
        RequestVisualRefresh();
        UpdateSculptUndoCommands();
        UpdateSculptSaveCommand();
        SetTransientStatus("All sculpt changes undone (StatsSculpTree cleared).");
    }

    private async void SaveSculptedDcm()
    {
        var targets = _loadedFiles.Where(item => item.CanSaveSculptedDcm).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("This will permanently overwrite the original DCM scan file(s) on disk:");
        builder.AppendLine();
        foreach (var target in targets)
        {
            builder.Append("• ");
            builder.AppendLine(target.DisplayName);
        }

        builder.AppendLine();
        builder.AppendLine("A backup copy (.dcm.bak) is created beside each file first.");
        builder.AppendLine();
        builder.Append("Are you sure you want to save these changes?");

        if (!ConfirmSculptSave(builder.ToString()))
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = "Saving sculpted DCM...";

            await Task.Run(() =>
            {
                foreach (var target in targets)
                {
                    var snapshot = target.MeshSnapshot;
                    if (snapshot is null || target.WriteProfile is null)
                    {
                        continue;
                    }

                    DcmMeshWriter.SaveVerticesToDcm(target.FilePath, snapshot.Positions, target.WriteProfile);
                }
            });

            foreach (var target in targets)
            {
                target.MarkSculptSaved();
            }

            SetTransientStatus($"Saved {targets.Count} sculpted DCM file(s). Backup: .dcm.bak");
        }
        catch (Exception ex)
        {
            SetTransientStatus($"DCM save failed: {ex.Message}");
            ShowSculptMessage("Save sculpted scan to DCM", ex.Message, NotificationIcon.Error);
        }
        finally
        {
            IsBusy = false;
            UpdateSculptSaveCommand();
            UpdateDefaultStatusText();
        }
    }

    private void PersistSculptStroke(
        LoadedMeshItemViewModel target,
        Point3D[] beforePositions,
        Point3D[] afterPositions)
    {
        if (_sculptTree is null || string.IsNullOrWhiteSpace(_sculptOrderFolderPath))
        {
            return;
        }

        try
        {
            _sculptTree.RecordStep(
                _sculptOrderFolderPath,
                target.FilePath,
                beforePositions,
                afterPositions,
                CurrentSculptTool,
                SculptBrushRadiusMm,
                SculptBrushStrength);
        }
        catch
        {
            SetTransientStatus("Sculpt stroke kept in memory only — could not write StatsSculpTree.");
        }
    }

    private void TrimOldestSculptUndoEntry()
    {
        if (_sculptUndoStack.Count == 0)
        {
            return;
        }

        var entries = _sculptUndoStack.ToArray();
        _sculptUndoStack.Clear();
        for (var index = entries.Length - 2; index >= 0; index--)
        {
            _sculptUndoStack.Push(entries[index]);
        }

        _sculptTree?.TrimOldestStep();
    }

    private void UpdateSculptUndoCommands()
    {
        CanUndoSculpt = _sculptUndoStack.Count > 0;
        CanUndoAllSculpt = _sculptTree?.HasSteps == true;
        CanRedoSculpt = _sculptRedoStack.Count > 0;

        if (UndoSculptCommand is RelayCommand undoRelay)
        {
            undoRelay.RaiseCanExecuteChanged();
        }

        if (UndoAllSculptCommand is RelayCommand undoAllRelay)
        {
            undoAllRelay.RaiseCanExecuteChanged();
        }

        if (RedoSculptCommand is RelayCommand redoRelay)
        {
            redoRelay.RaiseCanExecuteChanged();
        }
    }

    private static bool ConfirmSculptSave(string message)
    {
        var owner = GetDialogOwner();
        var dialog = new SMessageBox(
            "Save sculpted scan to DCM",
            message,
            SMessageBoxButtons.YesNo,
            NotificationIcon.Question,
            60,
            owner);
        if (owner is not null)
        {
            dialog.Owner = owner;
        }

        dialog.ShowDialog();
        return dialog.SMessageBoxxResult == SMessageBoxResult.Yes;
    }

    private static void ShowSculptMessage(string title, string message, NotificationIcon icon)
    {
        var owner = GetDialogOwner();
        var dialog = new SMessageBox(
            title,
            message,
            SMessageBoxButtons.Ok,
            icon,
            30,
            owner);
        if (owner is not null)
        {
            dialog.Owner = owner;
        }

        dialog.ShowDialog();
    }

    internal bool TryApplySculptStroke(
        LoadedMeshItemViewModel target,
        Point3D center,
        Vector3D surfaceNormal,
        Vector3D? grabDelta)
    {
        if (!IsSculptMode || target.IsLoadFailed)
        {
            return false;
        }

        var changed = target.TryApplySculptStroke(
            CurrentSculptTool,
            center,
            surfaceNormal,
            SculptBrushRadiusMm,
            SculptBrushStrength,
            grabDelta);

        if (changed)
        {
            _sculptStrokeHadChanges = true;
            RefreshSculptGeometryThrottled(target);
            RequestVisualRefresh();
            UpdateSculptSaveCommand();
        }

        return changed;
    }

    private void RefreshSculptGeometryThrottled(LoadedMeshItemViewModel target, bool force = false)
    {
        if (force)
        {
            target.RefreshGeometryAfterSculpt();
            _sculptPendingGeometryRefresh = false;
            _sculptLastGeometryRefreshTicks = DateTime.UtcNow.Ticks;
            return;
        }

        var now = DateTime.UtcNow.Ticks;
        if (now - _sculptLastGeometryRefreshTicks >= SculptGeometryRefreshMinIntervalTicks)
        {
            target.RefreshGeometryAfterSculpt();
            _sculptPendingGeometryRefresh = false;
            _sculptLastGeometryRefreshTicks = now;
        }
        else
        {
            _sculptPendingGeometryRefresh = true;
        }
    }

    internal LoadedMeshItemViewModel? FindLoadedFileByModel(object? modelHit)
    {
        if (modelHit is null)
        {
            return null;
        }

        return _loadedFiles.FirstOrDefault(item => ReferenceEquals(item.Model, modelHit));
    }

    private LoadedMeshItemViewModel? FindLoadedFileByMeshKey(string meshRelativePath)
    {
        if (string.IsNullOrWhiteSpace(_sculptOrderFolderPath))
        {
            return _loadedFiles.FirstOrDefault(item =>
                string.Equals(item.FilePath, meshRelativePath, StringComparison.OrdinalIgnoreCase));
        }

        return _loadedFiles.FirstOrDefault(item =>
            string.Equals(
                SculptTreeStore.GetRelativeMeshKey(_sculptOrderFolderPath, item.FilePath),
                meshRelativePath,
                StringComparison.OrdinalIgnoreCase));
    }
}
