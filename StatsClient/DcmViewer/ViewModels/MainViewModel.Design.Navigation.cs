using DCMViewer.Infrastructure;
using DCMViewer.Services;
using StatsClient.MVVM.Core;

namespace DCMViewer.ViewModels;

public partial class MainViewModel
{
    public string LibraryCatalogStatus { get; private set; } = string.Empty;

    public Infrastructure.RelayCommand DesignWorkflowBackCommand { get; private set; } = null!;
    public Infrastructure.RelayCommand DesignWorkflowNextCommand { get; private set; } = null!;

    private void InitDesignNavigationCommands()
    {
        DesignWorkflowBackCommand = new Infrastructure.RelayCommand(
            () => NavigateDesignWorkflow(-1),
            () => !IsBusy && IsDesignHostMode && StatsDesignWorkflowCoordinator.Previous(
                StatsDesignWorkflowCoordinator.Parse(DesignWorkflowStep)) is not null);
        DesignWorkflowNextCommand = new Infrastructure.RelayCommand(
            () => NavigateDesignWorkflow(+1),
            () => !IsBusy && IsDesignHostMode && StatsDesignWorkflowCoordinator.Next(
                StatsDesignWorkflowCoordinator.Parse(DesignWorkflowStep)) is not null);
    }

    private void NavigateDesignWorkflow(int direction)
    {
        var current = StatsDesignWorkflowCoordinator.Parse(DesignWorkflowStep);
        var target = direction < 0
            ? StatsDesignWorkflowCoordinator.Previous(current)
            : StatsDesignWorkflowCoordinator.Next(current);
        if (target is null)
        {
            return;
        }

        if (direction > 0 && !TryValidateWorkflowAdvance(current, target.Value, out var message))
        {
            SetTransientStatus(message);
            return;
        }

        DesignWorkflowStep = StatsDesignWorkflowCoordinator.ToStepName(target.Value);
    }

    private bool TryValidateWorkflowAdvance(
        StatsDesignWorkflowPhase from,
        StatsDesignWorkflowPhase to,
        out string message)
    {
        message = string.Empty;
        if (from == StatsDesignWorkflowPhase.Margin && to == StatsDesignWorkflowPhase.Inner)
        {
            if (!IsMarginClosed || MarginPointCount < 3)
            {
                message = "Close the margin loop before generating the inner shell.";
                return false;
            }
        }

        if (from == StatsDesignWorkflowPhase.Inner && to == StatsDesignWorkflowPhase.Shape)
        {
            if (!HasInnerShell)
            {
                message = "Generate the inner shell first, or go back to Margin if you are still defining the margin.";
                return false;
            }
        }

        if (from == StatsDesignWorkflowPhase.Shape && to == StatsDesignWorkflowPhase.Sculpt)
        {
            if (!HasClosedCrown)
            {
                message = "Load a library tooth and use Connect to margin line to create the crown.";
                return false;
            }
        }

        if (from == StatsDesignWorkflowPhase.Sculpt && to == StatsDesignWorkflowPhase.Export)
        {
            if (!HasClosedCrown)
            {
                message = "Complete sculpting and connect the crown before export.";
                return false;
            }
        }

        return true;
    }

    private void RaiseDesignNavigationCommandStates()
    {
        DesignWorkflowBackCommand?.RaiseCanExecuteChanged();
        DesignWorkflowNextCommand?.RaiseCanExecuteChanged();
    }

    private void OnDesignWorkflowStepEntered(StatsDesignWorkflowPhase phase)
    {
        switch (phase)
        {
            case StatsDesignWorkflowPhase.Inner:
                ApplyDesignUiDefaults();
                _ = EnsureInnerShellLoadedAsync();
                break;
            case StatsDesignWorkflowPhase.Shape:
                ApplyDesignUiDefaults();
                RefreshLibraryToothCatalog();
                StatsDesignDefaults.ApplyLibraryPlacementDefaults(DesignManifest);
                OnPropertyChanged(nameof(LibraryOffsetXmm));
                OnPropertyChanged(nameof(LibraryOffsetYmm));
                OnPropertyChanged(nameof(LibraryOffsetZmm));
                OnPropertyChanged(nameof(LibraryScaleX));
                OnPropertyChanged(nameof(LibraryScaleY));
                OnPropertyChanged(nameof(LibraryScaleZ));
                OnPropertyChanged(nameof(LibraryUniformScale));
                break;
        }
    }

    private async Task EnsureInnerShellLoadedAsync()
    {
        if (!HasInnerShell || string.IsNullOrWhiteSpace(_innerShellPath))
        {
            return;
        }

        var alreadyLoaded = _loadedFiles.Any(f =>
            string.Equals(f.FilePath, _innerShellPath, StringComparison.OrdinalIgnoreCase));
        if (alreadyLoaded)
        {
            return;
        }

        try
        {
            await LoadDesignMeshAsync(_innerShellPath, DesignMeshRole.InnerShell);
        }
        catch (Exception ex)
        {
            SetTransientStatus($"Could not show inner shell: {ex.Message}");
        }
    }
}