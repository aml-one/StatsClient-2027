namespace StatsClient.MVVM.ViewModel;

public sealed class StatsDesignWorkflowStepItem : System.ComponentModel.INotifyPropertyChanged
{
    private bool _isCurrent;

    public required string StepKey { get; init; }
    public required string Label { get; init; }
    public string? IconGlyph { get; init; }
    public string? IconImageUri { get; init; }

    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            if (_isCurrent == value)
            {
                return;
            }

            _isCurrent = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsCurrent)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsCurrentBrush)));
        }
    }

    public System.Windows.Media.Brush IsCurrentBrush =>
        IsCurrent
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0xF0, 0xF8))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0x98, 0xA8));

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}