using StatsClient.MVVM.Model;
using StatsClient.MVVM.View;
using System.Windows;

namespace StatsClient.MVVM.ViewModel;

public partial class MainViewModel
{
    private PrescriptionExtractPreviewWindow? _prescriptionExtractPreviewWindow;

    private void ShowPrescriptionExtractPreview()
    {
        if (_MainWindow is null)
            return;

        if (_prescriptionExtractPreviewWindow is null)
        {
            _prescriptionExtractPreviewWindow = new PrescriptionExtractPreviewWindow(_MainWindow);
            _prescriptionExtractPreviewWindow.Closed += (_, _) => _prescriptionExtractPreviewWindow = null;
        }

        _prescriptionExtractPreviewWindow.ApplyExtractedFields(LastPrescriptionPdfExtractedFields);

        if (!_prescriptionExtractPreviewWindow.IsVisible)
            _prescriptionExtractPreviewWindow.Show();
        else
            _prescriptionExtractPreviewWindow.Activate();
    }

    private void UpdatePrescriptionExtractPreviewWindow(PrescriptionPdfExtractedFields? fields)
    {
        if (_prescriptionExtractPreviewWindow is null || !_prescriptionExtractPreviewWindow.IsVisible)
            return;

        _prescriptionExtractPreviewWindow.ApplyExtractedFields(fields);
    }

    private void PushPrescriptionExtractedFields(PrescriptionPdfExtractedFields? fields)
    {
        LastPrescriptionPdfExtractedFields = fields;

        if (Application.Current.Dispatcher.CheckAccess())
            UpdatePrescriptionExtractPreviewWindow(fields);
        else
            Application.Current.Dispatcher.Invoke(() => UpdatePrescriptionExtractPreviewWindow(fields));
    }

    internal void ClosePrescriptionExtractPreviewWindowForShutdown()
    {
        if (_prescriptionExtractPreviewWindow is null)
        {
            return;
        }

        try
        {
            _prescriptionExtractPreviewWindow.Close();
        }
        catch
        {
        }

        _prescriptionExtractPreviewWindow = null;
    }
}
