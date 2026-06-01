using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using StatsClient.MVVM.Core;
using Application = System.Windows.Application;
using static StatsClient.MVVM.Core.LocalSettingsDB;

namespace StatsClient
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Clear any stale WPF render-tier cache (DISPLAY* subkeys under Avalon.Graphics).
            // A stale Tier 0 from a previous RDP/console session prevents WPF re-probing the GPU.
            ClearWpfRenderTierCache();

            // Request hardware rendering at process level.
            RenderOptions.ProcessRenderMode = RenderMode.Default;

            WriteRenderInfoLog();

            if (XamlSmokeRunner.IsRequested)
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var exitCode = XamlSmokeRunner.Run();
                Shutdown(exitCode);
                return;
            }

            // Apply saved Light/Dark scheme before StartupUri creates SplashWindow.
            ColorSchemeManager.ApplySavedFromLocalSettings();

            base.OnStartup(e);
        }

        /// <summary>
        /// Deletes per-display render-tier cache keys WPF writes under
        /// HKLM\SOFTWARE\Microsoft\Avalon.Graphics\DISPLAY*.
        /// </summary>
        private static void ClearWpfRenderTierCache()
        {
            try
            {
                const string avalon = @"SOFTWARE\Microsoft\Avalon.Graphics";
                using var key = Registry.LocalMachine.OpenSubKey(avalon, writable: true);
                if (key is null) return;
                foreach (var sub in key.GetSubKeyNames())
                {
                    if (sub.StartsWith("DISPLAY", StringComparison.OrdinalIgnoreCase))
                    {
                        try { key.DeleteSubKeyTree(sub, throwOnMissingSubKey: false); }
                        catch { /* skip if access denied */ }
                    }
                }
            }
            catch { /* never crash the app */ }
        }

        /// <summary>
        /// Writes WPF render-tier and GPU capability info to
        /// %ProgramData%\Stats_Client\render-info.log (always writable).
        /// Also detects the DisableHWAcceleration registry key.
        /// </summary>
        private static void WriteRenderInfoLog()
        {
            try
            {
                int tier = RenderCapability.Tier >> 16;
                string tierLabel = tier switch
                {
                    0 => "Software only (no HW acceleration)",
                    1 => "Partial hardware acceleration",
                    2 => "Full hardware acceleration",
                    _ => $"Unknown ({tier})"
                };

                bool ps2 = RenderCapability.IsPixelShaderVersionSupported(2, 0);
                bool ps3 = RenderCapability.IsPixelShaderVersionSupported(3, 0);
                bool isRemote = System.Windows.SystemParameters.IsRemotelyControlled;
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                const string avalon = @"Software\Microsoft\Avalon.Graphics";
                int hwDisabledUser    = (int?)Registry.CurrentUser .OpenSubKey(avalon)?.GetValue("DisableHWAcceleration") ?? 0;
                int hwDisabledMachine = (int?)Registry.LocalMachine.OpenSubKey(avalon)?.GetValue("DisableHWAcceleration") ?? 0;
                bool hwForcedOff = hwDisabledUser == 1 || hwDisabledMachine == 1;

                string hwDiagnosis = hwForcedOff
                    ? $"*** DisableHWAcceleration FOUND ù HKCU:{hwDisabledUser} HKLM:{hwDisabledMachine} ***"
                    : "DisableHWAcceleration: not set (OK)";

                var lines = new[]
                {
                    $"=== StatsClient Render Info === [{timestamp}]",
                    $"Machine       : {Environment.MachineName}",
                    $"User          : {Environment.UserName}",
                    $"Remote session: {isRemote}",
                    $"Render tier   : {tier} ù {tierLabel}",
                    $"PS 2.0        : {ps2}",
                    $"PS 3.0        : {ps3}",
                    $"OS            : {Environment.OSVersion}",
                    $"CPU cores     : {Environment.ProcessorCount}",
                    hwDiagnosis,
                    new string('-', 55),
                };

                string logFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Stats_Client");
                Directory.CreateDirectory(logFolder);
                File.WriteAllLines(Path.Combine(logFolder, "render-info.log"), lines);
            }
            catch { /* never crash the app over a log write */ }
        }
    }
}
