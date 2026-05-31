using System.Windows;
using System.Windows.Controls;

namespace DCMViewer.Controls;

public partial class DcmBusyOverlayPanel : UserControl
{
    public static readonly DependencyProperty StatusTextProperty =
        DependencyProperty.Register(
            nameof(StatusText),
            typeof(string),
            typeof(DcmBusyOverlayPanel),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty LoadProgressProperty =
        DependencyProperty.Register(
            nameof(LoadProgress),
            typeof(double),
            typeof(DcmBusyOverlayPanel),
            new PropertyMetadata(0.0));

    public static readonly DependencyProperty ProgressPercentTextProperty =
        DependencyProperty.Register(
            nameof(ProgressPercentText),
            typeof(string),
            typeof(DcmBusyOverlayPanel),
            new PropertyMetadata("0%"));

    public DcmBusyOverlayPanel()
    {
        InitializeComponent();
    }

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public double LoadProgress
    {
        get => (double)GetValue(LoadProgressProperty);
        set => SetValue(LoadProgressProperty, value);
    }

    public string ProgressPercentText
    {
        get => (string)GetValue(ProgressPercentTextProperty);
        set => SetValue(ProgressPercentTextProperty, value);
    }

    public event RoutedEventHandler? CancelClick;

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        CancelClick?.Invoke(this, e);
    }
}
