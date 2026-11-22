using StatsClient.MVVM.Core;

using StatsClient.MVVM.Model;

using Syncfusion.Pdf;

using Syncfusion.Pdf.Parsing;

using System.ComponentModel;

using System.Drawing;

using System.IO;

using System.Runtime.CompilerServices;

using System.Windows;

using System.Windows.Input;

using System.Windows.Media;

using System.Windows.Media.Imaging;



namespace StatsClient.MVVM.View;



public partial class PrescriptionPanPositionEditorWindow : Window, INotifyPropertyChanged

{

    private const float MoveStepPixels = 2f;

    private const float FontStepPoints = 1f;



    private PrescriptionPanEditSession? _session;

    private float _pixelsPerPoint = 1f;

    private float _pageWidthPoints;

    private float _pageHeightPoints;



    private BitmapImage? _prescriptionImage;

    private double _canvasWidth;

    private double _canvasHeight;

    private double _stampPixelX;

    private double _stampPixelY;

    private double _stampFontSizePixels;

    private string _stampLabelText = "";

    private string _headerTitle = "";

    private string _hintText = "";

    private string _systemDisplayName = "";

    private double _stampLabelMaxWidth = 2000;

    public double StampLabelMaxWidth
    {
        get => _stampLabelMaxWidth;
        private set { _stampLabelMaxWidth = value; OnPropertyChanged(); }
    }

    private System.Windows.Media.Brush _stampForeground = System.Windows.Media.Brushes.Black;



    public event EventHandler<PrescriptionPanEditSession>? SaveRequested;

    public event EventHandler<PrescriptionPanEditSession>? SaveAsDefaultRequested;



    public BitmapImage? PrescriptionImage

    {

        get => _prescriptionImage;

        private set { _prescriptionImage = value; OnPropertyChanged(); }

    }



    public double CanvasWidth

    {

        get => _canvasWidth;

        private set { _canvasWidth = value; OnPropertyChanged(); }

    }



    public double CanvasHeight

    {

        get => _canvasHeight;

        private set { _canvasHeight = value; OnPropertyChanged(); }

    }



    public double StampPixelX

    {

        get => _stampPixelX;

        private set { _stampPixelX = value; OnPropertyChanged(); }

    }



    public double StampPixelY

    {

        get => _stampPixelY;

        private set { _stampPixelY = value; OnPropertyChanged(); }

    }



    public double StampFontSizePixels

    {

        get => _stampFontSizePixels;

        private set { _stampFontSizePixels = value; OnPropertyChanged(); }

    }



    public string StampLabelText

    {

        get => _stampLabelText;

        private set { _stampLabelText = value; OnPropertyChanged(); }

    }



    public string HeaderTitle

    {

        get => _headerTitle;

        private set { _headerTitle = value; OnPropertyChanged(); }

    }



    public string HintText

    {

        get => _hintText;

        private set { _hintText = value; OnPropertyChanged(); }

    }



    public string SystemDisplayName

    {

        get => _systemDisplayName;

        private set { _systemDisplayName = value; OnPropertyChanged(); }

    }



    public System.Windows.Media.Brush StampForeground

    {

        get => _stampForeground;

        private set { _stampForeground = value; OnPropertyChanged(); }

    }



    public PrescriptionPanPositionEditorWindow(Window owner)

    {

        Owner = owner;

        InitializeComponent();

        DataContext = this;



        PreviewKeyDown += OnPreviewKeyDown;

    }



    public void Initialize(PrescriptionPanEditSession session)

    {

        _session = session;

        StampLabelText = session.StampDisplayText;

        SystemDisplayName = session.SystemDisplayName;



        if (session.Kind == PrescriptionStampEditKind.Rush)
        {
            HeaderTitle = "Edit RUSH position";
            HintText = "Arrow keys or buttons move the RUSH label 2 px. + / − adjust font size.";
            StampForeground = System.Windows.Media.Brushes.DarkRed;
            Title = $"Edit RUSH position — {session.SystemDisplayName}";
            StampLabelMaxWidth = 2000;
        }
        else if (session.Kind == PrescriptionStampEditKind.InfoLabel)
        {
            HeaderTitle = "Edit label position";
            HintText = "Arrow keys or buttons move the label 2 px. + / − adjust font size.";
            StampForeground = System.Windows.Media.Brushes.Black;
            Title = $"Edit label — {session.SystemDisplayName}";
            StampLabelMaxWidth = 420;
        }
        else
        {
            HeaderTitle = "Edit pan number position";
            HintText = "Arrow keys or buttons move the number 2 px. + / − adjust font size.";
            StampForeground = System.Windows.Media.Brushes.Black;
            Title = $"Edit pan number — {session.SystemDisplayName}";
            StampLabelMaxWidth = 2000;
        }



        if (!File.Exists(session.OriginalPdfPath))

        {

            throw new FileNotFoundException("Prescription PDF not found.", session.OriginalPdfPath);

        }



        using PdfLoadedDocument doc = new(session.OriginalPdfPath);

        PdfLoadedPage page = (doc.Pages[0] as PdfLoadedPage)!;

        SizeF pageSize = page.Graphics.ClientSize;

        _pageWidthPoints = pageSize.Width;

        _pageHeightPoints = pageSize.Height;



        using Bitmap bitmap = PrescriptionPdfPageRenderer.RenderFirstPage(session.OriginalPdfPath);

        _pixelsPerPoint = bitmap.Width / _pageWidthPoints;



        PrescriptionImage = PrescriptionPdfPageRenderer.ToBitmapImage(bitmap);

        CanvasWidth = bitmap.Width;

        CanvasHeight = bitmap.Height;



        SyncPixelsFromSession();

    }



    private void SyncPixelsFromSession()

    {

        if (_session is null)

        {

            return;

        }



        PointF origin = ResolveSessionOrigin();
        StampPixelX = origin.X * _pixelsPerPoint;
        StampPixelY = origin.Y * _pixelsPerPoint;

        float fontSizePoints = ResolveSessionFontSize();
        StampFontSizePixels = fontSizePoints * _pixelsPerPoint;
    }

    private PointF ResolveSessionOrigin()
    {
        if (_session is null)
        {
            return PointF.Empty;
        }

        return _session.Kind switch
        {
            PrescriptionStampEditKind.Rush => PrescriptionRushPositionResolver.ResolveOrigin(
                _session.StampHeader, _pageWidthPoints, _pageHeightPoints, _session.OffsetX, _session.OffsetY),
            PrescriptionStampEditKind.InfoLabel => PrescriptionInfoPositionResolver.ResolveOrigin(
                _session.StampHeader, _pageWidthPoints, _pageHeightPoints, _session.OffsetX, _session.OffsetY),
            _ => PrescriptionPanPositionResolver.ResolveOrigin(
                _session.StampHeader, _pageWidthPoints, _pageHeightPoints, _session.OffsetX, _session.OffsetY),
        };
    }

    private float ResolveSessionFontSize(float fontSizeDelta = 0)
    {
        if (_session is null)
        {
            return 0;
        }

        float delta = fontSizeDelta == 0 ? _session.FontSizeDelta : fontSizeDelta;
        return _session.Kind switch
        {
            PrescriptionStampEditKind.Rush => PrescriptionRushPositionResolver.ResolveFontSize(
                _session.StampHeader, _pageHeightPoints, delta),
            PrescriptionStampEditKind.InfoLabel => PrescriptionInfoPositionResolver.ResolveFontSize(
                _session.StampHeader, _pageHeightPoints, _session.InfoDisplayText, delta),
            _ => PrescriptionPanPositionResolver.ResolveFontSize(
                _session.StampHeader, _pageHeightPoints, delta),
        };
    }



    private void MoveStamp(float deltaPixelX, float deltaPixelY)

    {

        if (_session is null || _pixelsPerPoint <= 0)

        {

            return;

        }



        _session.OffsetX += deltaPixelX / _pixelsPerPoint;

        _session.OffsetY += deltaPixelY / _pixelsPerPoint;

        StampPixelX += deltaPixelX;

        StampPixelY += deltaPixelY;

    }



    private void AdjustFontSize(float deltaPoints)

    {

        if (_session is null || _pixelsPerPoint <= 0)

        {

            return;

        }



        float currentSize = ResolveSessionFontSize();
        float nextSize = ResolveSessionFontSize(_session.FontSizeDelta + deltaPoints);



        if (Math.Abs(nextSize - currentSize) < 0.01f)

        {

            return;

        }



        _session.FontSizeDelta += deltaPoints;

        StampFontSizePixels = nextSize * _pixelsPerPoint;

    }



    private void OnPreviewKeyDown(object sender, KeyEventArgs e)

    {

        switch (e.Key)

        {

            case Key.Left:

                MoveStamp(-MoveStepPixels, 0);

                e.Handled = true;

                break;

            case Key.Right:

                MoveStamp(MoveStepPixels, 0);

                e.Handled = true;

                break;

            case Key.Up:

                MoveStamp(0, -MoveStepPixels);

                e.Handled = true;

                break;

            case Key.Down:

                MoveStamp(0, MoveStepPixels);

                e.Handled = true;

                break;

            case Key.Add:

            case Key.OemPlus:

                AdjustFontSize(FontStepPoints);

                e.Handled = true;

                break;

            case Key.Subtract:

            case Key.OemMinus:

                AdjustFontSize(-FontStepPoints);

                e.Handled = true;

                break;

            case Key.Escape:

                Close();

                e.Handled = true;

                break;

        }

    }



    private void MoveUp_Click(object sender, RoutedEventArgs e) => MoveStamp(0, -MoveStepPixels);

    private void MoveLeft_Click(object sender, RoutedEventArgs e) => MoveStamp(-MoveStepPixels, 0);

    private void MoveDown_Click(object sender, RoutedEventArgs e) => MoveStamp(0, MoveStepPixels);

    private void MoveRight_Click(object sender, RoutedEventArgs e) => MoveStamp(MoveStepPixels, 0);

    private void IncreaseFont_Click(object sender, RoutedEventArgs e) => AdjustFontSize(FontStepPoints);

    private void DecreaseFont_Click(object sender, RoutedEventArgs e) => AdjustFontSize(-FontStepPoints);



    private void Save_Click(object sender, RoutedEventArgs e)

    {

        if (_session is null)

        {

            return;

        }



        SaveRequested?.Invoke(this, _session);

    }



    private void SaveAsDefault_Click(object sender, RoutedEventArgs e)

    {

        if (_session is null)

        {

            return;

        }



        SaveAsDefaultRequested?.Invoke(this, _session);

    }



    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();



    private void TitleBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)

    {

        if (e.ChangedButton != MouseButton.Left)

        {

            return;

        }



        try

        {

            DragMove();

        }

        catch

        {

        }

    }



    private void CloseButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)

    {

        e.Handled = true;

        Close();

    }



    public event PropertyChangedEventHandler? PropertyChanged;



    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

}


