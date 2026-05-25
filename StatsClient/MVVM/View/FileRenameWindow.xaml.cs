using StatsClient.MVVM.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace StatsClient.MVVM.View;

public partial class FileRenameWindow : Window, INotifyPropertyChanged
{
    private FileRenameWindow? instance;
    public FileRenameWindow Instance
    {
        get => instance!;
        set
        {
            instance = value;
            RaisePropertyChanged(nameof(Instance));
        }
    }

    private static FileRenameWindow? staticInstance;
    public static FileRenameWindow StaticInstance
    {
        get => staticInstance!;
        set
        {
            staticInstance = value;
            RaisePropertyChangedStatic(nameof(StaticInstance));
        }
    }

    private string originalFileName = "";
    public string OriginalFileName
    {
        get => originalFileName;
        set
        {
            originalFileName = value;
            RaisePropertyChanged(nameof(OriginalFileName));
        }
    }

    public static event PropertyChangedEventHandler? PropertyChangedStatic;
    public event PropertyChangedEventHandler? PropertyChanged;

    public static void RaisePropertyChangedStatic([CallerMemberName] string? propertyname = null)
    {
        PropertyChangedStatic?.Invoke(typeof(ObservableObject), new PropertyChangedEventArgs(propertyname));
    }
    public void RaisePropertyChanged([CallerMemberName] string? propertyname = null)
    {
        PropertyChanged?.Invoke(typeof(ObservableObject), new PropertyChangedEventArgs(propertyname));
    }
    public FileRenameWindow()
    {
        Instance = this;
        StaticInstance = this;
        InitializeComponent();
        this.PreviewKeyDown += new KeyEventHandler(HandleEsc);
    }

    public FileRenameWindow(string filepath)
    {
        OriginalFileName = filepath;
        Instance = this;
        StaticInstance = this;
        InitializeComponent();
        this.PreviewKeyDown += new KeyEventHandler(HandleEsc);
    }
    
    private void HandleEsc(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }

}
