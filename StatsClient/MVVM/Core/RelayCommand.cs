using System.Windows.Input;

namespace StatsClient.MVVM.Core;

public class RelayCommand(Action<object> execute, Func<object, bool>? canExecute = null) : ICommand
{
    private readonly Action<object> execute = execute;
    private readonly Func<object, bool> canExecute = canExecute!;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return this.canExecute == null || this.canExecute(parameter!);
    }

    public void Execute(object? parameter) => this.execute(parameter!);
}
