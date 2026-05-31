using StatsClient.MVVM.Core;
using StatsClient.MVVM.Model;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;



namespace StatsClient.MVVM.View;



public partial class PanStackVisionReviewWindow : Window

{

    private const double MinZoom = 0.02;

    private const double MaxZoom = 12.0;

    private const double ZoomStep = 1.12;



    private List<List<TextBox>> _columnCells = [];

    private int _rowCount;

    private int _columnCount;

    private bool _isReviewReady;

    private bool _matrixShellReady;

    /// <summary>True when user clicked Accept; false for Cancel or closing the window.</summary>
    public bool UserAccepted { get; private set; }



    private double _zoom = 1.0;

    private bool _isPanning;

    private Point _panMouseStart;

    private double _scrollStartH;

    private double _scrollStartV;

    private bool _autoZoomApplied;

    private DateTime? _loadingStartUtc;
    private DispatcherTimer? _elapsedTimer;
    private readonly Dictionary<TextBox, string> _lastValidByCell = new();
    private IReadOnlyList<PanStackVisionColumnData>? _lastOverlayColumns;

    private static readonly Brush FilledBrush = new SolidColorBrush(Color.FromRgb(220, 252, 231));
    private static readonly Brush DuplicateBrush = new SolidColorBrush(Color.FromRgb(254, 226, 226));
    private static readonly Brush DuplicateBorderBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38));

    public PanStackVisionReviewWindow(Window? owner)

    {

        InitializeComponent();

        if (owner is not null)

        {

            Owner = owner;

        }



        Loaded += (_, _) => ScheduleInitialZoom();
        ImageViewport.SizeChanged += (_, _) =>
        {
            if (!_autoZoomApplied && SourceImage.Source is not null)
            {
                ScheduleInitialZoom();
            }
        };
    }

    private void ScheduleInitialZoom()
    {
        if (SourceImage.Source is null)
        {
            return;
        }

        if (ImageViewport.ActualWidth < 10 || ImageViewport.ActualHeight < 10)
        {
            Dispatcher.BeginInvoke(ScheduleInitialZoom, DispatcherPriority.Loaded);
            return;
        }

        if (!_autoZoomApplied)
        {
            ApplyDefaultStackZoom();
        }
    }



    public void LoadImage(byte[] pngBytes)

    {

        using var stream = new MemoryStream(pngBytes);

        var image = new BitmapImage();

        image.BeginInit();

        image.CacheOption = BitmapCacheOption.OnLoad;

        image.StreamSource = stream;

        image.EndInit();

        image.Freeze();



        SourceImage.Source = image;
        _zoom = 1.0;
        _autoZoomApplied = false;

        if (IsLoaded)
        {
            ScheduleInitialZoom();
        }
    }



    public void SetStatus(string message) => StatusTextBlock.Text = message;

    public void BeginLoading(int sectionCount)
    {
        _loadingStartUtc = DateTime.UtcNow;
        LoadingPanel.Visibility = Visibility.Visible;
        LoadingProgressBar.IsIndeterminate = true;
        LoadingProgressBar.Value = 0;
        LoadingDetailTextBlock.Text = sectionCount > 0
            ? $"0 of {sectionCount} sections complete"
            : "Calling vision API…";
        StatusTextBlock.Text = "Starting…";
        UpdateElapsedDisplay();

        _elapsedTimer?.Stop();
        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _elapsedTimer.Tick += (_, _) => UpdateElapsedDisplay();
        _elapsedTimer.Start();
    }

    public void UpdateLoadingProgress(PanStackVisionProgress progress)
    {
        LoadingPanel.Visibility = Visibility.Visible;
        UpdateElapsedDisplay();

        if (!string.IsNullOrWhiteSpace(progress.Message))
        {
            StatusTextBlock.Text = progress.Message;
        }

        if (progress.ChunkCount <= 0)
        {
            LoadingProgressBar.IsIndeterminate = true;
            return;
        }

        if (progress.IsComplete)
        {
            LoadingProgressBar.IsIndeterminate = false;
            LoadingProgressBar.Maximum = progress.ChunkCount;
            LoadingProgressBar.Value = progress.ChunkCount;
            LoadingDetailTextBlock.Text = $"All {progress.ChunkCount} sections complete — building matrix…";
            return;
        }

        var completed = progress.SectionsCompleted;
        LoadingProgressBar.IsIndeterminate = completed == 0;
        LoadingProgressBar.Maximum = progress.ChunkCount;
        LoadingProgressBar.Value = completed;

        if (progress.IsSectionStarting)
        {
            LoadingDetailTextBlock.Text =
                $"Working on section {progress.ChunkIndex + 1} of {progress.ChunkCount}… ({completed} done)";
        }
        else
        {
            LoadingDetailTextBlock.Text = $"{completed} of {progress.ChunkCount} sections complete";
        }
    }

    public void EndLoading(string? finalStatus = null)
    {
        _elapsedTimer?.Stop();
        _elapsedTimer = null;
        LoadingPanel.Visibility = Visibility.Collapsed;

        if (_loadingStartUtc is not null)
        {
            var elapsed = DateTime.UtcNow - _loadingStartUtc.Value;
            ElapsedTimeTextBlock.Text = $"Finished in {FormatElapsed(elapsed)}";
            _loadingStartUtc = null;
        }

        if (!string.IsNullOrWhiteSpace(finalStatus))
        {
            StatusTextBlock.Text = finalStatus;
        }
    }

    private void UpdateElapsedDisplay()
    {
        if (_loadingStartUtc is null)
        {
            return;
        }

        ElapsedTimeTextBlock.Text = $"Elapsed {FormatElapsed(DateTime.UtcNow - _loadingStartUtc.Value)}";
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalHours >= 1)
        {
            return $"{(int)elapsed.TotalHours}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
        }

        if (elapsed.TotalMinutes >= 1)
        {
            return $"{elapsed.Minutes}:{elapsed.Seconds:D2}";
        }

        return $"{Math.Max(0, (int)elapsed.TotalSeconds)}s";
    }

    public void ShowParseError(string message)

    {

        EndLoading();
        SetStatus(message);
        AcceptButton.IsEnabled = false;
        AddRowButton.IsEnabled = false;
        AddColumnButton.IsEnabled = false;
    }



    public void ApplyPartialColumns(IReadOnlyList<PanStackVisionColumnData> columns, int labelsRead, string statusMessage)

    {

        var (columnCount, rowCount) = PanStackVisionColumnOrganizer.GetGridDimensions(columns);

        if (!_matrixShellReady)

        {

            InitializeMatrixFixed(columnCount, rowCount);

        }

        else

        {

            EnsureMatrixSize(columnCount, rowCount);

        }



        FillMatrixCells(columns);
        UpdateLabelOverlays(columns);

        SetStatus(statusMessage);



        if (!_autoZoomApplied)
        {
            FitEntireImageInViewer();
            _autoZoomApplied = true;
        }

        PanToLabelColumns(columns);
    }

    public void InitializeMatrixFromColumns(IReadOnlyList<PanStackVisionColumnData> columns)
    {
        var (columnCount, rowCount) = PanStackVisionColumnOrganizer.GetGridDimensions(columns);
        InitializeMatrixFixed(columnCount, rowCount);
        FillMatrixCells(columns);
        UpdateLabelOverlays(columns);

        if (!_autoZoomApplied)
        {
            FitEntireImageInViewer();
            _autoZoomApplied = true;
        }

        PanToLabelColumns(columns);
    }



    public void CompleteParsing(int totalRead, int skippedLowConfidence)

    {

        var skippedNote = skippedLowConfidence > 0

            ? $" ({skippedLowConfidence} low-confidence omitted)"

            : string.Empty;

        SetStatus($"Review the matrix. {totalRead} label(s) read{skippedNote}. Fill gaps, then Accept.");

        _isReviewReady = true;

        AcceptButton.IsEnabled = true;

        AddRowButton.IsEnabled = true;

        AddColumnButton.IsEnabled = true;



        foreach (var column in _columnCells)

        {

            foreach (var cell in column)

            {

                cell.IsEnabled = true;

            }

        }

    }



    private static int MaxRowIndex(IReadOnlyList<PanStackVisionColumnData> columns)
    {
        var max = -1;
        foreach (var column in columns)
        {
            foreach (var label in column.Labels)
            {
                if (label.RowIndex is int row && row > max)
                {
                    max = row;
                }
            }
        }

        if (max < 0)
        {
            max = columns.DefaultIfEmpty().Max(column => (column?.Labels.Count ?? 1) - 1);
        }

        return Math.Max(0, max);
    }

    private void FillMatrixCells(IReadOnlyList<PanStackVisionColumnData> columns)

    {

        foreach (var columnCells in _columnCells)

        {

            foreach (var cell in columnCells)

            {

                cell.Text = string.Empty;

                cell.Background = ColorSchemeResourceCatalog.GetBrush("WhiteBackground");

            }

        }

        var orderedColumns = columns.OrderBy(column => column.ColumnIndex).ToList();
        var placed = new HashSet<(int Col, int Row)>();

        foreach (var column in orderedColumns)
        {
            foreach (var label in column.Labels)
            {
                if (label.GridColumn is int gridCol
                    && label.RowIndex is int gridRow
                    && gridCol >= 0
                    && gridCol < _columnCount
                    && gridRow >= 0
                    && gridRow < _rowCount
                    && placed.Add((gridCol, gridRow)))
                {
                    var cell = _columnCells[gridCol][gridRow];
                    cell.Text = label.Number;
                    cell.Background = FilledBrush;
                    RememberValidCellValue(cell);
                }
            }
        }

        for (var c = 0; c < orderedColumns.Count && c < _columnCount; c++)
        {
            var column = orderedColumns[c];
            for (var r = 0; r < column.Labels.Count && r < _rowCount; r++)
            {
                var label = column.Labels[r];
                if (label.GridColumn.HasValue || label.RowIndex.HasValue)
                {
                    continue;
                }

                if (!placed.Add((c, r)))
                {
                    continue;
                }

                var cell = _columnCells[c][r];
                cell.Text = label.Number;
                cell.Background = FilledBrush;
                RememberValidCellValue(cell);
            }
        }
    }

    private void UpdateLabelOverlays(IReadOnlyList<PanStackVisionColumnData> columns)
    {
        _lastOverlayColumns = columns;
        LabelOverlayCanvas.Children.Clear();

        if (SourceImage.Source is not BitmapSource bitmap)
        {
            return;
        }

        var width = LabelOverlayCanvas.Width > 0 ? LabelOverlayCanvas.Width : bitmap.PixelWidth * _zoom;
        var height = LabelOverlayCanvas.Height > 0 ? LabelOverlayCanvas.Height : bitmap.PixelHeight * _zoom;
        var markerHeight = Math.Clamp(Math.Min(width, height) * 0.032, 16, 34);
        var markerWidth = markerHeight * 1.75;

        foreach (var label in columns.SelectMany(column => column.Labels)
                     .Where(label => label.OverlayCenterX.HasValue && label.OverlayCenterY.HasValue)
                     .OrderBy(label => label.OverlayCenterY)
                     .ThenBy(label => label.OverlayCenterX))
        {
            var cx = label.OverlayCenterX!.Value * width;
            var cy = label.OverlayCenterY!.Value * height;

            var badge = new Border
            {
                Width = markerWidth,
                Height = markerHeight,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromArgb(210, 22, 163, 74)),
                BorderBrush = ColorSchemeResourceCatalog.GetBrush("WhiteBackground"),
                BorderThickness = new Thickness(1.5),
                ToolTip = $"Pan {label.Number} ({label.Confidence:P0})",
                Child = new TextBlock
                {
                    Text = label.Number,
                    FontSize = Math.Clamp(markerHeight * 0.42, 9, 15),
                    FontWeight = FontWeights.Bold,
                    Foreground = ColorSchemeResourceCatalog.GetBrush("WhiteBackground"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                }
            };

            Canvas.SetLeft(badge, cx - markerWidth / 2);
            Canvas.SetTop(badge, cy - markerHeight / 2);
            LabelOverlayCanvas.Children.Add(badge);
        }
    }



    private void EnsureMatrixSize(int columnCount, int rowCount)

    {

        columnCount = Math.Max(1, columnCount);

        rowCount = Math.Max(1, rowCount);

        if (columnCount <= _columnCount && rowCount <= _rowCount)

        {

            return;

        }



        InitializeMatrixFixed(Math.Max(_columnCount, columnCount), Math.Max(_rowCount, rowCount));

    }



    private void InitializeMatrixFixed(int columnCount, int rowCount)

    {

        _columnCount = columnCount;

        _rowCount = rowCount;

        _columnCells = [];
        _lastValidByCell.Clear();

        MatrixGrid.Children.Clear();

        MatrixGrid.RowDefinitions.Clear();

        MatrixGrid.ColumnDefinitions.Clear();



        for (var r = 0; r <= _rowCount; r++)

        {

            MatrixGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        }



        for (var c = 0; c <= _columnCount; c++)

        {

            MatrixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        }



        var corner = CreateHeaderTextBlock(string.Empty);

        Grid.SetRow(corner, 0);

        Grid.SetColumn(corner, 0);

        MatrixGrid.Children.Add(corner);



        for (var c = 0; c < _columnCount; c++)

        {

            var colHeader = CreateHeaderTextBlock($"Col {c + 1}");

            Grid.SetRow(colHeader, 0);

            Grid.SetColumn(colHeader, c + 1);

            MatrixGrid.Children.Add(colHeader);



            var columnCells = new List<TextBox>();

            for (var r = 0; r < _rowCount; r++)

            {

                if (c == 0)

                {

                    var rowHeader = CreateHeaderTextBlock($"{r + 1}");

                    Grid.SetRow(rowHeader, r + 1);

                    Grid.SetColumn(rowHeader, 0);

                    MatrixGrid.Children.Add(rowHeader);

                }



                var cell = CreateMatrixCell();

                Grid.SetRow(cell, r + 1);

                Grid.SetColumn(cell, c + 1);

                MatrixGrid.Children.Add(cell);

                columnCells.Add(cell);

            }



            _columnCells.Add(columnCells);

        }



        _matrixShellReady = true;

    }



    private void ApplyImageLayout()
    {
        if (SourceImage.Source is not BitmapSource bitmap)
        {
            return;
        }

        var width = bitmap.PixelWidth * _zoom;
        var height = bitmap.PixelHeight * _zoom;
        ImageTransformGrid.Width = width;
        ImageTransformGrid.Height = height;
        SourceImage.Width = width;
        SourceImage.Height = height;
        LabelOverlayCanvas.Width = width;
        LabelOverlayCanvas.Height = height;

        if (_lastOverlayColumns is not null)
        {
            UpdateLabelOverlays(_lastOverlayColumns);
        }
    }

    private void CenterImageInViewer()
    {
        ImageScrollViewer.UpdateLayout();
        var h = Math.Max(0, ImageTransformGrid.ActualWidth - ImageScrollViewer.ViewportWidth) / 2;
        var v = Math.Max(0, ImageTransformGrid.ActualHeight - ImageScrollViewer.ViewportHeight) / 2;
        ImageScrollViewer.ScrollToHorizontalOffset(h);
        ImageScrollViewer.ScrollToVerticalOffset(v);
    }

    /// <summary>Show the entire photo in the viewer (no cropping).</summary>
    private void ApplyDefaultStackZoom()
    {
        FitEntireImageInViewer();
        _autoZoomApplied = true;
    }

    private void FitEntireImageInViewer()
    {
        if (SourceImage.Source is not BitmapSource bitmap)
        {
            return;
        }

        var viewportW = ImageScrollViewer.ViewportWidth;
        var viewportH = ImageScrollViewer.ViewportHeight;
        if (viewportW < 10 || viewportH < 10)
        {
            return;
        }

        _zoom = Math.Clamp(
            Math.Min(viewportW / bitmap.PixelWidth, viewportH / bitmap.PixelHeight) * 0.98,
            MinZoom,
            MaxZoom);
        ApplyImageLayout();
        CenterImageInViewer();
    }

    /// <summary>Scroll so detected pan columns are centered (keeps full-image zoom).</summary>
    public void PanToLabelColumns(IReadOnlyList<PanStackVisionColumnData> columns)
    {
        if (SourceImage.Source is not BitmapSource bitmap)
        {
            return;
        }

        var labels = columns.SelectMany(column => column.Labels)
            .Where(label => label.CenterX.HasValue)
            .ToList();
        if (labels.Count == 0 || ImageScrollViewer.ViewportWidth < 10)
        {
            return;
        }

        var minX = labels.Min(label => label.CenterX!.Value);
        var maxX = labels.Max(label => label.CenterX!.Value);
        var centerPx = (minX + maxX) / 2 * bitmap.PixelWidth * _zoom;
        var target = centerPx - ImageScrollViewer.ViewportWidth / 2;
        var maxScroll = Math.Max(0, ImageTransformGrid.ActualWidth - ImageScrollViewer.ViewportWidth);
        ImageScrollViewer.ScrollToHorizontalOffset(Math.Clamp(target, 0, maxScroll));
    }

    private void ImageScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (SourceImage.Source is not BitmapSource bitmap)
        {
            return;
        }

        var mouseInViewer = e.GetPosition(ImageScrollViewer);
        var oldZoom = _zoom;
        var factor = e.Delta > 0 ? ZoomStep : 1 / ZoomStep;
        _zoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);

        var imageX = (mouseInViewer.X + ImageScrollViewer.HorizontalOffset) / oldZoom;
        var imageY = (mouseInViewer.Y + ImageScrollViewer.VerticalOffset) / oldZoom;
        ApplyImageLayout();
        ImageScrollViewer.ScrollToHorizontalOffset(imageX * _zoom - mouseInViewer.X);
        ImageScrollViewer.ScrollToVerticalOffset(imageY * _zoom - mouseInViewer.Y);
        e.Handled = true;
    }

    private void ImageScrollViewer_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _isPanning = true;
        _panMouseStart = e.GetPosition(ImageScrollViewer);
        _scrollStartH = ImageScrollViewer.HorizontalOffset;
        _scrollStartV = ImageScrollViewer.VerticalOffset;
        ImageScrollViewer.CaptureMouse();
        ImageScrollViewer.Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void ImageScrollViewer_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning || e.MiddleButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(ImageScrollViewer);
        var delta = current - _panMouseStart;
        ImageScrollViewer.ScrollToHorizontalOffset(_scrollStartH - delta.X);
        ImageScrollViewer.ScrollToVerticalOffset(_scrollStartV - delta.Y);
        e.Handled = true;
    }



    private void ImageScrollViewer_OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _isPanning = false;
        ImageScrollViewer.ReleaseMouseCapture();
        ImageScrollViewer.Cursor = Cursors.Hand;
        e.Handled = true;
    }



    private static TextBlock CreateHeaderTextBlock(string text) =>

        new()

        {

            Text = text,

            FontWeight = FontWeights.SemiBold,

            FontSize = 11,

            Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),

            Margin = new Thickness(4),

            VerticalAlignment = VerticalAlignment.Center,

            HorizontalAlignment = HorizontalAlignment.Center

        };



    private TextBox CreateMatrixCell()

    {

        var textBox = new TextBox

        {

            Width = 72,

            Height = 28,

            FontSize = 14,

            FontWeight = FontWeights.SemiBold,

            HorizontalContentAlignment = HorizontalAlignment.Center,

            VerticalContentAlignment = VerticalAlignment.Center,

            Margin = new Thickness(3),

            Background = ColorSchemeResourceCatalog.GetBrush("WhiteBackground"),

            IsEnabled = false

        };

        textBox.PreviewTextInput += MatrixCell_PreviewTextInput;
        textBox.TextChanged += MatrixCell_TextChanged;
        textBox.LostFocus += MatrixCell_LostFocus;
        DataObject.AddPastingHandler(textBox, MatrixCell_OnPaste);
        return textBox;
    }

    private static void MatrixCell_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");
    }

    private void MatrixCell_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox cell)
        {
            return;
        }

        ApplyCellStyle(cell);
    }

    private void MatrixCell_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox cell)
        {
            return;
        }

        var digits = NormalizeDigits(cell.Text);
        if (digits.Length == 0)
        {
            cell.Text = string.Empty;
            _lastValidByCell[cell] = string.Empty;
            ApplyCellStyle(cell);
            return;
        }

        if (IsDuplicateInMatrix(cell, digits))
        {
            cell.Text = _lastValidByCell.GetValueOrDefault(cell, string.Empty);
            ApplyCellStyle(cell);
            SetStatus($"Pan {digits} is already in the matrix — each number must be unique.");
            return;
        }

        RememberValidCellValue(cell);
    }

    private void MatrixCell_OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(DataFormats.Text))
        {
            var text = e.DataObject.GetData(DataFormats.Text) as string ?? string.Empty;
            if (!Regex.IsMatch(text.Trim(), @"^\d*$"))
            {
                e.CancelCommand();
            }
        }
    }

    private static string NormalizeDigits(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? string.Empty : Regex.Replace(raw.Trim(), @"[^\d]", string.Empty);

    private void RememberValidCellValue(TextBox cell)
    {
        _lastValidByCell[cell] = NormalizeDigits(cell.Text);
        ApplyCellStyle(cell);
    }

    private void ApplyCellStyle(TextBox cell)
    {
        var digits = NormalizeDigits(cell.Text);
        if (digits.Length == 0)
        {
            cell.Background = ColorSchemeResourceCatalog.GetBrush("WhiteBackground");
            cell.BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225));
            cell.BorderThickness = new Thickness(1);
            cell.ToolTip = null;
            return;
        }

        if (IsDuplicateInMatrix(cell, digits))
        {
            cell.Background = DuplicateBrush;
            cell.BorderBrush = DuplicateBorderBrush;
            cell.BorderThickness = new Thickness(2);
            cell.ToolTip = $"Pan {digits} already appears in another cell.";
            return;
        }

        cell.Background = FilledBrush;
        cell.BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225));
        cell.BorderThickness = new Thickness(1);
        cell.ToolTip = null;
    }

    private bool IsDuplicateInMatrix(TextBox source, string digits)
    {
        if (digits.Length == 0)
        {
            return false;
        }

        foreach (var column in _columnCells)
        {
            foreach (var cell in column)
            {
                if (ReferenceEquals(cell, source))
                {
                    continue;
                }

                if (NormalizeDigits(cell.Text) == digits)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool HasDuplicateNumbers(out string? duplicate)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var column in _columnCells)
        {
            foreach (var cell in column)
            {
                var digits = NormalizeDigits(cell.Text);
                if (digits.Length == 0)
                {
                    continue;
                }

                if (!seen.Add(digits))
                {
                    duplicate = digits;
                    return true;
                }
            }
        }

        duplicate = null;
        return false;
    }



    private void AddRowButton_OnClick(object sender, RoutedEventArgs e)

    {

        if (!_isReviewReady)

        {

            return;

        }



        _rowCount++;

        MatrixGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });



        var rowHeader = CreateHeaderTextBlock($"{_rowCount}");

        Grid.SetRow(rowHeader, _rowCount);

        Grid.SetColumn(rowHeader, 0);

        MatrixGrid.Children.Add(rowHeader);



        for (var c = 0; c < _columnCount; c++)

        {

            var cell = CreateMatrixCell();

            cell.IsEnabled = true;

            Grid.SetRow(cell, _rowCount);

            Grid.SetColumn(cell, c + 1);

            MatrixGrid.Children.Add(cell);

            _columnCells[c].Add(cell);

        }

    }



    private void AddColumnButton_OnClick(object sender, RoutedEventArgs e)

    {

        if (!_isReviewReady)

        {

            return;

        }



        _columnCount++;

        MatrixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });



        var header = CreateHeaderTextBlock($"Col {_columnCount}");

        Grid.SetRow(header, 0);

        Grid.SetColumn(header, _columnCount);

        MatrixGrid.Children.Add(header);



        var newColumn = new List<TextBox>();

        for (var r = 0; r < _rowCount; r++)

        {

            var cell = CreateMatrixCell();

            cell.IsEnabled = true;

            Grid.SetRow(cell, r + 1);

            Grid.SetColumn(cell, _columnCount);

            MatrixGrid.Children.Add(cell);

            newColumn.Add(cell);

        }



        _columnCells.Add(newColumn);

    }



    private void CancelButton_OnClick(object sender, RoutedEventArgs e)

    {

        UserAccepted = false;

        Close();

    }



    private void AcceptButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (HasDuplicateNumbers(out var duplicate))
        {
            SetStatus($"Cannot accept — pan {duplicate} appears more than once.");
            return;
        }

        UserAccepted = true;
        Close();
    }



    public IReadOnlyList<string> CollectNumbersInReadOrder()

    {

        var result = new List<string>();

        for (var c = 0; c < _columnCount; c++)

        {

            for (var r = 0; r < _columnCells[c].Count; r++)

            {

                var digits = Regex.Replace(_columnCells[c][r].Text.Trim(), @"[^\d]", string.Empty);

                if (digits.Length > 0)

                {

                    result.Add(digits);

                }

            }

        }



        return result;

    }

}


