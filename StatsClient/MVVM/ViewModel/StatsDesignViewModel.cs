using StatsClient.MVVM.Core;
using StatsClient.MVVM.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace StatsClient.MVVM.ViewModel;

public sealed class StatsDesignViewModel : INotifyPropertyChanged
{
    private ThreeShapeOrdersModel? _order;
    private string _workflowStep = "Margin";
    private bool _isFullscreen;
    private bool _restoreLastDesignOnOpen;

    public StatsDesignViewModel()
    {
        WorkflowSteps = new ObservableCollection<StatsDesignWorkflowStepItem>(
        [
            new() { StepKey = "Margin", Label = "Margin", IconGlyph = "\uE8B7" },
            new() { StepKey = "Inner", Label = "Inner shell", IconGlyph = "\uE8F1" },
            new() { StepKey = "Shape", Label = "Library", IconGlyph = "\uE8A5" },
            new() { StepKey = "Sculpt", Label = "Sculpt", IconGlyph = "\uE70F" },
            new() { StepKey = "Export", Label = "Export", IconGlyph = "\uE74E" }
        ]);

        CloseWindowCommand = new RelayCommand(_ => CloseRequested());
        ToggleFullscreenCommand = new RelayCommand(_ => ToggleFullscreen());
        SelectWorkflowStepCommand = new RelayCommand(step =>
        {
            if (step is string value && !string.IsNullOrWhiteSpace(value))
            {
                WorkflowStep = value;
            }
        });

        UpdateWorkflowStepSelection();
    }

    public ObservableCollection<StatsDesignWorkflowStepItem> WorkflowSteps { get; }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? RequestClose;
    public event Action? FullscreenChanged;

    public ThreeShapeOrdersModel? Order
    {
        get => _order;
        set
        {
            _order = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(DesignName));
        }
    }

    public string WindowTitle =>
        Order is null ? "Stats Design" : $"Stats Design — {Order.IntOrderID}";

    public string DesignName
    {
        get => Order?.IntOrderID ?? "design";
        set { }
    }

    public string WorkflowStep
    {
        get => _workflowStep;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "Margin" : value.Trim();
            if (string.Equals(_workflowStep, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _workflowStep = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsMarginStep));
            OnPropertyChanged(nameof(IsInnerStep));
            OnPropertyChanged(nameof(IsShapeStep));
            OnPropertyChanged(nameof(IsSculptStep));
            OnPropertyChanged(nameof(IsExportStep));
            UpdateWorkflowStepSelection();
        }
    }

    public bool IsMarginStep => string.Equals(WorkflowStep, "Margin", StringComparison.Ordinal);
    public bool IsInnerStep => string.Equals(WorkflowStep, "Inner", StringComparison.Ordinal) ||
                               string.Equals(WorkflowStep, "Cement", StringComparison.Ordinal);
    public bool IsShapeStep => string.Equals(WorkflowStep, "Shape", StringComparison.Ordinal);
    public bool IsSculptStep => string.Equals(WorkflowStep, "Sculpt", StringComparison.Ordinal);
    public bool IsExportStep => string.Equals(WorkflowStep, "Export", StringComparison.Ordinal);

    public bool IsFullscreen
    {
        get => _isFullscreen;
        set
        {
            if (_isFullscreen == value)
            {
                return;
            }

            _isFullscreen = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FullscreenButtonLabel));
            FullscreenChanged?.Invoke();
        }
    }

    public string FullscreenButtonLabel => IsFullscreen ? "Exit full screen" : "Full screen";

    public bool RestoreLastDesignOnOpen
    {
        get => _restoreLastDesignOnOpen;
        set
        {
            if (_restoreLastDesignOnOpen == value)
            {
                return;
            }

            _restoreLastDesignOnOpen = value;
            OnPropertyChanged();
        }
    }

    public double NumericStep => StatsDesignDefaults.NumericStepMm;
    public int NumericDecimals => StatsDesignDefaults.NumericDecimals;

    public ICommand CloseWindowCommand { get; }
    public ICommand ToggleFullscreenCommand { get; }
    public ICommand SelectWorkflowStepCommand { get; }

    private void UpdateWorkflowStepSelection()
    {
        foreach (var step in WorkflowSteps)
        {
            step.IsCurrent = string.Equals(step.StepKey, WorkflowStep, StringComparison.Ordinal);
        }
    }

    private void CloseRequested() => RequestClose?.Invoke();

    private void ToggleFullscreen() => IsFullscreen = !IsFullscreen;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}