using System.Windows;
using StatsClient.MVVM.Model;

namespace StatsClient.MVVM.View;

public partial class AccountInfoEntryWindow : Window
{
    public AccountInfoEntryWindow()
    {
        InitializeComponent();
        FriendlyNameBox.Focus();
    }

    public AccountInfoModel? Entry { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string friendlyName = FriendlyNameBox.Text.Trim();
        string category = CategoryBox.Text.Trim();
        string userName = UserNameBox.Text.Trim();
        string password = PasswordBox.Password;
        if (string.IsNullOrWhiteSpace(friendlyName)
            || string.IsNullOrWhiteSpace(category)
            || string.IsNullOrWhiteSpace(userName)
            || string.IsNullOrWhiteSpace(password))
        {
            MessageBox.Show(
                this,
                "Friendly name, category, username, and password are required.",
                "Passwords",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        Entry = new AccountInfoModel
        {
            FriendlyName = friendlyName,
            Category = category,
            SubCategory = SubCategoryBox.Text.Trim(),
            Website = WebsiteBox.Text.Trim(),
            ApplicationName = ApplicationNameBox.Text.Trim(),
            ApplicationPath = ApplicationPathBox.Text.Trim(),
            Credentials = new AccountCredential
            {
                UserName = userName,
                Password = password
            }
        };

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
