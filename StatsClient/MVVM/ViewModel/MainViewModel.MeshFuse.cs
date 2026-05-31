using DCMViewer.Services;
using StatsClient.MVVM.Core;
using static StatsClient.MVVM.Core.Enums;
using static StatsClient.MVVM.Core.LocalSettingsDB;
using static StatsClient.MVVM.Core.MessageBoxes;

namespace StatsClient.MVVM.ViewModel;

public partial class MainViewModel
{
    private bool _meshFuseSettingsLoaded;

    private string _dcmViewerFuseMode = nameof(MeshFuseMode.UnifiedShell);
    public string DcmViewerFuseMode
    {
        get => _dcmViewerFuseMode;
        set
        {
            _dcmViewerFuseMode = value ?? nameof(MeshFuseMode.UnifiedShell);
            RaisePropertyChanged(nameof(DcmViewerFuseMode));
            RaisePropertyChanged(nameof(IsDcmViewerVoxelFuseMode));
            RaisePropertyChanged(nameof(IsDcmViewerAdvancedFuseMode));
        }
    }

    public bool IsDcmViewerAdvancedFuseMode =>
        !string.Equals(DcmViewerFuseMode, nameof(MeshFuseMode.SolidCombine), StringComparison.OrdinalIgnoreCase);

    public bool IsDcmViewerVoxelFuseMode =>
        string.Equals(DcmViewerFuseMode, nameof(MeshFuseMode.VoxelEnvelope), StringComparison.OrdinalIgnoreCase);

    private string _dcmViewerFuseResolution = MeshFuseOptions.DefaultResolution.ToString();
    public string DcmViewerFuseResolution
    {
        get => _dcmViewerFuseResolution;
        set
        {
            _dcmViewerFuseResolution = value ?? string.Empty;
            RaisePropertyChanged(nameof(DcmViewerFuseResolution));
        }
    }

    private string _dcmViewerFuseGapBridgeVoxels = MeshFuseOptions.DefaultGapBridgeVoxels.ToString();
    public string DcmViewerFuseGapBridgeVoxels
    {
        get => _dcmViewerFuseGapBridgeVoxels;
        set
        {
            _dcmViewerFuseGapBridgeVoxels = value ?? string.Empty;
            RaisePropertyChanged(nameof(DcmViewerFuseGapBridgeVoxels));
        }
    }

    private string _dcmViewerFuseShellThicknessVoxels = MeshFuseOptions.DefaultShellThicknessVoxels.ToString();
    public string DcmViewerFuseShellThicknessVoxels
    {
        get => _dcmViewerFuseShellThicknessVoxels;
        set
        {
            _dcmViewerFuseShellThicknessVoxels = value ?? string.Empty;
            RaisePropertyChanged(nameof(DcmViewerFuseShellThicknessVoxels));
        }
    }

    private string _dcmViewerFuseSmoothPasses = MeshFuseOptions.DefaultSmoothPasses.ToString();
    public string DcmViewerFuseSmoothPasses
    {
        get => _dcmViewerFuseSmoothPasses;
        set
        {
            _dcmViewerFuseSmoothPasses = value ?? string.Empty;
            RaisePropertyChanged(nameof(DcmViewerFuseSmoothPasses));
        }
    }

    public RelayCommand SaveDcmViewerFuseSettingsCommand { get; set; } = null!;

    private void InitMeshFuseSettingsCommands()
    {
        SaveDcmViewerFuseSettingsCommand = new RelayCommand(_ => SaveDcmViewerFuseSettingsMethod());
    }

    private void LoadDcmViewerFuseSettings()
    {
        if (_meshFuseSettingsLoaded)
        {
            return;
        }

        _meshFuseSettingsLoaded = true;
        var options = MeshFuseSettings.Load();
        DcmViewerFuseMode = options.Mode.ToString();
        DcmViewerFuseResolution = options.Resolution.ToString();
        DcmViewerFuseGapBridgeVoxels = options.GapBridgeVoxels.ToString();
        DcmViewerFuseShellThicknessVoxels = options.ShellThicknessVoxels.ToString();
        DcmViewerFuseSmoothPasses = options.SmoothPasses.ToString();
    }

    private void SaveDcmViewerFuseSettingsMethod()
    {
        if (!TryParseFuseSettings(out var options, out var error))
        {
            ShowMessageBox("DCM Viewer", error, SMessageBoxButtons.Close, NotificationIcon.Warning, 120, _MainWindow);
            return;
        }

        MeshFuseSettings.Save(options);
        DcmViewerFuseMode = options.Mode.ToString();
        DcmViewerFuseResolution = options.Resolution.ToString();
        DcmViewerFuseGapBridgeVoxels = options.GapBridgeVoxels.ToString();
        DcmViewerFuseShellThicknessVoxels = options.ShellThicknessVoxels.ToString();
        DcmViewerFuseSmoothPasses = options.SmoothPasses.ToString();

        ShowMessageBox(
            "DCM Viewer",
            "Mesh fuse settings saved. They apply the next time you export a fused STL from Order Info.",
            SMessageBoxButtons.Close,
            NotificationIcon.Info,
            120,
            _MainWindow);
    }

    private bool TryParseFuseSettings(out MeshFuseOptions options, out string error)
    {
        var mode = string.Equals(DcmViewerFuseMode, nameof(MeshFuseMode.VoxelEnvelope), StringComparison.OrdinalIgnoreCase)
            ? MeshFuseMode.VoxelEnvelope
            : string.Equals(DcmViewerFuseMode, nameof(MeshFuseMode.SolidCombine), StringComparison.OrdinalIgnoreCase)
                ? MeshFuseMode.SolidCombine
                : MeshFuseMode.UnifiedShell;

        if (!int.TryParse(DcmViewerFuseResolution.Trim(), out var resolution))
        {
            options = MeshFuseOptions.Default;
            error = $"Grid resolution must be a whole number ({MeshFuseOptions.MinResolution}–{MeshFuseOptions.MaxResolution}).";
            return false;
        }

        if (!int.TryParse(DcmViewerFuseGapBridgeVoxels.Trim(), out var gapBridge))
        {
            options = MeshFuseOptions.Default;
            error = $"Gap bridge must be a whole number ({MeshFuseOptions.MinGapBridgeVoxels}–{MeshFuseOptions.MaxGapBridgeVoxels}).";
            return false;
        }

        if (!int.TryParse(DcmViewerFuseShellThicknessVoxels.Trim(), out var shellThickness))
        {
            options = MeshFuseOptions.Default;
            error = $"Shell thickness must be a whole number ({MeshFuseOptions.MinShellThicknessVoxels}–{MeshFuseOptions.MaxShellThicknessVoxels}).";
            return false;
        }

        if (!int.TryParse(DcmViewerFuseSmoothPasses.Trim(), out var smoothPasses))
        {
            options = MeshFuseOptions.Default;
            error = $"Smooth passes must be a whole number ({MeshFuseOptions.MinSmoothPasses}–{MeshFuseOptions.MaxSmoothPasses}).";
            return false;
        }

        resolution = Math.Clamp(resolution, MeshFuseOptions.MinResolution, MeshFuseOptions.MaxResolution);
        gapBridge = Math.Clamp(gapBridge, MeshFuseOptions.MinGapBridgeVoxels, MeshFuseOptions.MaxGapBridgeVoxels);
        shellThickness = Math.Clamp(shellThickness, MeshFuseOptions.MinShellThicknessVoxels, MeshFuseOptions.MaxShellThicknessVoxels);
        smoothPasses = Math.Clamp(smoothPasses, MeshFuseOptions.MinSmoothPasses, MeshFuseOptions.MaxSmoothPasses);

        options = new MeshFuseOptions(mode, resolution, gapBridge, smoothPasses, shellThickness);
        error = string.Empty;
        return true;
    }
}
