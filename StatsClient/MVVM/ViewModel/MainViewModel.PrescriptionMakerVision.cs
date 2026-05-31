using StatsClient.MVVM.Core;
using StatsClient.MVVM.View;
using System.Windows;
using System.Windows.Threading;
using static StatsClient.MVVM.Core.Enums;

namespace StatsClient.MVVM.ViewModel;

public partial class MainViewModel
{
    private readonly PanStackVisionService _panStackVisionService = new();
    private CancellationTokenSource? _panStackVisionCts;
    private bool _isPanStackVisionRunning;

    public bool IsPanStackVisionRunning
    {
        get => _isPanStackVisionRunning;
        private set
        {
            if (_isPanStackVisionRunning == value)
            {
                return;
            }

            _isPanStackVisionRunning = value;
            RaisePropertyChanged(nameof(IsPanStackVisionRunning));
        }
    }

    public void HandlePrescriptionMakerImageReceived(byte[] imagePng)
    {
        if (imagePng.Length == 0 || _isPanStackVisionRunning)
        {
            return;
        }

        SMessageBoxResult confirm = ShowMessageBox(
            "Image detected",
            "Do you want to parse it to get the pan numbers from it?",
            SMessageBoxButtons.YesNo,
            NotificationIcon.Question,
            120,
            _MainWindow);

        if (confirm != SMessageBoxResult.Yes)
        {
            return;
        }

        _ = ParseAndReviewPanNumbersFromImageAsync(imagePng);
    }

    private async Task ParseAndReviewPanNumbersFromImageAsync(byte[] imagePng)
    {
        _panStackVisionCts?.Cancel();
        _panStackVisionCts = new CancellationTokenSource();
        var cancellationToken = _panStackVisionCts.Token;
        var ui = Application.Current.Dispatcher;

        SetPanStackVisionRunningOnUi(ui, this, true);
        PanStackVisionReviewWindow? reviewWindow = null;
        var acceptTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            var stackCrop = await ui.InvokeAsync(() =>
                PanStackVisionImageHelper.CropToPanStackRegion(imagePng)).Task.ConfigureAwait(true);
            var visionImagePng = stackCrop.ImagePng;

            reviewWindow = await ui.InvokeAsync(() =>
            {
                var window = new PanStackVisionReviewWindow(_MainWindow);
                window.LoadImage(visionImagePng);
                window.SetStatus(stackCrop.WasCropped
                    ? "Cropped to pan stack — preparing sections…"
                    : "Preparing image sections…");
                window.Closed += (_, _) =>
                {
                    if (!acceptTcs.Task.IsCompleted)
                    {
                        acceptTcs.TrySetResult(window.UserAccepted);
                    }

                    if (!window.UserAccepted)
                    {
                        _panStackVisionCts?.Cancel();
                    }
                };
                window.Show();
                return window;
            }).Task.ConfigureAwait(true);

            var loadingSteps = await ui.InvokeAsync(() =>
                Math.Max(1, _panStackVisionService.BuildSections(visionImagePng).Count)).Task.ConfigureAwait(true);

            await ui.InvokeAsync(() =>
            {
                reviewWindow?.BeginLoading(loadingSteps);
                reviewWindow?.SetStatus("AI vision — reading pan labels…");
            }).Task.ConfigureAwait(true);

            var progress = new Progress<PanStackVisionProgress>(update =>
            {
                if (reviewWindow is null)
                {
                    return;
                }

                ui.InvokeAsync(() =>
                {
                    reviewWindow.UpdateLoadingProgress(update);
                    if (update.PartialColumns is { Count: > 0 })
                    {
                        reviewWindow.ApplyPartialColumns(
                            update.PartialColumns,
                            update.LabelsReadSoFar,
                            update.Message);
                    }

                    if (update.IsComplete && update.PartialColumns is { Count: > 0 })
                    {
                        reviewWindow.InitializeMatrixFromColumns(update.PartialColumns);
                        reviewWindow.CompleteParsing(
                            update.LabelsReadSoFar,
                            update.SkippedLowConfidenceSoFar);
                    }
                });
            });

            PanStackVisionParseResult parseResult = await _panStackVisionService
                .ParsePanStackImageAsync(visionImagePng, cancellationToken, progress)
                .ConfigureAwait(true);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!parseResult.IsSuccess)
            {
                await ui.InvokeAsync(() => reviewWindow?.ShowParseError(parseResult.ErrorSummary))
                    .Task.ConfigureAwait(true);
                return;
            }

            var totalRead = parseResult.Columns.Sum(column => column.Labels.Count);
            var sectionWarning = string.IsNullOrWhiteSpace(parseResult.ErrorSummary)
                ? string.Empty
                : $" Some sections failed: {parseResult.ErrorSummary}";
            await ui.InvokeAsync(() =>
            {
                reviewWindow?.InitializeMatrixFromColumns(parseResult.Columns);
                reviewWindow?.CompleteParsing(totalRead, parseResult.SkippedLowConfidenceCount);
                reviewWindow?.EndLoading(
                    $"Review the matrix. {totalRead} label(s) read.{sectionWarning} Fill gaps, then Accept.");
            }).Task.ConfigureAwait(true);

            if (reviewWindow is null || !await acceptTcs.Task.ConfigureAwait(true))
            {
                return;
            }

            if (await ui.InvokeAsync(() => reviewWindow.HasDuplicateNumbers(out _))
                .Task.ConfigureAwait(true))
            {
                ShowMessageBox(
                    "Pan stack vision",
                    "The matrix contains duplicate pan numbers. Each cell must be unique before accepting.",
                    SMessageBoxButtons.Close,
                    NotificationIcon.Warning,
                    20,
                    _MainWindow);
                return;
            }

            var numbers = await ui.InvokeAsync(() => reviewWindow.CollectNumbersInReadOrder())
                .Task.ConfigureAwait(true);

            int added = 0;
            int skipped = 0;
            foreach (string number in numbers)
            {
                int before = PmPanNumberList.Count;
                PmAddNewPanNumber(number);
                if (PmPanNumberList.Count > before || PmPanNumberList.Contains(number))
                {
                    added++;
                }
                else
                {
                    skipped++;
                }
            }

            ShowNotificationMessage(
                "Pan stack vision",
                $"Accepted: {added} added to list, {skipped} skipped (duplicate or not registered).",
                NotificationIcon.Info);
        }
        catch (OperationCanceledException)
        {
            await ui.InvokeAsync(() => reviewWindow?.Close()).Task.ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await ui.InvokeAsync(() =>
            {
                if (reviewWindow is not null)
                {
                    reviewWindow.EndLoading();
                    reviewWindow.ShowParseError(ex.Message);
                }
                else
                {
                    ShowMessageBox(
                        "Pan stack vision",
                        ex.Message,
                        SMessageBoxButtons.Close,
                        NotificationIcon.Error,
                        20,
                        _MainWindow);
                }
            }).Task.ConfigureAwait(true);
        }
        finally
        {
            SetPanStackVisionRunningOnUi(ui, this, false);
        }
    }

    private static void SetPanStackVisionRunningOnUi(Dispatcher ui, MainViewModel viewModel, bool running)
    {
        if (ui.CheckAccess())
        {
            viewModel.IsPanStackVisionRunning = running;
            return;
        }

        ui.Invoke(() => viewModel.IsPanStackVisionRunning = running);
    }
}
