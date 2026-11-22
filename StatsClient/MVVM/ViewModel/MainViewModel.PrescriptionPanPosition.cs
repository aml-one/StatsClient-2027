using StatsClient.MVVM.Core;
using StatsClient.MVVM.Model;
using StatsClient.MVVM.View;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Parsing;
using Syncfusion.PdfToImageConverter;
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
    private PrescriptionPanEditSession? _prescriptionPanEditSession;
    private PrescriptionPanEditSession? _prescriptionRushEditSession;
    private PrescriptionPanEditSession? _prescriptionInfoEditSession;
    private PrescriptionPanPositionEditorWindow? _prescriptionStampPositionEditorWindow;

    private bool _prescriptionStateHasRush;
    private bool _prescriptionStateHasInfoLabel;
    private PrescriptionInfoLabelKind _prescriptionStateInfoLabelKind = PrescriptionInfoLabelKind.None;
    private string _prescriptionStateInfoLabelText = "";

    private Visibility _pmCanEditPanPosition = Visibility.Collapsed;
    public Visibility PmCanEditPanPosition
    {
        get => _pmCanEditPanPosition;
        set
        {
            _pmCanEditPanPosition = value;
            RaisePropertyChanged(nameof(PmCanEditPanPosition));
        }
    }

    private Visibility _pmCanEditRushPosition = Visibility.Collapsed;
    public Visibility PmCanEditRushPosition
    {
        get => _pmCanEditRushPosition;
        set
        {
            _pmCanEditRushPosition = value;
            RaisePropertyChanged(nameof(PmCanEditRushPosition));
        }
    }

    private Visibility _pmCanEditInfoLabelPosition = Visibility.Collapsed;
    public Visibility PmCanEditInfoLabelPosition
    {
        get => _pmCanEditInfoLabelPosition;
        set
        {
            _pmCanEditInfoLabelPosition = value;
            RaisePropertyChanged(nameof(PmCanEditInfoLabelPosition));
        }
    }

    public RelayCommand EditPrescriptionPanPositionCommand { get; private set; } = null!;
    public RelayCommand EditPrescriptionRushPositionCommand { get; private set; } = null!;
    public RelayCommand EditPrescriptionInfoLabelPositionCommand { get; private set; } = null!;
    public RelayCommand DismissPrescriptionProcessingOverlayCommand { get; private set; } = null!;

    private void WirePrescriptionPanPositionCommands()
    {
        EditPrescriptionPanPositionCommand = new RelayCommand(_ => OpenPrescriptionStampPositionEditor(_prescriptionPanEditSession));
        EditPrescriptionRushPositionCommand = new RelayCommand(_ => OpenPrescriptionStampPositionEditor(_prescriptionRushEditSession));
        EditPrescriptionInfoLabelPositionCommand = new RelayCommand(_ => OpenPrescriptionStampPositionEditor(_prescriptionInfoEditSession));
        DismissPrescriptionProcessingOverlayCommand = new RelayCommand(_ => ProcessingDigiPrescriptionNow = Visibility.Collapsed);
        WirePrescriptionPreviousNumbersCommand();
        WireShowOrderCreationWindowCommand();
        RefreshCanReopenLastPrescriptionPdf();
    }

    private void BeginPrescriptionPanEditSession(string originalPdfPath, string panNumber, string stampHeader)
    {
        try
        {
            string sessionPath = PDFTemp + "\\" + panNumber + "-original.pdf";
            File.Copy(originalPdfPath, sessionPath, overwrite: true);
            RememberReopenablePrescriptionPdfPath(sessionPath);

            _prescriptionPanEditSession = new PrescriptionPanEditSession
            {
                Kind = PrescriptionStampEditKind.PanNumber,
                OriginalPdfPath = sessionPath,
                PanNumber = panNumber,
                StampHeader = PrescriptionSystemCatalog.NormalizeCode(stampHeader),
                OffsetX = 0,
                OffsetY = 0,
                FontSizeDelta = 0,
                CanEdit = true,
            };
            PmCanEditPanPosition = Visibility.Visible;
            InvalidatePrescriptionRushEditSession();
            InvalidatePrescriptionInfoEditSession();
            _prescriptionStateHasRush = false;
            _prescriptionStateHasInfoLabel = false;
            _prescriptionStateInfoLabelKind = PrescriptionInfoLabelKind.None;
            _prescriptionStateInfoLabelText = "";
            PersistPrescriptionSavedState(panNumber, stampHeader, hasPan: true, hasRush: false, hasInfoLabel: false, PrescriptionInfoLabelKind.None, "");
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
            _prescriptionPanEditSession = null;
            PmCanEditPanPosition = Visibility.Collapsed;
        }
    }

    private void BeginPrescriptionRushEditSession(string panOnlyPdfPath, string panNumber, string stampHeader)
    {
        try
        {
            _prescriptionRushEditSession = new PrescriptionPanEditSession
            {
                Kind = PrescriptionStampEditKind.Rush,
                OriginalPdfPath = panOnlyPdfPath,
                PanNumber = panNumber,
                StampHeader = PrescriptionSystemCatalog.NormalizeCode(stampHeader),
                OffsetX = 0,
                OffsetY = 0,
                FontSizeDelta = 0,
                CanEdit = true,
            };
            PmCanEditRushPosition = Visibility.Visible;
            _prescriptionStateHasRush = true;
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
            _prescriptionRushEditSession = null;
            PmCanEditRushPosition = Visibility.Collapsed;
        }
    }

    internal void BeginPrescriptionInfoEditSession(string preInfoPdfPath, string panNumber, string stampHeader, string infoText)
    {
        try
        {
            _prescriptionInfoEditSession = new PrescriptionPanEditSession
            {
                Kind = PrescriptionStampEditKind.InfoLabel,
                OriginalPdfPath = preInfoPdfPath,
                PanNumber = panNumber,
                StampHeader = PrescriptionSystemCatalog.NormalizeCode(stampHeader),
                InfoDisplayText = infoText,
                OffsetX = 0,
                OffsetY = 0,
                FontSizeDelta = 0,
                CanEdit = true,
            };
            PmCanEditInfoLabelPosition = Visibility.Visible;
            _prescriptionStateHasInfoLabel = true;
            _prescriptionStateInfoLabelText = infoText;
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
            _prescriptionInfoEditSession = null;
            PmCanEditInfoLabelPosition = Visibility.Collapsed;
        }
    }

    private void InvalidatePrescriptionInfoEditSession()
    {
        if (_prescriptionInfoEditSession is not null)
        {
            _prescriptionInfoEditSession.CanEdit = false;
        }

        PmCanEditInfoLabelPosition = Visibility.Collapsed;
    }

    private void ClearPrescriptionInfoEditSession()
    {
        _prescriptionInfoEditSession = null;
        PmCanEditInfoLabelPosition = Visibility.Collapsed;
    }

    private void InvalidatePrescriptionPanEditSession()
    {
        if (_prescriptionPanEditSession is not null)
        {
            _prescriptionPanEditSession.CanEdit = false;
        }

        PmCanEditPanPosition = Visibility.Collapsed;
    }

    private void InvalidatePrescriptionRushEditSession()
    {
        if (_prescriptionRushEditSession is not null)
        {
            _prescriptionRushEditSession.CanEdit = false;
        }

        PmCanEditRushPosition = Visibility.Collapsed;
    }

    private void ClearPrescriptionPanEditSession()
    {
        _prescriptionPanEditSession = null;
        PmCanEditPanPosition = Visibility.Collapsed;
    }

    private void ClearPrescriptionRushEditSession()
    {
        _prescriptionRushEditSession = null;
        PmCanEditRushPosition = Visibility.Collapsed;
    }

    private void OpenPrescriptionStampPositionEditor(PrescriptionPanEditSession? session)
    {
        if (_MainWindow is null || session is null || !session.CanEdit)
        {
            return;
        }

        if (!File.Exists(session.OriginalPdfPath))
        {
            ShowMessageBox("Error", "Prescription PDF is no longer available.", SMessageBoxButtons.Ok, NotificationIcon.Warning, 15, MainWindow.Instance);
            if (session.Kind == PrescriptionStampEditKind.Rush)
            {
                ClearPrescriptionRushEditSession();
            }
            else if (session.Kind == PrescriptionStampEditKind.InfoLabel)
            {
                ClearPrescriptionInfoEditSession();
            }
            else
            {
                ClearPrescriptionPanEditSession();
            }

            return;
        }

        if (_prescriptionStampPositionEditorWindow is not null)
        {
            _prescriptionStampPositionEditorWindow.Close();
            _prescriptionStampPositionEditorWindow = null;
        }

        try
        {
            _prescriptionStampPositionEditorWindow = new PrescriptionPanPositionEditorWindow(_MainWindow);
            _prescriptionStampPositionEditorWindow.Initialize(session);
            _prescriptionStampPositionEditorWindow.SaveRequested += OnStampPositionEditorSaveRequested;
            _prescriptionStampPositionEditorWindow.SaveAsDefaultRequested += OnStampPositionEditorSaveAsDefaultRequested;
            _prescriptionStampPositionEditorWindow.Closed += (_, _) =>
            {
                if (_prescriptionStampPositionEditorWindow is not null)
                {
                    _prescriptionStampPositionEditorWindow.SaveRequested -= OnStampPositionEditorSaveRequested;
                    _prescriptionStampPositionEditorWindow.SaveAsDefaultRequested -= OnStampPositionEditorSaveAsDefaultRequested;
                }

                _prescriptionStampPositionEditorWindow = null;
            };
            _prescriptionStampPositionEditorWindow.Show();
            _prescriptionStampPositionEditorWindow.Activate();
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
            ShowMessageBox("Error", $"Could not open position editor: {ex.Message}", SMessageBoxButtons.Ok, NotificationIcon.Error, 15, MainWindow.Instance);
        }
    }

    private void OnStampPositionEditorSaveRequested(object? sender, PrescriptionPanEditSession session)
    {
        PrescriptionPanEditSession? liveSession = GetLiveStampEditSession(session.Kind);
        if (liveSession is null)
        {
            return;
        }

        liveSession.OffsetX = session.OffsetX;
        liveSession.OffsetY = session.OffsetY;
        liveSession.FontSizeDelta = session.FontSizeDelta;

        QueuePrescriptionStampSave(SnapshotStampSession(liveSession));
    }

    private void OnStampPositionEditorSaveAsDefaultRequested(object? sender, PrescriptionPanEditSession session)
    {
        if (session.Kind == PrescriptionStampEditKind.Rush)
        {
            QueueRushPositionSaveAsDefault(session);
            return;
        }

        if (session.Kind == PrescriptionStampEditKind.InfoLabel)
        {
            QueueInfoLabelPositionSaveAsDefault(session);
            return;
        }

        QueuePanPositionSaveAsDefault(session);
    }

    private void QueuePanPositionSaveAsDefault(PrescriptionPanEditSession session)
    {
        if (_prescriptionPanEditSession is null)
        {
            return;
        }

        try
        {
            using PdfLoadedDocument doc = new(_prescriptionPanEditSession.OriginalPdfPath);
            PdfLoadedPage page = (doc.Pages[0] as PdfLoadedPage)!;
            SizeF pageSize = page.Graphics.ClientSize;

            PointF origin = PrescriptionPanPositionResolver.ResolveOrigin(
                session.StampHeader,
                pageSize.Width,
                pageSize.Height,
                session.OffsetX,
                session.OffsetY);

            PrescriptionPanPositionStore.SaveNormalized(
                session.StampHeader,
                origin.X / pageSize.Width,
                origin.Y / pageSize.Height);

            float fontSize = PrescriptionPanPositionResolver.ResolveFontSize(
                session.StampHeader,
                pageSize.Height,
                session.FontSizeDelta);
            PrescriptionPanPositionStore.SaveFontSize(session.StampHeader, fontSize);

            _prescriptionPanEditSession.OffsetX = 0;
            _prescriptionPanEditSession.OffsetY = 0;
            _prescriptionPanEditSession.FontSizeDelta = 0;

            string systemName = PrescriptionSystemCatalog.DisplayName(session.StampHeader);
            QueuePrescriptionStampSave(
                SnapshotStampSession(_prescriptionPanEditSession),
                $"Default pan number position and font size saved for {systemName}.");
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
            ShowMessageBox("Error", $"{ex.Message}", SMessageBoxButtons.Ok, NotificationIcon.Error, 15, MainWindow.Instance);
        }
    }

    private void QueueRushPositionSaveAsDefault(PrescriptionPanEditSession session)
    {
        if (_prescriptionRushEditSession is null)
        {
            return;
        }

        try
        {
            using PdfLoadedDocument doc = new(_prescriptionRushEditSession.OriginalPdfPath);
            PdfLoadedPage page = (doc.Pages[0] as PdfLoadedPage)!;
            SizeF pageSize = page.Graphics.ClientSize;

            PointF origin = PrescriptionRushPositionResolver.ResolveOrigin(
                session.StampHeader,
                pageSize.Width,
                pageSize.Height,
                session.OffsetX,
                session.OffsetY);

            PrescriptionPanPositionStore.SaveRushNormalized(
                session.StampHeader,
                origin.X / pageSize.Width,
                origin.Y / pageSize.Height);

            float fontSize = PrescriptionRushPositionResolver.ResolveFontSize(
                session.StampHeader,
                pageSize.Height,
                session.FontSizeDelta);
            PrescriptionPanPositionStore.SaveRushFontSize(session.StampHeader, fontSize);

            _prescriptionRushEditSession.OffsetX = 0;
            _prescriptionRushEditSession.OffsetY = 0;
            _prescriptionRushEditSession.FontSizeDelta = 0;

            string systemName = PrescriptionSystemCatalog.DisplayName(session.StampHeader);
            QueuePrescriptionStampSave(
                SnapshotStampSession(_prescriptionRushEditSession),
                $"Default RUSH position and font size saved for {systemName}.");
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
            ShowMessageBox("Error", $"{ex.Message}", SMessageBoxButtons.Ok, NotificationIcon.Error, 15, MainWindow.Instance);
        }
    }

    private void QueueInfoLabelPositionSaveAsDefault(PrescriptionPanEditSession session)
    {
        if (_prescriptionInfoEditSession is null)
        {
            return;
        }

        try
        {
            using PdfLoadedDocument doc = new(_prescriptionInfoEditSession.OriginalPdfPath);
            PdfLoadedPage page = (doc.Pages[0] as PdfLoadedPage)!;
            SizeF pageSize = page.Graphics.ClientSize;

            PointF origin = PrescriptionInfoPositionResolver.ResolveOrigin(
                session.StampHeader,
                pageSize.Width,
                pageSize.Height,
                session.OffsetX,
                session.OffsetY);

            PrescriptionPanPositionStore.SaveInfoNormalized(
                session.StampHeader,
                origin.X / pageSize.Width,
                origin.Y / pageSize.Height);

            float fontSize = PrescriptionInfoPositionResolver.ResolveFontSize(
                session.StampHeader,
                pageSize.Height,
                session.InfoDisplayText,
                session.FontSizeDelta);
            PrescriptionPanPositionStore.SaveInfoFontSize(session.StampHeader, fontSize);

            _prescriptionInfoEditSession.OffsetX = 0;
            _prescriptionInfoEditSession.OffsetY = 0;
            _prescriptionInfoEditSession.FontSizeDelta = 0;

            string systemName = PrescriptionSystemCatalog.DisplayName(session.StampHeader);
            QueuePrescriptionStampSave(
                SnapshotStampSession(_prescriptionInfoEditSession),
                $"Default label position and font size saved for {systemName}.");
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
            ShowMessageBox("Error", $"{ex.Message}", SMessageBoxButtons.Ok, NotificationIcon.Error, 15, MainWindow.Instance);
        }
    }

    private PrescriptionPanEditSession? GetLiveStampEditSession(PrescriptionStampEditKind kind) =>
        kind switch
        {
            PrescriptionStampEditKind.Rush => _prescriptionRushEditSession,
            PrescriptionStampEditKind.InfoLabel => _prescriptionInfoEditSession,
            _ => _prescriptionPanEditSession,
        };

    private static PrescriptionPanEditSession SnapshotStampSession(PrescriptionPanEditSession session) =>
        new()
        {
            Kind = session.Kind,
            OriginalPdfPath = session.OriginalPdfPath,
            PanNumber = session.PanNumber,
            StampHeader = session.StampHeader,
            InfoDisplayText = session.InfoDisplayText,
            OffsetX = session.OffsetX,
            OffsetY = session.OffsetY,
            FontSizeDelta = session.FontSizeDelta,
            CanEdit = session.CanEdit,
        };

    private void QueuePrescriptionStampSave(PrescriptionPanEditSession snapshot, string? successNotification = null)
    {
        ClosePrescriptionStampPositionEditorImmediately();
        _ = ApplyPrescriptionStampSaveInBackgroundAsync(snapshot, successNotification);
    }

    private void ClosePrescriptionStampPositionEditorImmediately()
    {
        try
        {
            if (_prescriptionStampPositionEditorWindow is not null)
            {
                _prescriptionStampPositionEditorWindow.SaveRequested -= OnStampPositionEditorSaveRequested;
                _prescriptionStampPositionEditorWindow.SaveAsDefaultRequested -= OnStampPositionEditorSaveAsDefaultRequested;
                _prescriptionStampPositionEditorWindow.Close();
            }
        }
        catch
        {
        }

        _prescriptionStampPositionEditorWindow = null;
    }

    private async Task ApplyPrescriptionStampSaveInBackgroundAsync(PrescriptionPanEditSession snapshot, string? successNotification)
    {
        await Application.Current.Dispatcher.InvokeAsync(() =>
            ProcessingDigiPrescriptionNow = Visibility.Visible);

        try
        {
            string savedPdf = PDFTemp + "\\" + snapshot.PanNumber + ".pdf";
            string savedPdfCopy = PDFTemp + "\\" + snapshot.PanNumber + "-copy.pdf";
            string finalLocation = ReadLocalSetting("FinalPrescriptionsFolder");

            await Task.Run(() => WriteStampedPrescriptionPdf(snapshot, savedPdf, savedPdfCopy));

            (string photoLocation, int pageCount) = await Task.Run(() =>
                ExportPrescriptionStampPreviewPng(savedPdfCopy, snapshot.PanNumber, finalLocation, snapshot.StampHeader));

            if (string.IsNullOrEmpty(photoLocation))
            {
                throw new InvalidOperationException("Prescription preview image could not be created.");
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                WriteLocalSetting("LastFile", savedPdf);
                WriteLocalSetting("LastPanNumber", snapshot.PanNumber);
                PdfPageCount = pageCount.ToString();

                using Bitmap img = new(photoLocation);
                PmSavedPrescription = ImageSourceFromBitmap(new Bitmap(img));

                if (!string.IsNullOrWhiteSpace(finalLocation))
                {
                    TriggerSironaFolderRename(snapshot.PanNumber, finalLocation);
                }

                if (!string.IsNullOrWhiteSpace(successNotification))
                {
                    ShowNotificationMessage("Saved", successNotification, NotificationIcon.Success);
                }

                PersistPrescriptionSavedState(
                    snapshot.PanNumber,
                    snapshot.StampHeader,
                    hasPan: _prescriptionPanEditSession?.CanEdit == true || File.Exists(PDFTemp + "\\" + snapshot.PanNumber + "-original.pdf"),
                    hasRush: _prescriptionStateHasRush,
                    hasInfoLabel: _prescriptionStateHasInfoLabel,
                    _prescriptionStateInfoLabelKind,
                    _prescriptionStateInfoLabelText,
                    photoLocation);
            });
        }
        catch (Exception ex)
        {
            AddDebugLine(ex);
            await Application.Current.Dispatcher.InvokeAsync(() =>
                ShowMessageBox(
                    "Error",
                    $"Could not update prescription: {ex.Message}",
                    SMessageBoxButtons.Ok,
                    NotificationIcon.Error,
                    15,
                    MainWindow.Instance));
        }
        finally
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
                ProcessingDigiPrescriptionNow = Visibility.Collapsed);
        }
    }

    private static void WriteStampedPrescriptionPdf(PrescriptionPanEditSession session, string savedPdf, string savedPdfCopy)
    {
        PdfLoadedDocument doc = new(session.OriginalPdfPath);
        try
        {
            PdfLoadedPage page = (doc.Pages[0] as PdfLoadedPage)!;
            PdfGraphics graphics = page.Graphics;

            if (session.Kind == PrescriptionStampEditKind.Rush)
            {
                DrawPrescriptionRush(
                    graphics,
                    page,
                    session.StampHeader,
                    session.OffsetX,
                    session.OffsetY,
                    session.FontSizeDelta);
            }
            else if (session.Kind == PrescriptionStampEditKind.InfoLabel)
            {
                DrawPrescriptionInfoText(
                    graphics,
                    page,
                    session.StampHeader,
                    session.InfoDisplayText,
                    session.OffsetX,
                    session.OffsetY,
                    session.FontSizeDelta);
            }
            else
            {
                DrawPrescriptionPanNumber(
                    graphics,
                    page,
                    session.StampHeader,
                    session.PanNumber,
                    session.OffsetX,
                    session.OffsetY,
                    session.FontSizeDelta);
            }

            doc.Save(savedPdf);
            doc.Save(savedPdfCopy);
        }
        finally
        {
            doc.Close(true);
        }
    }

    private (string PhotoLocation, int PageCount) ExportPrescriptionStampPreviewPng(
        string savedPdfCopy,
        string panNumber,
        string finalLocation,
        string stampHeader)
    {
        using PdfToImageConverter converter = new();
        using FileStream inputStream = File.OpenRead(savedPdfCopy);
        converter.Load(inputStream);
        Stream[] outputStream = converter.Convert(0, converter.PageCount - 1, 200, 200, false, false);

        try
        {
            int count = outputStream.Length;

            for (int i = 0; i < count; i++)
            {
                using Bitmap image = new(outputStream[i]);
                image.Save($@"{DataBaseFolder}Temp\{panNumber}-{i}.png", System.Drawing.Imaging.ImageFormat.Png);
            }

            if (string.IsNullOrWhiteSpace(finalLocation))
            {
                return ($@"{DataBaseFolder}Temp\{panNumber}-0.png", count);
            }

            string photoLocation = finalLocation + "\\" + DateTime.Now.ToString("MM-dd") + "\\" + panNumber + ".png";
            Directory.CreateDirectory(Path.GetDirectoryName(photoLocation)!);

            if (count == 1)
            {
                using Bitmap image = new(outputStream[0]);
                TryDeleteFile(photoLocation);
                image.Save(photoLocation, System.Drawing.Imaging.ImageFormat.Png);
                return (photoLocation, count);
            }

            List<string> files = [];
            for (int i = 0; i < count; i++)
            {
                using Bitmap image = new(outputStream[i]);
                using Bitmap bitImage = Crop(image, stampHeader);
                string file = DataBaseFolder + "Temp\\" + panNumber + "-" + i + ".png";
                bitImage.Save(file, System.Drawing.Imaging.ImageFormat.Png);
                files.Add(file);
            }

            TryDeleteFile(photoLocation);
            CombineImages(files).Save(photoLocation, System.Drawing.Imaging.ImageFormat.Png);
            return (photoLocation, count);
        }
        finally
        {
            foreach (Stream stream in outputStream)
            {
                stream.Dispose();
            }
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    internal void ClosePrescriptionPanPositionEditorForShutdown()
    {
        try
        {
            _prescriptionStampPositionEditorWindow?.Close();
        }
        catch
        {
        }

        _prescriptionStampPositionEditorWindow = null;
        _prescriptionPanEditSession = null;
        _prescriptionRushEditSession = null;
        _prescriptionInfoEditSession = null;
    }
}
