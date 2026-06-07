using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StatsClient.MVVM.ViewModel;

namespace StatsClient.MVVM.View;

public partial class KnowledgeBaseTabPanel : UserControl
{
    public KnowledgeBaseTabPanel()
    {
        InitializeComponent();
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    private async void ImageDropZone_Drop(object sender, DragEventArgs e)
    {
        if (Vm is null || !e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return;
        }

        foreach (var file in files.Take(5))
        {
            if (!IsImageFile(file))
            {
                continue;
            }

            var bytes = await File.ReadAllBytesAsync(file);
            await Vm.KnowledgeBaseAddImageFromBytesAsync(bytes, Path.GetFileName(file));
        }
    }

    private void ImageDropZone_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void EditorImageDropZone_Drop(object sender, DragEventArgs e)
    {
        if (Vm is null || !e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return;
        }

        foreach (var file in files.Take(5))
        {
            if (!IsImageFile(file))
            {
                continue;
            }

            var bytes = await File.ReadAllBytesAsync(file);
            await Vm.KnowledgeBaseAddImageFromBytesAsync(bytes, Path.GetFileName(file));
        }
    }

    private void EditorImageDropZone_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void VisionSearchDropZone_Drop(object sender, DragEventArgs e)
    {
        if (Vm is null || !e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
        {
            return;
        }

        var file = files.FirstOrDefault(IsImageFile);
        if (file is null)
        {
            return;
        }

        Vm.KnowledgeBaseSearchImageBytes = await File.ReadAllBytesAsync(file);
    }

    private void VisionSearchDropZone_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void LinkEditor_LostFocus(object sender, RoutedEventArgs e) => Vm?.KnowledgeBaseEditorLinksChanged();

    private static bool IsImageFile(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }
}
