using StatsClient.MVVM.Core;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace StatsClient.UserControls;

public partial class TeethSelectorControl : UserControl
{
    private static readonly SolidColorBrush SelectedFill;
    private static readonly SolidColorBrush HoverFill;
    private static readonly SolidColorBrush DefaultStroke;
    private static readonly SolidColorBrush SelectedStroke;
    private static readonly SolidColorBrush RootStroke;
    private static readonly SolidColorBrush LabelBrush;
    private static readonly SolidColorBrush GumLineBrush;
    private static readonly SolidColorBrush MidlineBrush;
    private static readonly SolidColorBrush IncisorFill;
    private static readonly SolidColorBrush CanineFill;
    private static readonly SolidColorBrush PremolarFill;
    private static readonly SolidColorBrush MolarFill;

    private readonly Dictionary<int, Path> _crownPaths = [];
    private readonly HashSet<int> _selectedTeeth = [];
    private bool _suppressTeethNumbersCallback;

    static TeethSelectorControl()
    {
        SelectedFill = Freeze(Color.FromArgb(215, 201, 162, 39));
        HoverFill = Freeze(Color.FromArgb(120, 201, 162, 39));
        DefaultStroke = Freeze(Color.FromRgb(120, 136, 156));
        SelectedStroke = Freeze(Color.FromRgb(232, 198, 90));
        RootStroke = Freeze(Color.FromArgb(150, 100, 116, 139));
        LabelBrush = Freeze(Color.FromRgb(203, 213, 225));
        GumLineBrush = Freeze(Color.FromArgb(90, 148, 163, 184));
        MidlineBrush = Freeze(Color.FromArgb(70, 148, 163, 184));
        IncisorFill = Freeze(Color.FromArgb(44, 198, 186, 162));
        CanineFill = Freeze(Color.FromArgb(44, 212, 168, 128));
        PremolarFill = Freeze(Color.FromArgb(44, 148, 172, 198));
        MolarFill = Freeze(Color.FromArgb(44, 138, 168, 152));
    }

    public TeethSelectorControl()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
        {
            SetCurrentValue(TeethNumbersProperty, "8,9");
            BuildChart();
        }
    }

    private void TeethSelectorControl_Loaded(object sender, RoutedEventArgs e)
    {
        BuildChart();
    }

    public static readonly DependencyProperty TeethNumbersProperty =
        DependencyProperty.Register(
            nameof(TeethNumbers),
            typeof(string),
            typeof(TeethSelectorControl),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnTeethNumbersChanged));

    public static readonly DependencyProperty NumberFontSizeProperty =
        DependencyProperty.Register(
            nameof(NumberFontSize),
            typeof(double),
            typeof(TeethSelectorControl),
            new FrameworkPropertyMetadata(11.0, OnChartAppearanceChanged));

    public static readonly DependencyProperty NumberLabelMarginProperty =
        DependencyProperty.Register(
            nameof(NumberLabelMargin),
            typeof(double),
            typeof(TeethSelectorControl),
            new FrameworkPropertyMetadata(10.0, OnChartAppearanceChanged));

    public string TeethNumbers
    {
        get => (string)GetValue(TeethNumbersProperty);
        set => SetValue(TeethNumbersProperty, value);
    }

    public double NumberFontSize
    {
        get => (double)GetValue(NumberFontSizeProperty);
        set => SetValue(NumberFontSizeProperty, value);
    }

    /// <summary>
    /// Gap between each crown edge and its tooth number, in reference canvas pixels (1096×420).
    /// </summary>
    public double NumberLabelMargin
    {
        get => (double)GetValue(NumberLabelMarginProperty);
        set => SetValue(NumberLabelMarginProperty, value);
    }

    private static void OnChartAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TeethSelectorControl control)
        {
            control.RebuildChartIfReady();
        }
    }

    private void RebuildChartIfReady()
    {
        if (IsLoaded || DesignerProperties.GetIsInDesignMode(this))
        {
            BuildChart();
        }
    }

    private static void OnTeethNumbersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TeethSelectorControl control)
        {
            return;
        }

        if (control._crownPaths.Count == 0)
        {
            control.RebuildChartIfReady();
        }
        else
        {
            control.ApplyTeethNumbersFromBinding();
        }
    }

    private void BuildChart()
    {
        ChartCanvas.Children.Clear();
        _crownPaths.Clear();

        double width = ChartRoot.Width;
        double height = ChartRoot.Height;

        AddDecorativeLines(width, height);

        foreach (TeethSelectorSlot slot in TeethSelectorLayout.AllSlots)
        {
            ChartCanvas.Children.Add(new Path
            {
                Data = TeethSelectorGeometry.CreateRoot(slot, width, height),
                Stroke = RootStroke,
                StrokeThickness = 1,
                Fill = Brushes.Transparent,
                IsHitTestVisible = false,
            });

            var crown = new Path
            {
                Data = TeethSelectorGeometry.CreateCrown(slot, width, height),
                Fill = GetCategoryFill(slot.ToothNumber),
                Stroke = DefaultStroke,
                StrokeThickness = 1.1,
                Cursor = Cursors.Hand,
                Tag = slot.ToothNumber,
                ToolTip = $"Tooth {slot.ToothNumber}",
            };

            crown.MouseLeftButtonDown += CrownPath_MouseLeftButtonDown;
            crown.MouseEnter += CrownPath_MouseEnter;
            crown.MouseLeave += CrownPath_MouseLeave;

            _crownPaths[slot.ToothNumber] = crown;
            ChartCanvas.Children.Add(crown);
            AddToothLabel(slot, width, height);
        }

        ApplyTeethNumbersFromBinding();
    }

    private void AddDecorativeLines(double width, double height)
    {
        ChartCanvas.Children.Add(new Path
        {
            Data = TeethSelectorGeometry.CreateUpperGumLine(width, height),
            Stroke = GumLineBrush,
            StrokeThickness = 1.5,
            IsHitTestVisible = false,
        });

        ChartCanvas.Children.Add(new Path
        {
            Data = TeethSelectorGeometry.CreateLowerGumLine(width, height),
            Stroke = GumLineBrush,
            StrokeThickness = 1.5,
            IsHitTestVisible = false,
        });

        ChartCanvas.Children.Add(new Line
        {
            X1 = width * 0.5,
            Y1 = height * 0.11,
            X2 = width * 0.5,
            Y2 = height * 0.89,
            Stroke = MidlineBrush,
            StrokeThickness = 1,
            StrokeDashArray = [3, 4],
            IsHitTestVisible = false,
        });
    }

    private void AddToothLabel(TeethSelectorSlot slot, double width, double height)
    {
        bool isUpper = slot.ToothNumber <= 16;
        double cx = slot.CenterX * width;
        double halfHeight = slot.PixelHeight(height) / 2;
        double cy = slot.CenterY * height;
        double labelGap = Math.Max(0, NumberLabelMargin);

        var label = new TextBlock
        {
            Text = slot.ToothNumber.ToString(),
            FontSize = NumberFontSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = LabelBrush,
            IsHitTestVisible = false,
        };

        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double labelWidth = label.DesiredSize.Width;
        double labelHeight = label.DesiredSize.Height;

        Canvas.SetLeft(label, cx - (labelWidth / 2));
        Canvas.SetTop(
            label,
            isUpper
                ? cy - halfHeight - labelGap - labelHeight
                : cy + halfHeight + labelGap);
        ChartCanvas.Children.Add(label);
    }

    private void ApplyTeethNumbersFromBinding()
    {
        if (_crownPaths.Count == 0)
        {
            return;
        }

        _suppressTeethNumbersCallback = true;
        _selectedTeeth.Clear();
        foreach (int tooth in TeethSelectorLayout.ParseSelection(TeethNumbers))
        {
            _selectedTeeth.Add(tooth);
        }

        RefreshSelectionVisuals();
        _suppressTeethNumbersCallback = false;
    }

    private void CrownPath_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Path path || path.Tag is not int tooth)
        {
            return;
        }

        e.Handled = true;

        if (_selectedTeeth.Contains(tooth))
        {
            _selectedTeeth.Remove(tooth);
        }
        else
        {
            _selectedTeeth.Add(tooth);
        }

        RefreshSelectionVisuals();
        UpdateTeethNumbersProperty();
    }

    private void CrownPath_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Path path || path.Tag is not int tooth || _selectedTeeth.Contains(tooth))
        {
            return;
        }

        path.Fill = HoverFill;
    }

    private void CrownPath_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is not Path path || path.Tag is not int tooth)
        {
            return;
        }

        ApplyPathVisual(path, tooth);
    }

    private void RefreshSelectionVisuals()
    {
        foreach ((int tooth, Path path) in _crownPaths)
        {
            ApplyPathVisual(path, tooth);
        }
    }

    private void ApplyPathVisual(Path path, int tooth)
    {
        bool selected = _selectedTeeth.Contains(tooth);
        path.Fill = selected ? SelectedFill : GetCategoryFill(tooth);
        path.Stroke = selected ? SelectedStroke : DefaultStroke;
        path.StrokeThickness = selected ? 1.5 : 1.1;
    }

    private static SolidColorBrush GetCategoryFill(int toothNumber) =>
        TeethSelectorLayout.ResolveCrownCategory(toothNumber) switch
        {
            TeethSelectorCrownCategory.Incisor => IncisorFill,
            TeethSelectorCrownCategory.Canine => CanineFill,
            TeethSelectorCrownCategory.Premolar => PremolarFill,
            _ => MolarFill,
        };

    private void UpdateTeethNumbersProperty()
    {
        if (_suppressTeethNumbersCallback)
        {
            return;
        }

        string formatted = TeethSelectorLayout.FormatSelection(_selectedTeeth);
        if (!string.Equals(TeethNumbers, formatted, StringComparison.Ordinal))
        {
            TeethNumbers = formatted;
        }
    }

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
