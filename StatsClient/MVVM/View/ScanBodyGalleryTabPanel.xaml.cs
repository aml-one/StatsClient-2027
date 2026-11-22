using System.IO;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Input;

using Microsoft.Win32;

using StatsClient.MVVM.Core;

using StatsClient.MVVM.Core.ScanBodyGallery;

using StatsClient.MVVM.ViewModel;



namespace StatsClient.MVVM.View;



public partial class ScanBodyGalleryTabPanel : UserControl

{

    private const string ImageFileFilter =

        "Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg";



    public ScanBodyGalleryTabPanel()

    {

        InitializeComponent();

        PreviewKeyDown += OnPreviewKeyDown;

    }



    private MainViewModel? Vm => DataContext as MainViewModel;



    private async void VisionSearchDropZone_Drop(object sender, DragEventArgs e)

    {

        if (Vm is null || !e.Data.GetDataPresent(DataFormats.FileDrop))

            return;



        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)

            return;



        string? file = files.FirstOrDefault(ScanBodyGalleryImageHelper.IsAllowedImageFile);

        if (file is null)

            return;



        await SetVisionSearchImageAsync(file);

    }



    private void VisionSearchDropZone_PreviewDragOver(object sender, DragEventArgs e)

    {

        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

        e.Handled = true;

    }



    private async void VisionSearchDropZone_Click(object sender, MouseButtonEventArgs e)

    {

        if (Vm is null)

            return;



        var dialog = new OpenFileDialog

        {

            Filter = ImageFileFilter,

            CheckFileExists = true,

            Multiselect = false,

            Title = "Choose scan body photo"

        };



        if (dialog.ShowDialog() != true)

            return;



        await SetVisionSearchImageAsync(dialog.FileName);

    }



    private async Task SetVisionSearchImageAsync(string filePath)

    {

        if (Vm is null || !ScanBodyGalleryImageHelper.IsAllowedImageFile(filePath))

            return;



        Vm.SetScanBodyGallerySearchImageBytes(await File.ReadAllBytesAsync(filePath));

    }



    private void OnPreviewKeyDown(object sender, KeyEventArgs e)

    {

        if (Vm is null || !IsKeyboardFocusWithin)

            return;



        bool isPaste =

            (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control) ||

            (e.Key == Key.Insert && Keyboard.Modifiers == ModifierKeys.Shift);



        if (!isPaste)

            return;



        if (PrescriptionMakerImageHelper.TryGetClipboardImagePng(out byte[] pngBytes))

        {

            Vm.SetScanBodyGallerySearchImageBytes(pngBytes);

            e.Handled = true;

        }

    }

}

