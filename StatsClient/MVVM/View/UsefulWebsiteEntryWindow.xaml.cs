using System.Windows;

using System.Windows.Input;

using System.Windows.Media;

using StatsClient.MVVM.Core.UsefulWebsites;

using StatsClient.MVVM.Model;



namespace StatsClient.MVVM.View;



public partial class UsefulWebsiteEntryWindow : Window

{

    public UsefulWebsiteEntryWindow(UsefulWebsiteModel? existing = null)

    {

        InitializeComponent();

        PreviewKeyDown += OnPreviewKeyDown;



        if (existing is not null)

        {

            SetWindowCopy("Edit useful website");

            TitleBox.Text = existing.Title;

            UrlBox.Text = existing.Url;

            TagsBox.Text = existing.TagsDisplay;

            _websiteId = existing.WebsiteId;

            _sortOrder = existing.SortOrder;

        }

        else

        {

            SetWindowCopy("Add useful website");

            TitleBox.Focus();

        }

    }



    private readonly int _websiteId;

    private readonly int _sortOrder;



    public UsefulWebsiteModel? Entry { get; private set; }



    private void SetWindowCopy(string text)

    {

        Title = text;

        TitleBarText.Text = text;

        HeaderText.Text = text;

    }



    private void Save_Click(object sender, RoutedEventArgs e)

    {

        string title = TitleBox.Text.Trim();

        string url = UsefulWebsiteRepository.NormalizeUrl(UrlBox.Text);

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))

        {

            MessageBox.Show(

                this,

                "Title and website URL are required.",

                "Useful websites",

                MessageBoxButton.OK,

                MessageBoxImage.Warning);

            return;

        }



        Entry = new UsefulWebsiteModel

        {

            WebsiteId = _websiteId,

            Title = title,

            Url = url,

            SortOrder = _sortOrder,

            Tags = UsefulWebsiteRepository.NormalizeTags([TagsBox.Text]),

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



        try

        {

            DragMove();

        }

        catch

        {

        }

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

        }

    }

}

