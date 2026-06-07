using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace StatsClient.MVVM.Controls;

public partial class StatsDesignNumericUpDown : UserControl
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(double),
            typeof(StatsDesignNumericUpDown),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public static readonly DependencyProperty StepProperty =
        DependencyProperty.Register(nameof(Step), typeof(double), typeof(StatsDesignNumericUpDown), new PropertyMetadata(0.01));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(StatsDesignNumericUpDown), new PropertyMetadata(double.NegativeInfinity));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(StatsDesignNumericUpDown), new PropertyMetadata(double.PositiveInfinity));

    public static readonly DependencyProperty DecimalPlacesProperty =
        DependencyProperty.Register(nameof(DecimalPlaces), typeof(int), typeof(StatsDesignNumericUpDown), new PropertyMetadata(3, OnFormatChanged));

    private bool _isUpdatingText;

    public StatsDesignNumericUpDown()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshText();
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Step
    {
        get => (double)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public int DecimalPlaces
    {
        get => (int)GetValue(DecimalPlacesProperty);
        set => SetValue(DecimalPlacesProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not StatsDesignNumericUpDown control || control._isUpdatingText)
        {
            return;
        }

        if (e.NewValue is double value)
        {
            control._isUpdatingText = true;
            control.ValueBox.Text = value.ToString($"F{control.DecimalPlaces}", CultureInfo.InvariantCulture);
            control._isUpdatingText = false;
        }
        else
        {
            control.RefreshText();
        }
    }

    private static void OnFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatsDesignNumericUpDown control)
        {
            control.RefreshText();
        }
    }

    private void RefreshText()
    {
        _isUpdatingText = true;
        ValueBox.Text = Value.ToString($"F{DecimalPlaces}", CultureInfo.InvariantCulture);
        _isUpdatingText = false;
    }

    private void CommitText()
    {
        if (_isUpdatingText)
        {
            return;
        }

        if (!double.TryParse(ValueBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
            !double.TryParse(ValueBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
        {
            RefreshText();
            return;
        }

        Value = Clamp(parsed);
        RefreshText();
    }

    private double Clamp(double value) => Math.Clamp(value, Minimum, Maximum);

    private void ValueBox_OnLostFocus(object sender, RoutedEventArgs e) => CommitText();

    private void ValueBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitText();
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private void Increment_Click(object sender, RoutedEventArgs e)
    {
        Value = Clamp(Value + Step);
        RefreshText();
    }

    private void Decrement_Click(object sender, RoutedEventArgs e)
    {
        Value = Clamp(Value - Step);
        RefreshText();
    }
}