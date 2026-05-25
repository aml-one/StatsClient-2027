using System.Windows.Controls;
using System.Windows;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using StatsClient.MVVM.Core;
using System.IO;
using HelixToolkit.Wpf;

namespace DCMViewer.Controls;

/// <summary>
/// Reusable viewer canvas component that hosts the watermark background, Helix 3D viewport,
/// section-plane visuals, and measurement visuals for embedding in other WPF applications.
/// </summary>
public partial class DcmViewerCanvasComponent : UserControl
{
    private static readonly ImageSource? DefaultLogoSource = TryCreateDefaultLogoSource();
    private MainWindow? _embeddedWindow;
    private UIElement? _hostedContent;
    private List<DCMFileItem>? _pendingCaseFiles;

    private static ImageSource? TryCreateDefaultLogoSource()
    {
        try
        {
            return new BitmapImage(new Uri("pack://application:,,,/DcmViewer/Images/logo.png", UriKind.Absolute));
        }
        catch (IOException)
        {
            return null;
        }
    }

    public static readonly DependencyProperty GradientModeProperty =
        DependencyProperty.Register(
            nameof(GradientMode),
            typeof(ViewerBackgroundGradientMode),
            typeof(DcmViewerCanvasComponent),
            new PropertyMetadata(ViewerBackgroundGradientMode.Radial, OnAppearancePropertyChanged));

    public static readonly DependencyProperty GradientStartColorProperty =
        DependencyProperty.Register(
            nameof(GradientStartColor),
            typeof(Color),
            typeof(DcmViewerCanvasComponent),
            new PropertyMetadata(Color.FromRgb(255, 255, 255), OnAppearancePropertyChanged));

    public static readonly DependencyProperty GradientMidColorProperty =
        DependencyProperty.Register(
            nameof(GradientMidColor),
            typeof(Color),
            typeof(DcmViewerCanvasComponent),
            new PropertyMetadata(Color.FromRgb(243, 246, 249), OnAppearancePropertyChanged));

    public static readonly DependencyProperty GradientMidOuterColorProperty =
        DependencyProperty.Register(
            nameof(GradientMidOuterColor),
            typeof(Color),
            typeof(DcmViewerCanvasComponent),
            new PropertyMetadata(Color.FromRgb(211, 215, 218), OnAppearancePropertyChanged));

    public static readonly DependencyProperty GradientOuterColorProperty =
        DependencyProperty.Register(
            nameof(GradientOuterColor),
            typeof(Color),
            typeof(DcmViewerCanvasComponent),
            new PropertyMetadata(Color.FromRgb(176, 180, 184), OnAppearancePropertyChanged));

    public static readonly DependencyProperty IsBackgroundTransparentProperty =
        DependencyProperty.Register(
            nameof(IsBackgroundTransparent),
            typeof(bool),
            typeof(DcmViewerCanvasComponent),
            new PropertyMetadata(false, OnAppearancePropertyChanged));

    public static readonly DependencyProperty IsWatermarkVisibleProperty =
        DependencyProperty.Register(
            nameof(IsWatermarkVisible),
            typeof(bool),
            typeof(DcmViewerCanvasComponent),
            new PropertyMetadata(true, OnAppearancePropertyChanged));

    public static readonly DependencyProperty UseFullAppShellProperty =
        DependencyProperty.Register(
            nameof(UseFullAppShell),
            typeof(bool),
            typeof(DcmViewerCanvasComponent),
            new PropertyMetadata(true));

    public static readonly DependencyProperty IsLogoVisibleProperty =
        DependencyProperty.Register(
            nameof(IsLogoVisible),
            typeof(bool),
            typeof(DcmViewerCanvasComponent),
            new PropertyMetadata(true, OnAppearancePropertyChanged));

    public static readonly DependencyProperty LogoSourceProperty =
        DependencyProperty.Register(
            nameof(LogoSource),
            typeof(ImageSource),
            typeof(DcmViewerCanvasComponent),
            new PropertyMetadata(null, OnAppearancePropertyChanged));

    public static readonly DependencyProperty WatermarkTextProperty =
        DependencyProperty.Register(
            nameof(WatermarkText),
            typeof(string),
            typeof(DcmViewerCanvasComponent),
            new PropertyMetadata("AmL", OnAppearancePropertyChanged));

    public static readonly DependencyProperty WatermarkTextColorProperty =
        DependencyProperty.Register(
            nameof(WatermarkTextColor),
            typeof(Color),
            typeof(DcmViewerCanvasComponent),
            new PropertyMetadata(Color.FromRgb(184, 163, 92), OnAppearancePropertyChanged));

    public static readonly DependencyProperty WatermarkTextFontSizeProperty =
        DependencyProperty.Register(
            nameof(WatermarkTextFontSize),
            typeof(double),
            typeof(DcmViewerCanvasComponent),
            new PropertyMetadata(80.0, OnAppearancePropertyChanged));

    /// <summary>
    /// Initializes a new instance of the <see cref="DcmViewerCanvasComponent"/> class.
    /// </summary>
    public DcmViewerCanvasComponent()
    {
        InitializeComponent();
        UpdateBackgroundBrush();
        UpdateWatermarkVisuals();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    /// <summary>
    /// Gets or sets the background gradient layout mode.
    /// </summary>
    public ViewerBackgroundGradientMode GradientMode
    {
        get => (ViewerBackgroundGradientMode)GetValue(GradientModeProperty);
        set => SetValue(GradientModeProperty, value);
    }

    /// <summary>
    /// Gets or sets the innermost background gradient color.
    /// </summary>
    public Color GradientStartColor
    {
        get => (Color)GetValue(GradientStartColorProperty);
        set => SetValue(GradientStartColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the inner-middle background gradient color.
    /// </summary>
    public Color GradientMidColor
    {
        get => (Color)GetValue(GradientMidColorProperty);
        set => SetValue(GradientMidColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the outer-middle background gradient color.
    /// </summary>
    public Color GradientMidOuterColor
    {
        get => (Color)GetValue(GradientMidOuterColorProperty);
        set => SetValue(GradientMidOuterColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the outermost background gradient color.
    /// </summary>
    public Color GradientOuterColor
    {
        get => (Color)GetValue(GradientOuterColorProperty);
        set => SetValue(GradientOuterColorProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the component background is fully transparent.
    /// When true, host container background is visible through the canvas.
    /// </summary>
    public bool IsBackgroundTransparent
    {
        get => (bool)GetValue(IsBackgroundTransparentProperty);
        set => SetValue(IsBackgroundTransparentProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether watermark visuals are visible.
    /// This controls both the logo image and the watermark text.
    /// </summary>
    public bool IsWatermarkVisible
    {
        get => (bool)GetValue(IsWatermarkVisibleProperty);
        set => SetValue(IsWatermarkVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the component hosts the full DCMViewer shell UI
    /// (toolbar/buttons/panels) instead of canvas-only mode.
    /// </summary>
    public bool UseFullAppShell
    {
        get => (bool)GetValue(UseFullAppShellProperty);
        set => SetValue(UseFullAppShellProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the logo watermark image is shown.
    /// </summary>
    public bool IsLogoVisible
    {
        get => (bool)GetValue(IsLogoVisibleProperty);
        set => SetValue(IsLogoVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets a custom logo image source. If null, the built-in embedded logo is used.
    /// </summary>
    public ImageSource? LogoSource
    {
        get => (ImageSource?)GetValue(LogoSourceProperty);
        set => SetValue(LogoSourceProperty, value);
    }

    /// <summary>
    /// Gets or sets the watermark text displayed over the background.
    /// </summary>
    public string WatermarkText
    {
        get => (string)GetValue(WatermarkTextProperty);
        set => SetValue(WatermarkTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the watermark text color.
    /// </summary>
    public Color WatermarkTextColor
    {
        get => (Color)GetValue(WatermarkTextColorProperty);
        set => SetValue(WatermarkTextColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the watermark text font size.
    /// </summary>
    public double WatermarkTextFontSize
    {
        get => (double)GetValue(WatermarkTextFontSizeProperty);
        set => SetValue(WatermarkTextFontSizeProperty, value);
    }

    /// <summary>
    /// Gets the hosted Helix viewport for advanced host-side interaction.
    /// </summary>
    public HelixViewport3D Viewport3D => Viewport;

    /// <summary>
    /// Gets the cutting-plane group used to apply section cuts to the model content.
    /// </summary>
    public CuttingPlaneGroup CuttingPlaneGroup => SectionCutGroup;

    /// <summary>
    /// Gets the 3D model visual used for section-plane display.
    /// </summary>
    public ModelVisual3D SectionPlaneModel => SectionPlaneVisual;

    /// <summary>
    /// Gets the outline visual used for the section-plane boundary.
    /// </summary>
    public LinesVisual3D SectionPlaneOutline => SectionPlaneOutlineVisual;

    /// <summary>
    /// Gets the measurement line visual used in the hosted 3D viewport.
    /// </summary>
    public LinesVisual3D MeasurementLineVisual => MeasurementLine;

    /// <summary>
    /// Gets the billboard text visual used for 3D measurement labels.
    /// </summary>
    public BillboardTextVisual3D MeasurementTextVisual => MeasurementText;

    public Task LoadCaseFilesAsync(IEnumerable<DCMFileItem> files)
    {
        var fileList = files?.ToList() ?? [];
        if (!UseFullAppShell)
        {
            return Task.CompletedTask;
        }

        _pendingCaseFiles = fileList;
        return EnsureEmbeddedWindowLoadedAsync();
    }

    public void RestoreInteraction()
    {
        if (!UseFullAppShell || _embeddedWindow is null)
        {
            return;
        }

        if (_hostedContent is not null && !ReferenceEquals(Content, _hostedContent))
        {
            Content = null;
            Content = _hostedContent;
            ApplyHostedContentDataContext();
        }

        _embeddedWindow.RestoreEmbeddedInteraction();

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() =>
        {
            _embeddedWindow?.RestoreEmbeddedInteraction();
        }));
    }

    public void UnloadViewer()
    {
        _pendingCaseFiles = null;

        if (_embeddedWindow is not null)
        {
            _embeddedWindow.Close();
            _embeddedWindow = null;
        }

        _hostedContent = null;
        Content = null;
        DataContext = null;
    }

    public async Task ReloadCaseFilesAsync(IEnumerable<DCMFileItem> files)
    {
        if (!UseFullAppShell)
        {
            return;
        }

        var fileList = files?.ToList() ?? [];
        UnloadViewer();
        _pendingCaseFiles = fileList;
        await EnsureEmbeddedWindowLoadedAsync();
    }

    public async Task AddCaseFileAsync(DCMFileItem file)
    {
        if (!UseFullAppShell)
        {
            return;
        }

        await EnsureEmbeddedWindowLoadedAsync();
        if (_embeddedWindow is not null)
        {
            await _embeddedWindow.AddCaseFileAsync(file);
        }
    }

    public async Task RemoveCaseFileAsync(string filePath)
    {
        if (!UseFullAppShell)
        {
            return;
        }

        await EnsureEmbeddedWindowLoadedAsync();
        _embeddedWindow?.RemoveCaseFile(filePath);
    }

    private void ApplyHostedContentDataContext()
    {
        if (_hostedContent is FrameworkElement frameworkElement && _embeddedWindow is not null)
        {
            frameworkElement.DataContext = _embeddedWindow.DataContext;
        }
    }

    private async Task EnsureEmbeddedWindowLoadedAsync()
    {
        if (!UseFullAppShell || DesignerProperties.GetIsInDesignMode(this))
        {
            return;
        }

        if (_embeddedWindow is not null)
        {
            _embeddedWindow.SetCanvasBackgroundTransparent(IsBackgroundTransparent);
            DataContext = _embeddedWindow.DataContext;
            if (Content is null && _hostedContent is not null)
            {
                Content = _hostedContent;
                ApplyHostedContentDataContext();
            }

            _embeddedWindow.RestoreEmbeddedInteraction();

            if (_pendingCaseFiles is { Count: > 0 })
            {
                var pendingCaseFiles = _pendingCaseFiles;
                _pendingCaseFiles = null;
                await _embeddedWindow.LoadCaseFilesAsync(pendingCaseFiles);
            }

            return;
        }

        _embeddedWindow = new MainWindow();
        _embeddedWindow.SetCanvasBackgroundTransparent(IsBackgroundTransparent);
        DataContext = _embeddedWindow.DataContext;

        if (_embeddedWindow.Content is not UIElement hostedContent)
        {
            return;
        }

        _hostedContent = hostedContent;

        if (hostedContent is FrameworkElement frameworkElement)
        {
            frameworkElement.DataContext = _embeddedWindow.DataContext;
        }

        _embeddedWindow.Content = null;
        Content = hostedContent;
        _embeddedWindow.RestoreEmbeddedInteraction();

        if (_pendingCaseFiles is { Count: > 0 })
        {
            var pendingCaseFiles = _pendingCaseFiles;
            _pendingCaseFiles = null;
            await _embeddedWindow.LoadCaseFilesAsync(pendingCaseFiles);
        }
    }

    private static void OnAppearancePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DcmViewerCanvasComponent component)
        {
            return;
        }

        if (component.UseFullAppShell)
        {
            component._embeddedWindow?.SetCanvasBackgroundTransparent(component.IsBackgroundTransparent);
            return;
        }

        component.UpdateBackgroundBrush();
        component.UpdateWatermarkVisuals();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await EnsureEmbeddedWindowLoadedAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!UseFullAppShell || DesignerProperties.GetIsInDesignMode(this) || _embeddedWindow is null)
        {
            return;
        }

        if (IsVisible)
        {
            _embeddedWindow.RestoreEmbeddedInteraction();
        }
    }

    private void UpdateBackgroundBrush()
    {
        if (IsBackgroundTransparent)
        {
            WatermarkCanvas.Background = Brushes.Transparent;
            return;
        }

        GradientBrush brush = GradientMode switch
        {
            ViewerBackgroundGradientMode.LinearHorizontal => new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5)
            },
            ViewerBackgroundGradientMode.LinearVertical => new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 0),
                EndPoint = new Point(0.5, 1)
            },
            _ => new RadialGradientBrush
            {
                GradientOrigin = new Point(0.5, 0.5),
                Center = new Point(0.5, 0.5),
                RadiusX = 0.8,
                RadiusY = 0.8
            }
        };

        brush.GradientStops.Add(new GradientStop(GradientStartColor, 0.0));
        brush.GradientStops.Add(new GradientStop(GradientMidColor, 0.35));
        brush.GradientStops.Add(new GradientStop(GradientMidOuterColor, 0.7));
        brush.GradientStops.Add(new GradientStop(GradientOuterColor, 1.0));

        WatermarkCanvas.Background = brush;
    }

    private void UpdateWatermarkVisuals()
    {
        LogoImage.Source = LogoSource ?? DefaultLogoSource;
        LogoImage.Visibility = (IsWatermarkVisible && IsLogoVisible) ? Visibility.Visible : Visibility.Collapsed;

        WatermarkTextBlock.Text = WatermarkText;
        WatermarkTextBlock.Foreground = new SolidColorBrush(WatermarkTextColor);
        WatermarkTextBlock.FontSize = WatermarkTextFontSize;
        WatermarkTextBlock.Visibility = IsWatermarkVisible ? Visibility.Visible : Visibility.Collapsed;
    }
}
