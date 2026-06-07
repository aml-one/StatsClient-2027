using System.Windows.Threading;
using DCMViewer.ViewModels;

namespace DCMViewer.Infrastructure;

internal static class WpfUiThread
{
    public static Dispatcher RequireDispatcher() =>
        MainViewModel.UiDispatcher
        ?? System.Windows.Application.Current?.Dispatcher
        ?? throw new InvalidOperationException("WPF application dispatcher is not available.");

    public static bool CheckAccess() => RequireDispatcher().CheckAccess();

    public static void Invoke(Action action)
    {
        var dispatcher = RequireDispatcher();
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }

    public static async Task RunAsync(Func<Task> action)
    {
        var dispatcher = RequireDispatcher();
        if (dispatcher.CheckAccess())
        {
            await action();
            return;
        }

        await dispatcher.InvokeAsync(action).Task.Unwrap();
    }

    public static async Task RunAsync(Action action)
    {
        var dispatcher = RequireDispatcher();
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        await dispatcher.InvokeAsync(action);
    }

    public static async Task<T> RunAsync<T>(Func<T> func)
    {
        var dispatcher = RequireDispatcher();
        if (dispatcher.CheckAccess())
        {
            return func();
        }

        return await dispatcher.InvokeAsync(func).Task;
    }
}
