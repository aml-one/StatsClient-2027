using System.Windows;
using System.Windows.Controls;
using DCMViewer.Services;

namespace DCMViewer;

internal partial class FuseExportChoiceWindow : Window
{
    public FuseExportChoiceWindow(int meshCount, MeshFuseMode defaultMode)
    {
        InitializeComponent();

        TitleTextBlock.Text = meshCount == 1
            ? "Export visible scan"
            : $"Merge {meshCount:N0} visible scans";

        CleanupArtifactsCheckBox.IsChecked = MeshFuseSettings.LoadCleanupArtifactsEnabled();
        CleanupStrengthSlider.Value = MeshFuseSettings.LoadCleanupStrength();
        UpdateCleanupStrengthLabel();

        switch (defaultMode)
        {
            case MeshFuseMode.VoxelEnvelope:
                VoxelEnvelopeRadio.IsChecked = true;
                break;
            case MeshFuseMode.UnifiedShell:
                UnifiedShellRadio.IsChecked = true;
                break;
            default:
                SolidCombineRadio.IsChecked = true;
                break;
        }

        UpdateUnifiedShellCleanupVisibility();
    }

    public static FuseExportChoice? ShowDialog(Window? owner, int meshCount)
    {
        var window = new FuseExportChoiceWindow(meshCount, MeshFuseMode.SolidCombine)
        {
            Owner = owner
        };

        return window.ShowDialog() == true ? window.GetSelectedChoice() : null;
    }

    private FuseExportChoice GetSelectedChoice()
    {
        var mode = GetSelectedMode();
        var cleanupEnabled = mode == MeshFuseMode.UnifiedShell && CleanupArtifactsCheckBox.IsChecked == true;
        var cleanupStrength = (int)Math.Round(CleanupStrengthSlider.Value);

        MeshFuseSettings.SaveCleanupPreferences(cleanupEnabled, cleanupStrength);

        return new FuseExportChoice(mode, cleanupEnabled, cleanupStrength);
    }

    private MeshFuseMode GetSelectedMode()
    {
        if (VoxelEnvelopeRadio.IsChecked == true)
        {
            return MeshFuseMode.VoxelEnvelope;
        }

        if (SolidCombineRadio.IsChecked == true)
        {
            return MeshFuseMode.SolidCombine;
        }

        return MeshFuseMode.UnifiedShell;
    }

    private void FuseModeRadio_OnChecked(object sender, RoutedEventArgs e)
    {
        UpdateUnifiedShellCleanupVisibility();
    }

    private void CleanupControls_OnChanged(object sender, RoutedEventArgs e)
    {
        UpdateCleanupStrengthLabel();
    }

    private void UpdateUnifiedShellCleanupVisibility()
    {
        UnifiedShellCleanupPanel.Visibility = UnifiedShellRadio.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdateCleanupStrengthLabel()
    {
        var strength = (int)Math.Round(CleanupStrengthSlider.Value);
        var maxIslandTriangles = MeshFuseOptions.MapCleanupStrengthToMaxIslandTriangles(strength);
        CleanupStrengthLabel.Text = $"{strength}";
        CleanupStrengthSlider.ToolTip = $"Removes disconnected islands up to about {maxIslandTriangles:N0} triangles from the saved STL.";
    }

    private void Continue_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
