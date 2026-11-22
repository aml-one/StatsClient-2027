using StatsClient.MVVM.Core;
using StatsClient.MVVM.Model;
using StatsClient.MVVM.View;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Windows;
using static StatsClient.MVVM.Core.Enums;
using static StatsClient.MVVM.Core.Functions;
using static StatsClient.MVVM.Core.LocalSettingsDB;
using static StatsClient.MVVM.Core.MessageBoxes;

namespace StatsClient.MVVM.ViewModel;

public partial class MainViewModel
{
    private PrescriptionPreviousNumbersWindow? _prescriptionPreviousNumbersWindow;
    private bool _skipNextPrescriptionDuplicateCheck;
    private string? _lastReopenablePrescriptionPdfPath;

    public RelayCommand ShowPrescriptionPreviousNumbersCommand { get; private set; } = null!;
    public RelayCommand ReopenLastPrescriptionPdfCommand { get; private set; } = null!;

    private bool _canReopenLastPrescriptionPdf;
    public bool CanReopenLastPrescriptionPdf
    {
        get => _canReopenLastPrescriptionPdf;
        private set
        {
            if (_canReopenLastPrescriptionPdf == value)
            {
                return;
            }

            _canReopenLastPrescriptionPdf = value;
            RaisePropertyChanged(nameof(CanReopenLastPrescriptionPdf));
        }
    }

    private void WirePrescriptionPreviousNumbersCommand()
    {
        ShowPrescriptionPreviousNumbersCommand = new RelayCommand(_ => OpenPrescriptionPreviousNumbersWindow());
        ReopenLastPrescriptionPdfCommand = new RelayCommand(_ => _ = ReopenLastPrescriptionPdfAsync());
    }

    internal void RememberReopenablePrescriptionPdfPath(string? pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
        {
            return;
        }

        _lastReopenablePrescriptionPdfPath = pdfPath.Trim();
        RefreshCanReopenLastPrescriptionPdf();
    }

    private void RefreshCanReopenLastPrescriptionPdf()
    {
        bool canReopen = ResolveLastReopenablePrescriptionPdfPath() is not null;
        CanReopenLastPrescriptionPdf = canReopen;
    }

    private static bool IsExistingPdfPath(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
        && File.Exists(path);

    private string? ResolveLastReopenablePrescriptionPdfPath()
    {
        if (IsExistingPdfPath(_lastReopenablePrescriptionPdfPath))
        {
            return _lastReopenablePrescriptionPdfPath;
        }

        if (IsExistingPdfPath(FullPathGlobal))
        {
            return FullPathGlobal;
        }

        string baseFile = ReadLocalSetting("BaseFile").Replace("|", "'");
        if (IsExistingPdfPath(baseFile))
        {
            return baseFile;
        }

        string lastPan = ReadLocalSetting("LastPanNumber").Trim();
        if (!string.IsNullOrWhiteSpace(lastPan) && !lastPan.StartsWith("Click", StringComparison.OrdinalIgnoreCase))
        {
            string[] candidatePaths =
            [
                Path.Combine(PDFTemp, lastPan + "-original.pdf"),
                PDFTemp + "\\" + lastPan + "-original.pdf",
            ];

            foreach (string candidate in candidatePaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (IsExistingPdfPath(candidate))
                {
                    return candidate;
                }
            }

            PrescriptionSavedState? state = PrescriptionSavedStateStore.TryLoad(PDFTemp, lastPan);
            if (IsExistingPdfPath(state?.OriginalPdfPath))
            {
                return state!.OriginalPdfPath;
            }
        }

        return null;
    }

    private async Task ReopenLastPrescriptionPdfAsync()
    {
        string? pdfPath = ResolveLastReopenablePrescriptionPdfPath();
        if (string.IsNullOrWhiteSpace(pdfPath))
        {
            ShowMessageBox(
                "No PDF",
                "No previous prescription PDF is available to reopen.",
                SMessageBoxButtons.Ok,
                NotificationIcon.Warning,
                12,
                MainWindow.Instance);
            RefreshCanReopenLastPrescriptionPdf();
            return;
        }

        if (ProcessingDigiPrescriptionNow == Visibility.Visible)
        {
            ShowMessageBox(
                "Busy",
                "A prescription is already being processed. Please wait for it to finish.",
                SMessageBoxButtons.Ok,
                NotificationIcon.Warning,
                12,
                MainWindow.Instance);
            return;
        }

        ClearPrescriptionPanEditSession();
        ClearPrescriptionRushEditSession();
        ClearPrescriptionInfoEditSession();
        PmCanEditPanPosition = Visibility.Collapsed;
        PmCanEditRushPosition = Visibility.Collapsed;
        PmCanEditInfoLabelPosition = Visibility.Collapsed;

        lock (PrescriptionPdfWatchGate)
        {
            _prescriptionPdfLastHandledPath = null;
            _prescriptionPdfInFlightPath = null;
        }

        _pendingPrescriptionFingerprint = null;
        _skipNextPrescriptionDuplicateCheck = true;

        await ProcessWatchedPrescriptionPdfAsync(pdfPath, "Reopen").ConfigureAwait(false);
        RefreshCanReopenLastPrescriptionPdf();
    }

    private void OpenPrescriptionPreviousNumbersWindow()
    {
        if (_MainWindow is null)
        {
            return;
        }

        if (_prescriptionPreviousNumbersWindow is not null)
        {
            _prescriptionPreviousNumbersWindow.Close();
            _prescriptionPreviousNumbersWindow = null;
        }

        _prescriptionPreviousNumbersWindow = new PrescriptionPreviousNumbersWindow(_MainWindow);
        _prescriptionPreviousNumbersWindow.SetRows(BuildTodayPrescriptionPreviousNumberRows());
        _prescriptionPreviousNumbersWindow.PanNumberSelected += OnPreviousPrescriptionPanNumberSelected;
        _prescriptionPreviousNumbersWindow.Closed += (_, _) =>
        {
            if (_prescriptionPreviousNumbersWindow is not null)
            {
                _prescriptionPreviousNumbersWindow.PanNumberSelected -= OnPreviousPrescriptionPanNumberSelected;
            }

            _prescriptionPreviousNumbersWindow = null;
        };
        _prescriptionPreviousNumbersWindow.Show();
        _prescriptionPreviousNumbersWindow.Activate();
    }

    private IEnumerable<PrescriptionPreviousNumberRow> BuildTodayPrescriptionPreviousNumberRows()
    {
        string finalLocation = ReadLocalSetting("FinalPrescriptionsFolder");
        if (string.IsNullOrWhiteSpace(finalLocation) || finalLocation.StartsWith("Click", StringComparison.Ordinal))
        {
            return [];
        }

        string todayFolder = Path.Combine(finalLocation, DateTime.Now.ToString("MM-dd"));
        if (!Directory.Exists(todayFolder))
        {
            return [];
        }

        var rows = new List<PrescriptionPreviousNumberRow>();
        foreach (string pngPath in Directory.EnumerateFiles(todayFolder, "*.png"))
        {
            string panNumber = Path.GetFileNameWithoutExtension(pngPath);
            if (string.IsNullOrWhiteSpace(panNumber))
            {
                continue;
            }

            PrescriptionSavedState? state = PrescriptionSavedStateStore.TryLoad(PDFTemp, panNumber);
            DateTime savedAt = state?.SavedAt ?? File.GetLastWriteTime(pngPath);
            bool hasPdf = File.Exists(PDFTemp + "\\" + panNumber + ".pdf")
                || File.Exists(PDFTemp + "\\" + panNumber + "-original.pdf");

            rows.Add(new PrescriptionPreviousNumberRow
            {
                PanNumber = panNumber,
                SavedTimeDisplay = savedAt.ToString("h:mm tt"),
                StatusSummary = BuildPrescriptionStatusSummary(state, hasPdf),
                CanReload = true,
            });
        }

        return rows.OrderByDescending(r =>
        {
            PrescriptionSavedState? state = PrescriptionSavedStateStore.TryLoad(PDFTemp, r.PanNumber);
            return state?.SavedAt ?? DateTime.MinValue;
        });
    }

    private static string BuildPrescriptionStatusSummary(PrescriptionSavedState? state, bool hasPdf)
    {
        var parts = new List<string>();
        if (state is null)
        {
            parts.Add("Pan");
        }
        else
        {
            parts.Add("Pan");
            if (state.HasRush)
            {
                parts.Add("RUSH");
            }

            if (state.HasInfoLabel)
            {
                parts.Add(state.InfoLabelKind == PrescriptionInfoLabelKind.SentTo ? "Sent To" : "Missing");
            }
        }

        if (!hasPdf)
        {
            parts.Add("preview only");
        }

        return string.Join(" · ", parts);
    }

    private void OnPreviousPrescriptionPanNumberSelected(object? sender, string panNumber)
    {
        try
        {
            ReloadPrescriptionFromPreviousNumber(panNumber);
            _prescriptionPreviousNumbersWindow?.Close();
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
            ShowMessageBox("Error", $"Could not reload prescription: {ex.Message}", SMessageBoxButtons.Ok, NotificationIcon.Error, 15, MainWindow.Instance);
        }
    }

    private void ReloadPrescriptionFromPreviousNumber(string panNumber)
    {
        string finalLocation = ReadLocalSetting("FinalPrescriptionsFolder");
        string pngPath = Path.Combine(finalLocation, DateTime.Now.ToString("MM-dd"), panNumber + ".png");
        if (!File.Exists(pngPath))
        {
            ShowMessageBox("Not found", $"No saved prescription image found for {panNumber} today.", SMessageBoxButtons.Ok, NotificationIcon.Warning, 12, MainWindow.Instance);
            return;
        }

        PrescriptionSavedState? state = PrescriptionSavedStateStore.TryLoad(PDFTemp, panNumber);
        string currentPdf = state?.CurrentPdfPath ?? PDFTemp + "\\" + panNumber + ".pdf";
        if (!File.Exists(currentPdf))
        {
            currentPdf = PDFTemp + "\\" + panNumber + ".pdf";
        }

        if (!File.Exists(currentPdf) && !File.Exists(PDFTemp + "\\" + panNumber + "-original.pdf"))
        {
            using Bitmap previewOnly = new(pngPath);
            PmSavedPrescription = ImageSourceFromBitmap(new Bitmap(previewOnly));
            WriteLocalSetting("LastPanNumber", panNumber);
            ShowPrescriptionPreviewPanel();
            ShowNotificationMessage(
                "Preview only",
                $"Pan {panNumber} image loaded, but temp PDFs are missing so stamp editing is unavailable.",
                NotificationIcon.Warning);
            return;
        }

        ClearPrescriptionPanEditSession();
        ClearPrescriptionRushEditSession();
        ClearPrescriptionInfoEditSession();

        WriteLocalSetting("LastPanNumber", panNumber);
        if (File.Exists(currentPdf))
        {
            WriteLocalSetting("LastFile", currentPdf);
        }

        if (state is not null)
        {
            PageHeaderIsHigh = state.StampHeader;
            _prescriptionStateHasRush = state.HasRush;
            _prescriptionStateHasInfoLabel = state.HasInfoLabel;
            _prescriptionStateInfoLabelKind = state.InfoLabelKind;
            _prescriptionStateInfoLabelText = state.InfoLabelText;
            RestorePrescriptionEditSessionsFromState(state);
            ApplyPrescriptionActionButtonVisibilityInstance(state);
            if (IsExistingPdfPath(state.OriginalPdfPath))
            {
                RememberReopenablePrescriptionPdfPath(state.OriginalPdfPath);
            }
        }
        else
        {
            PageHeaderIsHigh = ResolvePrescriptionStampHeader();
            PmRushButtonShows = Visibility.Visible;
            PmSendToButtonShows = Visibility.Visible;
            PmMissingButtonShows = Visibility.Visible;

            string originalPath = PDFTemp + "\\" + panNumber + "-original.pdf";
        if (File.Exists(originalPath))
        {
            BeginPrescriptionPanEditSessionFromExisting(originalPath, panNumber, PageHeaderIsHigh);
            RememberReopenablePrescriptionPdfPath(originalPath);
        }
        }

        using Bitmap img = new(pngPath);
        PmSavedPrescription = ImageSourceFromBitmap(new Bitmap(img));
        ShowPrescriptionPreviewPanel();
        RefreshCanReopenLastPrescriptionPdf();
    }

    private void RestorePrescriptionEditSessionsFromState(PrescriptionSavedState state)
    {
        if (File.Exists(state.OriginalPdfPath))
        {
            BeginPrescriptionPanEditSessionFromExisting(state.OriginalPdfPath, state.PanNumber, state.StampHeader);
        }

        if (state.HasRush && File.Exists(state.PanOnlyPdfPath))
        {
            BeginPrescriptionRushEditSession(state.PanOnlyPdfPath, state.PanNumber, state.StampHeader);
        }

        if (state.HasInfoLabel && File.Exists(state.PreInfoPdfPath) && !string.IsNullOrWhiteSpace(state.InfoLabelText))
        {
            BeginPrescriptionInfoEditSession(state.PreInfoPdfPath, state.PanNumber, state.StampHeader, state.InfoLabelText);
        }
    }

    private void ApplyPrescriptionActionButtonVisibilityInstance(PrescriptionSavedState state)
    {
        PmRushButtonShows = state.HasRush ? Visibility.Hidden : Visibility.Visible;
        if (state.HasInfoLabel)
        {
            PmSendToButtonShows = Visibility.Hidden;
            PmMissingButtonShows = Visibility.Hidden;
        }
        else
        {
            PmSendToButtonShows = Visibility.Visible;
            PmMissingButtonShows = Visibility.Visible;
        }
    }

    private void BeginPrescriptionPanEditSessionFromExisting(string originalPdfPath, string panNumber, string stampHeader)
    {
        _prescriptionPanEditSession = new PrescriptionPanEditSession
        {
            Kind = PrescriptionStampEditKind.PanNumber,
            OriginalPdfPath = originalPdfPath,
            PanNumber = panNumber,
            StampHeader = PrescriptionSystemCatalog.NormalizeCode(stampHeader),
            CanEdit = true,
        };
        PmCanEditPanPosition = Visibility.Visible;
    }

    internal void PersistPrescriptionSavedState(
        string panNumber,
        string stampHeader,
        bool hasPan,
        bool hasRush,
        bool hasInfoLabel,
        PrescriptionInfoLabelKind infoLabelKind,
        string infoLabelText,
        string? previewPngPath = null)
    {
        string currentPdf = PDFTemp + "\\" + panNumber + ".pdf";
        string originalPdf = PDFTemp + "\\" + panNumber + "-original.pdf";
        string panOnlyPdf = PDFTemp + "\\" + panNumber + "-pan-only.pdf";
        string preInfoPdf = PDFTemp + "\\" + panNumber + "-pre-info.pdf";

        if (string.IsNullOrWhiteSpace(previewPngPath))
        {
            string finalLocation = ReadLocalSetting("FinalPrescriptionsFolder");
            previewPngPath = Path.Combine(finalLocation, DateTime.Now.ToString("MM-dd"), panNumber + ".png");
        }

        PrescriptionSavedStateStore.Save(PDFTemp, new PrescriptionSavedState
        {
            PanNumber = panNumber,
            StampHeader = PrescriptionSystemCatalog.NormalizeCode(stampHeader),
            SavedAt = DateTime.Now,
            HasPan = hasPan,
            HasRush = hasRush,
            HasInfoLabel = hasInfoLabel,
            InfoLabelKind = infoLabelKind,
            InfoLabelText = infoLabelText,
            CurrentPdfPath = File.Exists(currentPdf) ? currentPdf : "",
            OriginalPdfPath = File.Exists(originalPdf) ? originalPdf : "",
            PanOnlyPdfPath = File.Exists(panOnlyPdf) ? panOnlyPdf : "",
            PreInfoPdfPath = File.Exists(preInfoPdf) ? preInfoPdf : "",
            PreviewPngPath = previewPngPath,
        });
    }
}
