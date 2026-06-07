using System.Windows;
using StatsClient.KnowledgeBase.Core;

namespace StatsClient.MVVM.View;

public partial class KnowledgeBaseBackupPreviewWindow : Window
{
    public KnowledgeBaseBackupPreviewWindow(KnowledgeBaseCardSnapshot snapshot, DateTime backedUpUtc)
    {
        InitializeComponent();
        TitleText.Text = snapshot.Title;
        BackedUpText.Text = $"Backup from {backedUpUtc:yyyy-MM-dd HH:mm} UTC";
        BodyText.Text = snapshot.BodyText;
        TagsText.Text = string.Join(", ", snapshot.Tags);
        CategoryText.Text = snapshot.CategoryName ?? "(None)";
        LinksList.ItemsSource = snapshot.Links;
        ImagesList.ItemsSource = snapshot.Images
            .Select(i => i.ThumbnailBase64 is string b64 ? Convert.FromBase64String(b64) : null)
            .Where(b => b is not null)
            .ToList();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
