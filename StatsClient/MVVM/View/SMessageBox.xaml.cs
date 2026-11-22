using StatsClient.MVVM.Core;
using StatsClient.MVVM.ViewModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using static StatsClient.MVVM.ViewModel.MainViewModel;
using static StatsClient.MVVM.Core.Enums;

namespace StatsClient.MVVM.View
{
    public partial class SMessageBox : Window, INotifyPropertyChanged
    {
        public static event PropertyChangedEventHandler? PropertyChangedStatic;
        public event PropertyChangedEventHandler? PropertyChanged;

        public void RaisePropertyChanged([CallerMemberName] string? propertyname = null)
        {
            PropertyChanged?.Invoke(typeof(ObservableObject), new PropertyChangedEventArgs(propertyname));
        }

        #region Properties
        private string sMessageBoxIcon = @"\Images\MessageIcons\Info.png";
        public string SMessageBoxIcon
        {
            get => sMessageBoxIcon;
            set
            {
                sMessageBoxIcon = value;
                RaisePropertyChanged(nameof(SMessageBoxIcon));
            }
        }

                
        private double countDownSeconds = 0;
        public double CountDownSeconds
        {
            get => countDownSeconds;
            set
            {
                countDownSeconds = value;
                RaisePropertyChanged(nameof(CountDownSeconds));
            }
        }
        
        private string sMessageTitle = "";
        public string SMessageTitle
        {
            get => sMessageTitle;
            set
            {
                sMessageTitle = value;
                RaisePropertyChanged(nameof(SMessageTitle));
            }
        }

        private string sMessageBody = "";
        public string SMessageBody
        {
            get => sMessageBody;
            set
            {
                sMessageBody = value;
                RaisePropertyChanged(nameof(SMessageBody));
            }
        }

        private string sMessageButtonLeftContent = "Yes";
        public string SMessageButtonLeftContent
        {
            get => sMessageButtonLeftContent;
            set
            {
                sMessageButtonLeftContent = value;
                RaisePropertyChanged(nameof(SMessageButtonLeftContent));
            }
        }

        private Visibility sMessageButtonLeftVisibility = Visibility.Hidden;
        public Visibility SMessageButtonLeftVisibility
        {
            get => sMessageButtonLeftVisibility;
            set
            {
                sMessageButtonLeftVisibility = value;
                RaisePropertyChanged(nameof(SMessageButtonLeftVisibility));
            }
        }

        public RelayCommand SMessageButtonClickCommand { get; set; }

        private string sMessageButtonRightContent = "Ok";
        public string SMessageButtonRightContent
        {
            get => sMessageButtonRightContent;
            set
            {
                sMessageButtonRightContent = value;
                RaisePropertyChanged(nameof(SMessageButtonRightContent));
            }
        }

        private Visibility sMessageButtonRightVisibility = Visibility.Hidden;
        public Visibility SMessageButtonRightVisibility
        {
            get => sMessageButtonRightVisibility;
            set
            {
                sMessageButtonRightVisibility = value;
                RaisePropertyChanged(nameof(SMessageButtonRightVisibility));
            }
        }

        private SMessageBoxResult sMessageBoxxResult;
        public SMessageBoxResult SMessageBoxxResult
        {
            get => sMessageBoxxResult;
            set
            {
                sMessageBoxxResult = value;
                RaisePropertyChanged(nameof(SMessageBoxxResult));
            }
        }

        private double sMessageBoxDismissAfterSeconds = 60;
        public double SMessageBoxDismissAfterSeconds
        {
            get => sMessageBoxDismissAfterSeconds;
            set
            {
                sMessageBoxDismissAfterSeconds = value;
                RaisePropertyChanged(nameof(SMessageBoxDismissAfterSeconds));
            }
        }
        #endregion Properties

        public System.Timers.Timer _timer;
        public System.Timers.Timer _countbackTimer;

        public SMessageBox(string Title, string Message, SMessageBoxButtons Buttons,
                                         NotificationIcon MessageBoxIcon,
                                         double DismissAfterSeconds = 90,
                                         Window? ownerWindow = null)
        {

            

            InitializeComponent();
            DataContext = this;

            _timer = new System.Timers.Timer(1000 * 60);
            _timer.Elapsed += Timer_Elapsed;
            
            _countbackTimer = new System.Timers.Timer(1000);
            _countbackTimer.Elapsed += CountbackTimer_Elapsed;

            this.PreviewKeyDown += new KeyEventHandler(HandleEsc);

            SMessageButtonClickCommand = new RelayCommand(SMessageButtonClick);

            CountDownSeconds = DismissAfterSeconds;
            SMessageBoxxResult = SMessageBoxResult.Cancel;
            SMessageBoxDismissAfterSeconds = DismissAfterSeconds;
            SMessageBody = Message.Replace("\n", Environment.NewLine);
            SMessageTitle = Title;
            SMessageBoxIcon = $@"\Images\MessageIcons\{MessageBoxIcon}.png";

            if (Buttons == SMessageBoxButtons.YesNo)
            {
                SMessageButtonLeftVisibility = Visibility.Visible;
                SMessageButtonRightVisibility = Visibility.Visible;
                SMessageButtonLeftContent = "Yes";
                SMessageButtonRightContent = "No";
            }
            else if (Buttons == SMessageBoxButtons.Ok)
            {
                SMessageButtonLeftVisibility = Visibility.Hidden;
                SMessageButtonRightVisibility = Visibility.Visible;
                SMessageButtonRightContent = "Ok";
            }
            else if (Buttons == SMessageBoxButtons.Close)
            {
                SMessageButtonLeftVisibility = Visibility.Hidden;
                SMessageButtonRightVisibility = Visibility.Visible;
                SMessageButtonRightContent = "Close";
            }
            else if (Buttons == SMessageBoxButtons.OkCancel)
            {
                SMessageButtonLeftVisibility = Visibility.Visible;
                SMessageButtonRightVisibility = Visibility.Visible;
                SMessageButtonLeftContent = "Ok";
                SMessageButtonRightContent = "Cancel";
            }
            else if (Buttons == SMessageBoxButtons.TryAgainClose)
            {
                SMessageButtonLeftVisibility = Visibility.Visible;
                SMessageButtonRightVisibility = Visibility.Visible;
                SMessageButtonLeftContent = "Try again";
                SMessageButtonRightContent = "Close";
            }
            else if (Buttons == SMessageBoxButtons.YesLater)
            {
                SMessageButtonLeftVisibility = Visibility.Visible;
                SMessageButtonRightVisibility = Visibility.Visible;
                SMessageButtonLeftContent = "Yes";
                SMessageButtonRightContent = "Do it later";
            }

            if (ownerWindow is not null)
            {
                Owner = ownerWindow;
            }

            ApplyInitialLayout(ownerWindow);

            Loaded += SMessageBox_Loaded;
            Closed += SMessageBox_Closed;
        }

        private void ApplyInitialLayout(Window? ownerWindow)
        {
            const double horizontalPadding = 38;
            const double minWidth = 400;
            const double maxWidth = 640;
            const double minHeight = 100;
            const double headerHeight = 30;
            const double footerHeight = 52;
            const double verticalPadding = 16;

            double contentMaxWidth = maxWidth - horizontalPadding;
            Size bodySize = MeasureMessageBody(contentMaxWidth);

            double textWidth = Math.Ceiling(bodySize.Width);
            double targetWidth = Math.Clamp(Math.Max(textWidth + horizontalPadding, minWidth), minWidth, maxWidth);

            bodySize = MeasureMessageBody(targetWidth - horizontalPadding);
            double textHeight = Math.Ceiling(bodySize.Height);

            double maxHeight = SystemParameters.WorkArea.Height * 0.85;
            double targetHeight = Math.Max(minHeight, headerHeight + footerHeight + verticalPadding + textHeight);
            if (targetHeight > maxHeight)
            {
                targetHeight = maxHeight;
                MessageBodyText.MaxHeight = maxHeight - headerHeight - footerHeight - verticalPadding;
            }

            MessageBodyText.MaxWidth = targetWidth - horizontalPadding;
            Width = targetWidth;
            Height = targetHeight;

            CenterOnOwnerWindow(ownerWindow);
        }

        private Size MeasureMessageBody(double maxTextWidth)
        {
            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var formatted = new FormattedText(
                SMessageBody ?? "",
                CultureInfo.CurrentUICulture,
                FlowDirection,
                new Typeface(MessageBodyText.FontFamily, MessageBodyText.FontStyle, MessageBodyText.FontWeight, MessageBodyText.FontStretch),
                MessageBodyText.FontSize,
                Brushes.Black,
                pixelsPerDip)
            {
                MaxTextWidth = Math.Max(1, maxTextWidth),
                TextAlignment = TextAlignment.Left,
            };

            return new Size(formatted.Width, formatted.Height);
        }

        private void CenterOnOwnerWindow(Window? ownerWindow)
        {
            Rect targetRect;
            Window? owner = ownerWindow ?? Owner;
            if (owner is not null)
            {
                double ownerWidth = owner.ActualWidth > 0 ? owner.ActualWidth : owner.Width;
                double ownerHeight = owner.ActualHeight > 0 ? owner.ActualHeight : owner.Height;
                if (ownerWidth > 0 && ownerHeight > 0)
                {
                    targetRect = new Rect(
                        owner.Left + (ownerWidth - Width) / 2,
                        owner.Top + (ownerHeight - Height) / 2,
                        Width,
                        Height);
                }
                else
                {
                    targetRect = CenterOnWorkArea();
                }
            }
            else
            {
                targetRect = CenterOnWorkArea();
            }

            Rect workArea = SystemParameters.WorkArea;
            Left = Math.Max(workArea.Left, Math.Min(targetRect.Left, workArea.Right - Width));
            Top = Math.Max(workArea.Top, Math.Min(targetRect.Top, workArea.Bottom - Height));
        }

        private Rect CenterOnWorkArea()
        {
            Rect workArea = SystemParameters.WorkArea;
            return new Rect(
                workArea.Left + (workArea.Width - Width) / 2,
                workArea.Top + (workArea.Height - Height) / 2,
                Width,
                Height);
        }

        private bool _backdropShown;

        private void SMessageBox_Loaded(object sender, RoutedEventArgs e)
        {
            // WPF re-broadcasts Loaded on window resize; only attach the backdrop once per dialog.
            if (_backdropShown)
            {
                return;
            }

            _backdropShown = true;
            WindowModalBackdrop.Show(Owner);
        }

        private void SMessageBox_Closed(object? sender, EventArgs e)
        {
            WindowModalBackdrop.Hide(Owner);
        }

        private void CountbackTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CountDownSeconds--;
                countDownTimer.Text = CountDownSeconds.ToString();
            });
        }

        private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (MainViewModel.Instance is not null)
                MainViewModel.Instance.SMessageBoxxResult = SMessageBoxResult.Cancel;
            else
                SplashViewModel.Instance.SMessageBoxxResult = SMessageBoxResult.Cancel;
            _timer.Stop();
            _countbackTimer.Stop();
            Application.Current.Dispatcher.Invoke(Close);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _timer.Interval = 1000 * SMessageBoxDismissAfterSeconds;
            _timer.Start();
            _countbackTimer.Start();
            xRightButton.Focus();
        }

        private void SMessageButtonClick(object obj)
        {
            string result = (string)obj;

            if (result.Equals("yes", StringComparison.CurrentCultureIgnoreCase))
            {
                if (MainViewModel.Instance is not null)
                    MainViewModel.Instance.SMessageBoxxResult = SMessageBoxResult.Yes;
                else
                    SplashViewModel.Instance.SMessageBoxxResult = SMessageBoxResult.Yes;
            }
            else if (result.Equals("try again", StringComparison.CurrentCultureIgnoreCase))
            {
                if (MainViewModel.Instance is not null)
                    MainViewModel.Instance.SMessageBoxxResult = SMessageBoxResult.TryAgain;
                else
                    SplashViewModel.Instance.SMessageBoxxResult = SMessageBoxResult.TryAgain;
            }
            else if (result.Equals("no", StringComparison.CurrentCultureIgnoreCase))
            {
                if (MainViewModel.Instance is not null)
                    MainViewModel.Instance.SMessageBoxxResult = SMessageBoxResult.No;
                else
                    SplashViewModel.Instance.SMessageBoxxResult = SMessageBoxResult.No;
            }
            else if (result.Equals("ok", StringComparison.CurrentCultureIgnoreCase))
            {
                if (MainViewModel.Instance is not null)
                    MainViewModel.Instance.SMessageBoxxResult = SMessageBoxResult.Ok;
                else
                    SplashViewModel.Instance.SMessageBoxxResult = SMessageBoxResult.Ok;
            }
            else if (result.Equals("close", StringComparison.CurrentCultureIgnoreCase))
            {
                if (MainViewModel.Instance is not null)
                    MainViewModel.Instance.SMessageBoxxResult = SMessageBoxResult.Close;
                else
                    SplashViewModel.Instance.SMessageBoxxResult = SMessageBoxResult.Close;
            }
            else if (result.Equals("cancel", StringComparison.CurrentCultureIgnoreCase))
            {
                if (MainViewModel.Instance is not null)
                    MainViewModel.Instance.SMessageBoxxResult = SMessageBoxResult.Cancel;
                else
                    SplashViewModel.Instance.SMessageBoxxResult = SMessageBoxResult.Cancel;
            }
            else if (result.Equals("do it later", StringComparison.CurrentCultureIgnoreCase))
            {
                if (MainViewModel.Instance is not null)
                    MainViewModel.Instance.SMessageBoxxResult = SMessageBoxResult.Later;
                else
                    SplashViewModel.Instance.SMessageBoxxResult = SMessageBoxResult.Later;
            }

            _timer.Stop();
            _countbackTimer.Stop();

            Close();
        }

        private void Border_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                try
                {
                    this.DragMove();
                }
                catch { }
        }

        private void HandleEsc(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (MainViewModel.Instance is not null)
                    MainViewModel.Instance.SMessageBoxxResult = SMessageBoxResult.Cancel;
                else
                    SplashViewModel.Instance.SMessageBoxxResult = SMessageBoxResult.Cancel;
                _timer.Stop();
                _countbackTimer.Stop();
                Close();
            }
        }
    }
}
