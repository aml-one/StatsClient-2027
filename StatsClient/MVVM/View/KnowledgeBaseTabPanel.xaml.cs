using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using StatsClient.MVVM.ViewModel;

namespace StatsClient.MVVM.View;

public partial class KnowledgeBaseTabPanel : UserControl
{
    private const string ImageFileFilter =
        "Images (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp";

    private const string EditorAttachmentFilter =
        "Images and PDF (*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.pdf)|*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.pdf|Images (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp|PDF (*.pdf)|*.pdf";

    public KnowledgeBaseTabPanel()
    {
        InitializeComponent();
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

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

        await AddEditorAttachmentsAsync(files);
    }

    private void EditorImageDropZone_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void EditorImageDropZone_Click(object sender, MouseButtonEventArgs e)
    {
        if (Vm is null || !Vm.KnowledgeBaseIsEditMode)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = EditorAttachmentFilter,
            CheckFileExists = true,
            Multiselect = true,
            Title = "Add images or PDF"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await AddEditorAttachmentsAsync(dialog.FileNames);
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
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = ImageFileFilter,
            CheckFileExists = true,
            Multiselect = false,
            Title = "Choose scan photo"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await SetVisionSearchImageAsync(dialog.FileName);
    }

    private async Task AddEditorAttachmentsAsync(IEnumerable<string> files)
    {
        if (Vm is null)
        {
            return;
        }

        foreach (var file in files.Take(5))
        {
            if (!IsEditorAttachmentFile(file))
            {
                continue;
            }

            var bytes = await File.ReadAllBytesAsync(file);
            await Vm.KnowledgeBaseAddImageFromBytesAsync(bytes, Path.GetFileName(file));
        }
    }

    private async Task SetVisionSearchImageAsync(string filePath)
    {
        if (Vm is null || !IsImageFile(filePath))
        {
            return;
        }

        Vm.KnowledgeBaseSearchImageBytes = await File.ReadAllBytesAsync(filePath);
    }

    private void LinkEditor_LostFocus(object sender, RoutedEventArgs e) => Vm?.KnowledgeBaseEditorLinksChanged();

    private static bool IsEditorAttachmentFile(string path) =>
        IsImageFile(path) || IsPdfFile(path);

    private static bool IsImageFile(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPdfFile(string path) =>
        Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
}
