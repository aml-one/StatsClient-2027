using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Bitmap = System.Drawing.Bitmap;
using WpfImage = System.Windows.Controls.Image;
using WpfPoint = System.Windows.Point;

namespace StatsClient.MVVM.View;

public partial class PrescriptionDuplicateReviewWindow : Window
{
    private const double MinZoom = 0.05;
    private const double MaxZoom = 12.0;
    private const double ZoomStep = 1.12;

    private readonly LinkedImagePane _savedPane;
    private readonly LinkedImagePane _incomingPane;
    private double _userZoomMultiplier = 1.0;
    private bool _isPanning;
    private bool _isSyncingScroll;
    private WpfPoint _panMouseStart;
    private double _panStartRatioH;
    private double _panStartRatioV;
    private ScrollViewer? _activeScrollViewer;

    public bool IsDuplicate { get; private set; }

    public PrescriptionDuplicateReviewWindow(Window owner)
    {
        Owner = owner;
        InitializeComponent();

        _savedPane = new LinkedImagePane(SavedScrollViewer, SavedHostGrid, SavedTransformGrid, SavedImage);
        _incomingPane = new LinkedImagePane(IncomingScrollViewer, IncomingHostGrid, IncomingTransformGrid, IncomingImage);
    }

    public void Initialize(Bitmap incomingBitmap, string lastSavedPath, string lastPanNumber, string matchReason)
    {
        SavedHeaderTextBlock.Text = $"Saved (#{lastPanNumber})";

        _savedPane.SetSource(LoadBitmapSource(lastSavedPath));
        _incomingPane.SetSource(ToBitmapSource(incomingBitmap));

        Loaded += (_, _) => FitBothImages();
        SizeChanged += (_, _) =>
        {
            if (IsLoaded)
            {
                FitBothImages();
            }
        };
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (DialogResult != true)
        {
            IsDuplicate = true;
        }

        base.OnClosing(e);
    }

    private void TitleBar_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        IsDuplicate = true;
        DialogResult = true;
        Close();
    }

    private void SameDiscardButton_OnClick(object sender, RoutedEventArgs e)
    {
        IsDuplicate = true;
        DialogResult = true;
        Close();
    }

    private void DifferentSaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        IsDuplicate = false;
        DialogResult = true;
        Close();
    }

    private void FitBothImages()
    {
        _userZoomMultiplier = 1.0;
        _savedPane.ComputeBaseFit();
        _incomingPane.ComputeBaseFit();
        ApplyZoomLayout();
        CenterBothImages();
    }

    private void ApplyZoomLayout()
    {
        _savedPane.ApplyZoom(_userZoomMultiplier);
        _incomingPane.ApplyZoom(_userZoomMultiplier);
        _savedPane.UpdateHostDimensions();
        _incomingPane.UpdateHostDimensions();
    }

    private void CenterBothImages()
    {
        _savedPane.CenterInViewer();
        _incomingPane.CenterInViewer();
    }

    private void ImageScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_savedPane.Source is null && _incomingPane.Source is null)
        {
            return;
        }

        var scrollViewer = (ScrollViewer)sender;
        var activePane = GetPane(scrollViewer);
        if (activePane.Source is null)
        {
            return;
        }

        var mouseInViewer = e.GetPosition(scrollViewer);
        var oldZoom = activePane.EffectiveZoom;
        var imageX = (mouseInViewer.X + scrollViewer.HorizontalOffset) / Math.Max(oldZoom, 0.0001);
        var imageY = (mouseInViewer.Y + scrollViewer.VerticalOffset) / Math.Max(oldZoom, 0.0001);

        var factor = e.Delta > 0 ? ZoomStep : 1 / ZoomStep;
        var newMultiplier = Math.Clamp(_userZoomMultiplier * factor, MinZoom, MaxZoom);
        if (Math.Abs(newMultiplier - _userZoomMultiplier) < 0.0001)
        {
            e.Handled = true;
            return;
        }

        _userZoomMultiplier = newMultiplier;
        ApplyZoomLayout();

        activePane.ScrollToContentPoint(imageX, imageY, mouseInViewer);
        SyncNormalizedScrollFrom(activePane);
        e.Handled = true;
    }

    private void ImageScrollViewer_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _isPanning = true;
        _activeScrollViewer = (ScrollViewer)sender;
        _panMouseStart = e.GetPosition(_activeScrollViewer);
        (_panStartRatioH, _panStartRatioV) = GetNormalizedScroll(_activeScrollViewer);
        _activeScrollViewer.CaptureMouse();
        _activeScrollViewer.Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void ImageScrollViewer_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning || e.MiddleButton != MouseButtonState.Pressed || _activeScrollViewer is null)
        {
            return;
        }

        var current = e.GetPosition(_activeScrollViewer);
        var delta = current - _panMouseStart;
        var activeMaxH = GetMaxScrollH(_activeScrollViewer);
        var activeMaxV = GetMaxScrollV(_activeScrollViewer);
        var startOffsetH = _panStartRatioH * activeMaxH;
        var startOffsetV = _panStartRatioV * activeMaxV;
        var ratioH = activeMaxH > 0 ? (startOffsetH - delta.X) / activeMaxH : 0.5;
        var ratioV = activeMaxV > 0 ? (startOffsetV - delta.Y) / activeMaxV : 0.5;

        if (_isSyncingScroll)
        {
            return;
        }

        _isSyncingScroll = true;
        try
        {
            SetNormalizedScroll(_savedPane.ScrollViewer, ratioH, ratioV);
            SetNormalizedScroll(_incomingPane.ScrollViewer, ratioH, ratioV);
        }
        finally
        {
            _isSyncingScroll = false;
        }
        e.Handled = true;
    }

    private void ImageScrollViewer_OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _isPanning = false;
        if (_activeScrollViewer is not null)
        {
            _activeScrollViewer.ReleaseMouseCapture();
            _activeScrollViewer.Cursor = Cursors.Hand;
            _activeScrollViewer = null;
        }

        e.Handled = true;
    }

    private LinkedImagePane GetPane(ScrollViewer scrollViewer)
        => scrollViewer == _savedPane.ScrollViewer ? _savedPane : _incomingPane;

    private void SyncNormalizedScrollFrom(LinkedImagePane sourcePane)
    {
        if (_isSyncingScroll)
        {
            return;
        }

        _isSyncingScroll = true;
        try
        {
            var (ratioH, ratioV) = GetNormalizedScroll(sourcePane.ScrollViewer);
            SetNormalizedScroll(_savedPane.ScrollViewer, ratioH, ratioV);
            SetNormalizedScroll(_incomingPane.ScrollViewer, ratioH, ratioV);
        }
        finally
        {
            _isSyncingScroll = false;
        }
    }

    private static (double ratioH, double ratioV) GetNormalizedScroll(ScrollViewer scrollViewer)
    {
        var maxH = GetMaxScrollH(scrollViewer);
        var maxV = GetMaxScrollV(scrollViewer);
        return (
            maxH > 0 ? scrollViewer.HorizontalOffset / maxH : 0.5,
            maxV > 0 ? scrollViewer.VerticalOffset / maxV : 0.5);
    }

    private static void SetNormalizedScroll(ScrollViewer scrollViewer, double ratioH, double ratioV)
    {
        SetClampedScroll(
            scrollViewer,
            ratioH * GetMaxScrollH(scrollViewer),
            ratioV * GetMaxScrollV(scrollViewer));
    }

    private static void SetClampedScroll(ScrollViewer scrollViewer, double offsetH, double offsetV)
    {
        scrollViewer.ScrollToHorizontalOffset(Math.Clamp(offsetH, 0, GetMaxScrollH(scrollViewer)));
        scrollViewer.ScrollToVerticalOffset(Math.Clamp(offsetV, 0, GetMaxScrollV(scrollViewer)));
    }

    private static double GetMaxScrollH(ScrollViewer scrollViewer)
        => Math.Max(0, scrollViewer.ExtentWidth - scrollViewer.ViewportWidth);

    private static double GetMaxScrollV(ScrollViewer scrollViewer)
        => Math.Max(0, scrollViewer.ExtentHeight - scrollViewer.ViewportHeight);

    private static BitmapSource LoadBitmapSource(string path)
    {
        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.UriSource = new Uri(path, UriKind.Absolute);
        bitmapImage.EndInit();
        bitmapImage.Freeze();
        return bitmapImage;
    }

    private static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        using var memory = new MemoryStream();
        bitmap.Save(memory, ImageFormat.Png);
        memory.Position = 0;

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = memory;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private sealed class LinkedImagePane
    {
        private double _baseFitZoom = 1.0;
        private double _userZoomMultiplier = 1.0;

        public LinkedImagePane(ScrollViewer scrollViewer, Grid hostGrid, Grid transformGrid, WpfImage image)
        {
            ScrollViewer = scrollViewer;
            HostGrid = hostGrid;
            TransformGrid = transformGrid;
            Image = image;
        }

        public ScrollViewer ScrollViewer { get; }
        public Grid HostGrid { get; }
        public Grid TransformGrid { get; }
        public WpfImage Image { get; }
        public BitmapSource? Source => Image.Source as BitmapSource;
        public double EffectiveZoom => _baseFitZoom * _userZoomMultiplier;

        public void SetSource(BitmapSource source)
        {
            Image.Source = source;
        }

        public void ComputeBaseFit()
        {
            ScrollViewer.UpdateLayout();
            if (Source is null)
            {
                return;
            }

            var viewportW = ScrollViewer.ViewportWidth;
            var viewportH = ScrollViewer.ViewportHeight;
            if (viewportW < 10 || viewportH < 10)
            {
                return;
            }

            _baseFitZoom = Math.Min(viewportW / Source.PixelWidth, viewportH / Source.PixelHeight) * 0.98;
        }

        public void ApplyZoom(double userZoomMultiplier)
        {
            _userZoomMultiplier = userZoomMultiplier;
            if (Source is null)
            {
                return;
            }

            var zoom = EffectiveZoom;
            var width = Source.PixelWidth * zoom;
            var height = Source.PixelHeight * zoom;
            TransformGrid.Width = width;
            TransformGrid.Height = height;
            Image.Width = width;
            Image.Height = height;
        }

        public void UpdateHostDimensions()
        {
            var viewportW = ScrollViewer.ViewportWidth;
            var viewportH = ScrollViewer.ViewportHeight;
            if (viewportW < 10 || viewportH < 10 || Source is null)
            {
                return;
            }

            var contentW = TransformGrid.Width;
            var contentH = TransformGrid.Height;
            HostGrid.Width = Math.Max(viewportW, contentW);
            HostGrid.Height = Math.Max(viewportH, contentH);
        }

        public void CenterInViewer()
        {
            ScrollViewer.UpdateLayout();
            UpdateHostDimensions();
            if (Source is null)
            {
                return;
            }

            var maxH = GetMaxScrollH(ScrollViewer);
            var maxV = GetMaxScrollV(ScrollViewer);
            ScrollViewer.ScrollToHorizontalOffset(maxH / 2);
            ScrollViewer.ScrollToVerticalOffset(maxV / 2);
        }

        public void ScrollToContentPoint(double imageX, double imageY, WpfPoint mouseInViewer)
        {
            ScrollViewer.UpdateLayout();
            UpdateHostDimensions();
            ScrollViewer.ScrollToHorizontalOffset(imageX * EffectiveZoom - mouseInViewer.X);
            ScrollViewer.ScrollToVerticalOffset(imageY * EffectiveZoom - mouseInViewer.Y);
        }

        private static double GetMaxScrollH(ScrollViewer scrollViewer)
            => Math.Max(0, scrollViewer.ExtentWidth - scrollViewer.ViewportWidth);

        private static double GetMaxScrollV(ScrollViewer scrollViewer)
            => Math.Max(0, scrollViewer.ExtentHeight - scrollViewer.ViewportHeight);
    }
}
