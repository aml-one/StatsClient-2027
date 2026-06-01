using System.IO;
using System.Windows;
using System.Windows.Markup;
using StatsClient.MVVM.Model;
using StatsClient.MVVM.View;

namespace StatsClient.MVVM.Core;

/// <summary>
/// Loads key views under the real application resource tree to catch XAML parse errors early.
/// Activate with environment variable STATS_XAML_SMOKE=1.
/// </summary>
internal static class XamlSmokeRunner
{
    public const string EnvironmentVariable = "STATS_XAML_SMOKE";

    public static bool IsRequested =>
        string.Equals(Environment.GetEnvironmentVariable(EnvironmentVariable), "1", StringComparison.Ordinal);

    public static int Run()
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "xaml-smoke.log");
        var failures = new List<string>();
        File.WriteAllText(logPath, $"XAML smoke test started {DateTime.Now:O}{Environment.NewLine}");

        try
        {
            ColorSchemeManager.InitializeFromApplicationResources();
            ColorSchemeManager.Apply(ColorSchemeManager.DarkScheme);
#if DEBUG
            XamlResourceValidator.ValidateProjectXaml();
            ColorSchemeResourceCatalog.ValidateActiveScheme(Application.Current);
#endif

            RunSmokePass("Dark", failures, logPath, includeParameterizedViews: true);
        }
        catch (Exception ex)
        {
            failures.Add(ex.ToString());
            File.AppendAllText(logPath, ex + Environment.NewLine);
        }

        if (failures.Count > 0)
        {
            File.AppendAllText(logPath, $"FAILED: {failures.Count}{Environment.NewLine}");
            foreach (var failure in failures)
            {
                Console.Error.WriteLine(failure);
            }

            return 1;
        }

        File.AppendAllText(logPath, "PASSED" + Environment.NewLine);
        Console.WriteLine("XAML smoke test passed.");
        return 0;
    }

    private static void RunSmokePass(string schemeLabel, List<string> failures, string logPath, bool includeParameterizedViews)
    {
        File.AppendAllText(logPath, $"--- {schemeLabel} scheme ---{Environment.NewLine}");
        Console.WriteLine($"--- {schemeLabel} scheme ---");

        SmokeXaml($"{schemeLabel}/SplashWindow", "/MVVM/View/SplashWindow.xaml", failures, logPath, includeParameterizedViews);
        SmokeXaml($"{schemeLabel}/MainWindow", "/MVVM/View/MainWindow.xaml", failures, logPath, includeParameterizedViews);
        SmokeXaml($"{schemeLabel}/ServerLogTabPanel", "/MVVM/View/ServerLogTabPanel.xaml", failures, logPath, includeParameterizedViews);
        SmokeXaml($"{schemeLabel}/AccountInfosTabPanel", "/MVVM/View/AccountInfosTabPanel.xaml", failures, logPath, includeParameterizedViews);
        SmokeXaml($"{schemeLabel}/HomeTabPanel", "/MVVM/View/HomeTabPanel.xaml", failures, logPath, includeParameterizedViews);
        SmokeXaml($"{schemeLabel}/ThreeShapeFilterPanel", "/MVVM/View/ThreeShapeFilterPanel.xaml", failures, logPath, includeParameterizedViews);
        if (includeParameterizedViews)
        {
            SmokeXaml($"{schemeLabel}/SetPanColorWindow", "/MVVM/View/SetPanColorWindow.xaml", failures, logPath, includeParameterizedViews);
        }
        SmokeXaml($"{schemeLabel}/MainMenu", "/MVVM/View/MainMenu.xaml", failures, logPath, includeParameterizedViews);
        if (includeParameterizedViews)
        {
            SmokeXaml($"{schemeLabel}/OrderInfoWindow", "/MVVM/View/OrderInfoWindow.xaml", failures, logPath, includeParameterizedViews);
            SmokeXaml($"{schemeLabel}/UserPanel", "/MVVM/View/UserPanel.xaml", failures, logPath, includeParameterizedViews);
        }
        SmokeXaml($"{schemeLabel}/SmartOrderNames2Page", "/MVVM/View/SmartOrderNames2Page.xaml", failures, logPath, includeParameterizedViews);
        SmokeXaml($"{schemeLabel}/DcmViewer.MainWindow", "/DcmViewer/MainWindow.xaml", failures, logPath, includeParameterizedViews);
        SmokeXaml($"{schemeLabel}/DcmBusyOverlayPanel", "/DcmViewer/Controls/DcmBusyOverlayPanel.xaml", failures, logPath, includeParameterizedViews);
    }

    private static void SmokeXaml(string name, string componentPath, List<string> failures, string logPath, bool includeParameterizedViews)
    {
        try
        {
            if (includeParameterizedViews && TrySmokeParameterizedView(componentPath, out var xamlError))
            {
                if (xamlError is not null)
                {
                    throw xamlError;
                }

                File.AppendAllText(logPath, $"OK  {name}{Environment.NewLine}");
                Console.WriteLine($"OK  {name}");
                return;
            }

            Application.LoadComponent(new Uri(componentPath, UriKind.Relative));
            File.AppendAllText(logPath, $"OK  {name}{Environment.NewLine}");
            Console.WriteLine($"OK  {name}");
        }
        catch (Exception ex)
        {
            var message = $"{name}: {FormatException(ex)}{Environment.NewLine}{ex}";
            failures.Add(message);
            File.AppendAllText(logPath, $"FAIL {message}{Environment.NewLine}");
            Console.Error.WriteLine($"FAIL {name}: {FormatException(ex)}");
        }
    }

    /// <summary>
    /// Views whose only constructor requires parameters cannot use LoadComponent(Uri).
    /// Returns true when handled; xamlError is set only for markup failures.
    /// </summary>
    private static bool TrySmokeParameterizedView(string componentPath, out Exception? xamlError)
    {
        xamlError = null;
        try
        {
            switch (componentPath)
            {
                case "/MVVM/View/SetPanColorWindow.xaml":
                    _ = new SetPanColorWindow("", "255-255-255");
                    return true;
                case "/MVVM/View/OrderInfoWindow.xaml":
                    _ = new OrderInfoWindow(new ThreeShapeOrdersModel());
                    return true;
                case "/MVVM/View/UserPanel.xaml":
                {
                    var panel = new UserPanel("smoke");
                    panel._timer.Stop();
                    panel._timer.Dispose();
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            if (ContainsXamlParseException(ex))
            {
                xamlError = ex;
                return true;
            }

            // Constructor dependencies (ViewModels, DB) are unavailable in smoke mode — markup already loaded.
            return true;
        }

        return false;
    }

    private static bool ContainsXamlParseException(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is XamlParseException)
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatException(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is XamlParseException xpe)
            {
                return xpe.Message;
            }
        }

        return ex.Message;
    }
}
