using StatsClient.MVVM.Core;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace StatsClient.MVVM.View;

public partial class ImagePreviewWindow : Window, INotifyPropertyChanged
{
    public string? ImageSource { get; set; }
    
    private string? fileName;
    public string? FileName
    {
        get => fileName;
        set
        {
            fileName = value;
            RaisePropertyChanged(nameof(FileName));
        }
    }

    public RelayCommand CloseWindowCommand { get; set; }

    public ImagePreviewWindow()
    {
        InitializeComponent();
        DataContext = this;

        this.PreviewKeyDown += new KeyEventHandler(HandleEsc);
        CloseWindowCommand = new RelayCommand(o => Close());

        MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight;
        MaxWidth = SystemParameters.MaximizedPrimaryScreenWidth;
    }

    
    public event PropertyChangedEventHandler? PropertyChanged;
    public void RaisePropertyChanged([CallerMemberName] string? propertyname = null)
    {
        PropertyChanged?.Invoke(typeof(ObservableObject), new PropertyChangedEventArgs(propertyname));
    }

    private void HandleEsc(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape || e.Key == Key.Enter)
            Close();
    }

    public void ShowDialog(Window window, string imageSource)
    {
        Height = OrderInfoWindow.StaticInstance.Height;
        
        FileInfo fileInfo = new (imageSource);
        this.Owner = window;
        this.ImageSource = imageSource;
        this.Title = fileInfo.Name.Replace(fileInfo.Extension, "");
        FileName = fileInfo.Name.Replace(fileInfo.Extension, "");

        image.ImagePath = ImageSource;

        this.ShowDialog();            
    }

    

    


    public void TitleBar_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            if (WindowState == WindowState.Maximized) 
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;
        }

        if (e.ChangedButton == MouseButton.Left)
            try
            {
                this.DragMove();
            }
            catch { }
    }

    private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            Close();
        }
    }
}
