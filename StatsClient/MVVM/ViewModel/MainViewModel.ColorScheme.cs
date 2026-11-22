using StatsClient.MVVM.Core;
using static StatsClient.MVVM.Core.LocalSettingsDB;

namespace StatsClient.MVVM.ViewModel;

public partial class MainViewModel
{
    private string cbSettingColorScheme = Core.ColorSchemeManager.DefaultScheme;

    public string CbSettingColorScheme
    {
        get => cbSettingColorScheme;
        set
        {
            cbSettingColorScheme = value;
            RaisePropertyChanged(nameof(CbSettingColorScheme));
        }
    }

    public List<string> ColorSchemeOptions { get; } =
        Core.ColorSchemeManager.AvailableSchemes.ToList();

    public RelayCommand ColorSchemeSelectionChangedCommand { get; set; } = null!;

    private void InitializeColorSchemeCommands()
    {
        ColorSchemeSelectionChangedCommand =
            new RelayCommand(_ => ColorSchemeSelectionChangedMethod());
    }

    private void LoadAndApplyColorSchemeSetting()
    {
        Core.ColorSchemeManager.ApplySavedFromLocalSettings();
        CbSettingColorScheme = Core.ColorSchemeManager.CurrentScheme;
        ApplyActiveColorSchemeToWindowBackground();
        ApplyLabnextHostStyleForCurrentScheme();
    }

    private void ColorSchemeSelectionChangedMethod()
    {
        var scheme = Core.ColorSchemeManager.NormalizeScheme(CbSettingColorScheme);
        CbSettingColorScheme = scheme;
        WriteLocalSetting(Core.ColorSchemeManager.LocalSettingKey, scheme);
        Core.ColorSchemeManager.Apply(scheme);
        Core.ColorSchemeManager.RemoveLegacySchemeOverrides();
        ApplyActiveColorSchemeToWindowBackground();
        ApplyLabnextHostStyleForCurrentScheme();
    }

    private void ApplyLabnextHostStyleForCurrentScheme()
    {
        if (!IsMainWindowReady)
            return;

        _MainWindow.UpdateLabnextChromeForColorScheme();

        if (!_MainWindow.webviewLabnext.IsInitialized || _MainWindow.webviewLabnext.CoreWebView2 is null)
            return;

        _ = _MainWindow.ApplyLabnextHostStyleForCurrentSchemeAsync();
    }

    private void ApplyActiveColorSchemeToWindowBackground()
    {
        var windowBackground = Core.ColorSchemeManager.GetWindowBackgroundHex();
        ColorSchemeWindowBackground = windowBackground;
        ModernColorSchemeWindowBackground = windowBackground;
        WindowBackground = windowBackground;
    }
}
