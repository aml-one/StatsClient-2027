using StatsClient.MVVM.Core;
using StatsClient.MVVM.View;
using static StatsClient.MVVM.Core.DatabaseConnection;
using static StatsClient.MVVM.Core.Enums;
using static StatsClient.MVVM.Core.LocalSettingsDB;
using static StatsClient.MVVM.Core.MessageBoxes;

namespace StatsClient.MVVM.ViewModel;

public partial class MainViewModel
{
    public const string SettingEncodeIdentifierVisionEndpoint = "EncodeIdentifier_VisionEndpoint";
    public const string SettingEncodeIdentifierMaxTokens = "EncodeIdentifier_MaxTokens";
    public const string SettingEncodeIdentifierTemperature = "EncodeIdentifier_Temperature";
    public const string SettingEncodeIdentifierTopP = "EncodeIdentifier_TopP";

    public const string DefaultEncodeIdentifierVisionEndpoint = "https://integrate.api.nvidia.com/v1/chat/completions";

    private bool _encodeIdentifierSettingsLoaded;
    private bool cbSettingModuleEncodeIdentifier;

    public bool CbSettingModuleEncodeIdentifier
    {
        get => cbSettingModuleEncodeIdentifier;
        set
        {
            cbSettingModuleEncodeIdentifier = value;
            RaisePropertyChanged(nameof(CbSettingModuleEncodeIdentifier));
        }
    }

    private string encodeIdentifierVisionEndpoint = DefaultEncodeIdentifierVisionEndpoint;
    public string EncodeIdentifierVisionEndpoint
    {
        get => encodeIdentifierVisionEndpoint;
        set
        {
            encodeIdentifierVisionEndpoint = value ?? string.Empty;
            RaisePropertyChanged(nameof(EncodeIdentifierVisionEndpoint));
        }
    }

    private string encodeIdentifierMaxTokens = "512";
    public string EncodeIdentifierMaxTokens
    {
        get => encodeIdentifierMaxTokens;
        set
        {
            encodeIdentifierMaxTokens = value ?? string.Empty;
            RaisePropertyChanged(nameof(EncodeIdentifierMaxTokens));
        }
    }

    private string encodeIdentifierTemperature = "0.2";
    public string EncodeIdentifierTemperature
    {
        get => encodeIdentifierTemperature;
        set
        {
            encodeIdentifierTemperature = value ?? string.Empty;
            RaisePropertyChanged(nameof(EncodeIdentifierTemperature));
        }
    }

    private string encodeIdentifierTopP = "0.7";
    public string EncodeIdentifierTopP
    {
        get => encodeIdentifierTopP;
        set
        {
            encodeIdentifierTopP = value ?? string.Empty;
            RaisePropertyChanged(nameof(EncodeIdentifierTopP));
        }
    }

    public RelayCommand CbSettingModuleEncodeIdentifierCommand { get; set; } = null!;
    public RelayCommand SaveEncodeIdentifierSettingsCommand { get; set; } = null!;

    private void InitEncodeIdentifierCommands()
    {
        CbSettingModuleEncodeIdentifierCommand = new RelayCommand(_ => CbSettingModuleEncodeIdentifierMethod());
        SaveEncodeIdentifierSettingsCommand = new RelayCommand(_ => SaveEncodeIdentifierSettingsMethod());
    }

    private void LoadEncodeIdentifierSettings()
    {
        if (_encodeIdentifierSettingsLoaded)
            return;

        _encodeIdentifierSettingsLoaded = true;

        string endpoint = ReadStatsSetting(SettingEncodeIdentifierVisionEndpoint);
        if (!string.IsNullOrWhiteSpace(endpoint))
            EncodeIdentifierVisionEndpoint = endpoint;

        string maxTokens = ReadStatsSetting(SettingEncodeIdentifierMaxTokens);
        if (!string.IsNullOrWhiteSpace(maxTokens))
            EncodeIdentifierMaxTokens = maxTokens;

        string temperature = ReadStatsSetting(SettingEncodeIdentifierTemperature);
        if (!string.IsNullOrWhiteSpace(temperature))
            EncodeIdentifierTemperature = temperature;

        string topP = ReadStatsSetting(SettingEncodeIdentifierTopP);
        if (!string.IsNullOrWhiteSpace(topP))
            EncodeIdentifierTopP = topP;
    }

    private void CbSettingModuleEncodeIdentifierMethod()
    {
        WriteLocalSetting("ModuleEncodeIdentifier", CbSettingModuleEncodeIdentifier.ToString());
        if (CbSettingModuleEncodeIdentifier)
            LoadEncodeIdentifierSettings();
    }

    private void SaveEncodeIdentifierSettingsMethod()
    {
        WriteStatsSetting(SettingEncodeIdentifierVisionEndpoint,
            string.IsNullOrWhiteSpace(EncodeIdentifierVisionEndpoint)
                ? DefaultEncodeIdentifierVisionEndpoint
                : EncodeIdentifierVisionEndpoint.Trim());

        WriteStatsSetting(SettingEncodeIdentifierMaxTokens,
            string.IsNullOrWhiteSpace(EncodeIdentifierMaxTokens) ? "512" : EncodeIdentifierMaxTokens.Trim());

        WriteStatsSetting(SettingEncodeIdentifierTemperature,
            string.IsNullOrWhiteSpace(EncodeIdentifierTemperature) ? "0.2" : EncodeIdentifierTemperature.Trim());

        WriteStatsSetting(SettingEncodeIdentifierTopP,
            string.IsNullOrWhiteSpace(EncodeIdentifierTopP) ? "0.7" : EncodeIdentifierTopP.Trim());

        ShowMessageBox("Encode Identifier", "Vision settings saved to Stats database.", SMessageBoxButtons.Close, NotificationIcon.Info, 120, _MainWindow);
    }
}
