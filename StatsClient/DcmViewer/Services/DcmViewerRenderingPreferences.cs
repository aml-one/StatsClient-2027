using StatsClient.MVVM.Core;

namespace DCMViewer.Services;

/// <summary>
/// Local preference for DCM viewer rendering quality. High quality keeps the full DX11 setup;
/// standard mode reduces per-frame work for smoother rotation on remote sessions.
/// </summary>
public static class DcmViewerRenderingPreferences
{
    public const string LocalSettingKey = "DcmHighQualityRendering";

    public static bool HighQualityRendering { get; private set; } = true;

    public static event EventHandler? Changed;

    public static void LoadFromLocalSettings()
    {
        var saved = LocalSettingsDB.ReadLocalSetting(LocalSettingKey);
        if (string.IsNullOrWhiteSpace(saved))
        {
            HighQualityRendering = true;
            return;
        }

        HighQualityRendering = !bool.TryParse(saved, out bool value) || value;
    }

    public static void SetHighQualityRendering(bool value, bool persist = true)
    {
        if (HighQualityRendering == value)
        {
            return;
        }

        HighQualityRendering = value;
        if (persist)
        {
            LocalSettingsDB.WriteLocalSetting(LocalSettingKey, value.ToString());
        }

        Changed?.Invoke(null, EventArgs.Empty);
    }
}
