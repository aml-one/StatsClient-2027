using StatsClient.MVVM.Core;
using StatsClient.MVVM.Model;
using StatsClient.MVVM.View;
using System.Drawing;
using System.IO;
using System.Windows;
using Bitmap = System.Drawing.Bitmap;

namespace StatsClient.MVVM.ViewModel;

public partial class MainViewModel
{
    private LastPrescriptionFingerprint? _lastPrescriptionFingerprint;
    private LastPrescriptionFingerprint? _pendingPrescriptionFingerprint;

    private async Task<bool> TryHandleDuplicatePrescriptionAsync(Bitmap renderBitmap, string pageHeaderIsHigh)
    {
        if (string.IsNullOrWhiteSpace(FullPathGlobal) || !File.Exists(FullPathGlobal))
        {
            return false;
        }

        if (_skipNextPrescriptionDuplicateCheck)
        {
            _skipNextPrescriptionDuplicateCheck = false;
            _pendingPrescriptionFingerprint = PrescriptionDuplicateDetector.BuildFingerprint(
                FullPathGlobal,
                renderBitmap,
                pageHeaderIsHigh);
            return false;
        }

        DuplicateMatchResult? match = await Task.Run(() =>
            PrescriptionDuplicateDetector.TryMatch(
                _lastPrescriptionFingerprint,
                FullPathGlobal,
                renderBitmap,
                pageHeaderIsHigh)).ConfigureAwait(true);

        if (match is null)
        {
            _pendingPrescriptionFingerprint = PrescriptionDuplicateDetector.BuildFingerprint(
                FullPathGlobal,
                renderBitmap,
                pageHeaderIsHigh);
            return false;
        }

        bool isDuplicate = false;
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            using var incomingCopy = new Bitmap(renderBitmap);
            var reviewWindow = new PrescriptionDuplicateReviewWindow(_MainWindow)
            {
                Topmost = true
            };
            reviewWindow.Initialize(
                incomingCopy,
                match.LastFingerprint.SavedPngPath,
                match.LastFingerprint.PanNumber,
                match.Reason);
            reviewWindow.ShowDialog();
            isDuplicate = reviewWindow.IsDuplicate;
        });

        if (isDuplicate)
        {
            AbortDuplicatePrescription();
            return true;
        }

        _pendingPrescriptionFingerprint = PrescriptionDuplicateDetector.BuildFingerprint(
            FullPathGlobal,
            renderBitmap,
            pageHeaderIsHigh);
        return false;
    }

    private void AbortDuplicatePrescription()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(FullPathGlobal) && File.Exists(FullPathGlobal))
            {
                File.Delete(FullPathGlobal);
            }
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
        }

        ProcessingDigiPrescriptionNow = Visibility.Collapsed;
        ShowingPrescriptionPreviewPanel = Visibility.Collapsed;
        PrescriptionPreviewTimer.Stop();
        _pendingPrescriptionFingerprint = null;
    }

    private void CommitPrescriptionFingerprint(string panNumber, string savedPngPath)
    {
        if (_pendingPrescriptionFingerprint is null)
        {
            return;
        }

        _lastPrescriptionFingerprint = _pendingPrescriptionFingerprint with
        {
            PanNumber = panNumber,
            SavedPngPath = savedPngPath
        };
        _pendingPrescriptionFingerprint = null;
        MarkPrescriptionPanNumberAsUsed(panNumber);
    }

    private void MarkPrescriptionPanNumberAsUsed(string panNumber)
    {
        if (string.IsNullOrWhiteSpace(panNumber))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(LastUsedPanNumber) && !string.Equals(LastUsedPanNumber, panNumber, StringComparison.Ordinal))
        {
            LastUsedPanNumber_oneBefore = LastUsedPanNumber;
        }

        LastUsedPanNumber = panNumber;
        LastUsedPanNumbersDate = DateTime.Now;
    }

    private PrescriptionPdfExtractedFields? lastPrescriptionPdfExtractedFields;

    public PrescriptionPdfExtractedFields? LastPrescriptionPdfExtractedFields
    {
        get => lastPrescriptionPdfExtractedFields;
        private set
        {
            lastPrescriptionPdfExtractedFields = value;
            RaisePropertyChanged(nameof(LastPrescriptionPdfExtractedFields));
            RaisePropertyChanged(nameof(HasPrescriptionExtractData));
        }
    }

    public bool HasPrescriptionExtractData => lastPrescriptionPdfExtractedFields is not null;

    private string _prescriptionEditHeaderType = "1";

    private async Task ProceedWithPrescriptionEditAsync(string pageHeaderIsHigh, PrescriptionScannerDetection? detection = null)
    {
        _prescriptionEditHeaderType = pageHeaderIsHigh;
        if (await TryHandleDuplicatePrescriptionAsync(PrescriptionImageForProcess, pageHeaderIsHigh).ConfigureAwait(true))
        {
            return;
        }

        PrescriptionPdfExtractedFields? extracted = null;
        if (!string.IsNullOrWhiteSpace(FullPathGlobal) && File.Exists(FullPathGlobal))
        {
            detection ??= LastPrescriptionScannerDetection;
            extracted = await Task.Run(() =>
                PrescriptionPdfFieldExtractor.Extract(
                    FullPathGlobal,
                    pageHeaderIsHigh,
                    PrescriptionImageForProcess,
                    detection)).ConfigureAwait(true);
        }

        await Application.Current.Dispatcher.InvokeAsync(() =>
            PushPrescriptionExtractedFields(extracted)).Task.ConfigureAwait(true);

        await Task.Run(() => EditPDF(FullPathGlobal, NextPanNumberGlobal)).ConfigureAwait(true);

        if (InitiateOrderCreation && extracted is not null)
        {
            string panForOrder = NextPanNumberGlobal;
            await Application.Current.Dispatcher.InvokeAsync(() =>
                ShowOrderCreationWindow(extracted, panForOrder)).Task.ConfigureAwait(true);
        }
    }
}
