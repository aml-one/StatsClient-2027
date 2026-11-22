using System.Collections.ObjectModel;

using System.IO;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Input;

using System.Windows.Media;

using Microsoft.Win32;

using StatsClient.MVVM.Core;

using StatsClient.MVVM.Core.ScanBodyGallery;

using StatsClient.MVVM.Model;



namespace StatsClient.MVVM.View;



public partial class ScanBodyGalleryEntryWindow : Window

{

    private const string ImageFileFilter =

        "Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg";



    private readonly ObservableCollection<ScanBodyGalleryImageEditorItem> _images = [];

    private readonly int _entryId;

    private readonly int _sortOrder;



    public ScanBodyGalleryEntryWindow(ScanBodyGalleryEntryDetailModel? existing = null)

    {

        InitializeComponent();

        PreviewKeyDown += OnPreviewKeyDown;

        ImagesList.ItemsSource = _images;



        if (existing is not null)

        {

            SetWindowCopy("Edit scan body");

            BrandBox.Text = existing.Brand;

            SystemBox.Text = existing.SystemName;

            SizeBox.Text = existing.SizeLabel;

            OtherInfoBox.Text = existing.OtherInfo;

            TagsBox.Text = existing.ManualTagsText;

            _entryId = existing.EntryId;

            _sortOrder = existing.SortOrder;



            foreach (var image in existing.Images)

            {

                _images.Add(new ScanBodyGalleryImageEditorItem

                {

                    ImageId = image.ImageId,

                    ImageData = image.ImageData,

                    ThumbnailData = image.ThumbnailData,

                    FileName = image.FileName,

                    ContentType = image.ContentType,

                });

            }

        }

        else

        {

            SetWindowCopy("Add scan body");

            BrandBox.Focus();

        }



        UpdateAutoTagsPreview();

    }



    public ScanBodyGalleryEntryDetailModel? Entry { get; private set; }



    private void SetWindowCopy(string text)

    {

        Title = text;

        TitleBarText.Text = text;

        HeaderText.Text = text;

    }



    private void Field_TextChanged(object sender, TextChangedEventArgs e) => UpdateAutoTagsPreview();



    private void UpdateAutoTagsPreview()

    {

        var tags = ScanBodyGalleryTagHelper.BuildTags(

            BrandBox.Text,

            SystemBox.Text,

            SizeBox.Text,

            OtherInfoBox.Text,

            ScanBodyGalleryTagHelper.NormalizeTags([TagsBox.Text]));

        AutoTagsPreview.Text = tags.Count == 0

            ? "Auto tags: (none yet)"

            : $"Auto tags: {string.Join(", ", tags)}";

    }



    private async void ImageDropZone_Drop(object sender, DragEventArgs e)

    {

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))

            return;



        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)

            return;



        await AddImagesFromPathsAsync(files);

    }



    private void ImageDropZone_PreviewDragOver(object sender, DragEventArgs e)

    {

        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

        e.Handled = true;

    }



    private async void ImageDropZone_Click(object sender, MouseButtonEventArgs e)

    {

        var dialog = new OpenFileDialog

        {

            Filter = ImageFileFilter,

            CheckFileExists = true,

            Multiselect = true,

            Title = "Add scan body images"

        };



        if (dialog.ShowDialog() != true)

            return;



        await AddImagesFromPathsAsync(dialog.FileNames);

    }



    private async Task AddImagesFromPathsAsync(IEnumerable<string> paths)

    {

        foreach (string path in paths)

        {

            if (!ScanBodyGalleryImageHelper.IsAllowedImageFile(path))

                continue;



            if (_images.Count >= ScanBodyGalleryImageHelper.MaxImagesPerEntry)

                break;



            try

            {

                byte[] bytes = await File.ReadAllBytesAsync(path);

                AddImageBytes(bytes, Path.GetFileName(path));

            }

            catch

            {

            }

        }

    }



    private void AddImageBytes(byte[] bytes, string? fileName)

    {

        if (_images.Count >= ScanBodyGalleryImageHelper.MaxImagesPerEntry)

            return;



        try

        {

            var prepared = ScanBodyGalleryImageHelper.PrepareImage(bytes, fileName);

            _images.Add(new ScanBodyGalleryImageEditorItem

            {

                ImageData = prepared.ImageData,

                ThumbnailData = prepared.ThumbnailData,

                FileName = prepared.FileName,

                ContentType = prepared.ContentType,

            });

        }

        catch (Exception ex)

        {

            MessageBox.Show(this, ex.Message, "Scan body gallery", MessageBoxButton.OK, MessageBoxImage.Warning);

        }

    }



    private void RemoveImage_Click(object sender, RoutedEventArgs e)

    {

        if (sender is Button { DataContext: ScanBodyGalleryImageEditorItem item })

            _images.Remove(item);

    }



    private void Save_Click(object sender, RoutedEventArgs e)

    {

        if (_images.Count == 0)

        {

            MessageBox.Show(this, "Add at least one image.", "Scan body gallery", MessageBoxButton.OK, MessageBoxImage.Warning);

            return;

        }



        if (string.IsNullOrWhiteSpace(BrandBox.Text))

        {

            MessageBox.Show(this, "Brand is required.", "Scan body gallery", MessageBoxButton.OK, MessageBoxImage.Warning);

            BrandBox.Focus();

            return;

        }



        Entry = new ScanBodyGalleryEntryDetailModel

        {

            EntryId = _entryId,

            Brand = BrandBox.Text.Trim(),

            SystemName = SystemBox.Text.Trim(),

            SizeLabel = SizeBox.Text.Trim(),

            OtherInfo = OtherInfoBox.Text.Trim(),

            ManualTagsText = TagsBox.Text.Trim(),

            SortOrder = _sortOrder,

            Images = _images.Select(i => new ScanBodyGalleryImageModel

            {

                ImageId = i.ImageId,

                ImageData = i.ImageData,

                ThumbnailData = i.ThumbnailData,

                FileName = i.FileName,

                ContentType = i.ContentType,

            }).ToList(),

        };



        DialogResult = true;

        Close();

    }



    private void Cancel_Click(object sender, RoutedEventArgs e)

    {

        DialogResult = false;

        Close();

    }



    private void CloseTitleBar_Click(object sender, RoutedEventArgs e)

    {

        DialogResult = false;

        Close();

    }



    private void TitleBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)

    {

        if (e.ChangedButton != MouseButton.Left || IsClickInsideCloseButton(e.OriginalSource as DependencyObject))

            return;



        try { DragMove(); }

        catch { }

    }



    private bool IsClickInsideCloseButton(DependencyObject? source)

    {

        while (source is not null)

        {

            if (ReferenceEquals(source, CloseTitleBarButton))

                return true;

            source = VisualTreeHelper.GetParent(source);

        }



        return false;

    }



    private void OnPreviewKeyDown(object sender, KeyEventArgs e)

    {

        if (e.Key == Key.Escape)

        {

            DialogResult = false;

            Close();

            return;

        }



        bool isPaste =

            (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control) ||

            (e.Key == Key.Insert && Keyboard.Modifiers == ModifierKeys.Shift);



        if (!isPaste)

            return;



        if (PrescriptionMakerImageHelper.TryGetClipboardImagePng(out byte[] pngBytes))

        {

            AddImageBytes(pngBytes, "clipboard.png");

            e.Handled = true;

        }

    }

}

