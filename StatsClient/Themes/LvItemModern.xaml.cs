using StatsClient.MVVM.View;
using StatsClient.MVVM.ViewModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace StatsClient.Themes;

public partial class LvItemModern
{
    public LvItemModern()
    {
        InitializeComponent();
    }

    private void MenuItemOpenUpOrderInfoWindow_Click(object sender, RoutedEventArgs e)
    {
        MainViewModel.Instance.OpenUpOrderInfoWindow();
    }


    private void MenuItemOpenUpRenameOrderWindow_Click(object sender, RoutedEventArgs e)
    {
        MainViewModel.Instance.OpenUpRenameOrderWindow();
    }

    private void MenuItemLabnext_Click(object sender, RoutedEventArgs e)
    {
        MainViewModel.Instance.IsLabnextLookupIsOpen = true;
        MainViewModel.Instance.ListUpdateable = false;
    }

    private void MenuItemGenerateStCopy_Click(object sender, RoutedEventArgs e)
    {
        MainViewModel.Instance.GenerateStCopy();
    }

    private void ContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        MainViewModel.Instance.ListUpdateable = false;
    }

    private void ContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        MainViewModel.Instance.ListUpdateable = true;
    }

    private void MenuItemExploreFolder_Click(object sender, RoutedEventArgs e)
    {
        MainViewModel.Instance.ExploreOrderFolder();
    }

    private void AccountInfosShowPassword_Button_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Tag is not null)
        {
            string password = ((Button)sender).Tag.ToString()!;
            if ((TextBlock)((Grid)((Button)sender).Parent).Children[3] is not null) {
                try
                {
                    TextBlock passwordTb = (TextBlock)((Grid)((Button)sender).Parent).Children[3];

                    ShowPassword(passwordTb, password);
                }
                catch (Exception)
                {
                }
            }
        }
        else
        {
            MessageBox.Show(MainWindow.Instance, "No password found!", "Password missing", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void ShowPassword(TextBlock passwordTb, string password)
    {
        passwordTb.Text = password;
        await Task.Delay(2000);

        passwordTb.Text = "------";
    }

    private void Shade_A1_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("A1");
    private void Shade_A2_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("A2");
    private void Shade_A3_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("A3");
    private void Shade_A35_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("A35");
    private void Shade_A4_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("A4");

    private void Shade_B1_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("B1");
    private void Shade_B2_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("B2");
    private void Shade_B3_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("B3");
    private void Shade_B4_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("B4");

    private void Shade_C1_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("C1");
    private void Shade_C2_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("C2");
    private void Shade_C3_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("C3");
    private void Shade_C4_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("C4");

    private void Shade_D2_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("D2");
    private void Shade_D3_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("D3");
    private void Shade_D4_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("D4");


    private void Shade_1M1_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("1M1");
    private void Shade_1M2_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("1M2");
    private void Shade_2M1_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("2M1");
    private void Shade_2M2_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("2M2");
    private void Shade_2M3_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("2M3");
    private void Shade_3M1_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("3M1");
    private void Shade_3M2_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("3M2");
    private void Shade_3M3_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("3M3");
    private void Shade_4M1_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("4M1");
    private void Shade_4M2_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("4M2");
    private void Shade_4M3_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("4M3");
    private void Shade_5M1_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("5M1");
    private void Shade_5M2_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("5M2");
    private void Shade_5M3_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("5M3");


    private void Shade_2L15_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("2L15");
    private void Shade_2L25_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("2L25");
    private void Shade_3L15_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("3L15");
    private void Shade_3L25_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("3L25");
    private void Shade_4L15_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("4L15");
    private void Shade_4L25_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("4L25");
    private void Shade_2R15_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("2R15");
    private void Shade_2R25_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("2R25");
    private void Shade_3R15_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("3R15");
    private void Shade_3R25_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("3R25");
    private void Shade_4R15_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("4R15");
    private void Shade_4R25_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("4R25");

    private void Shade_0M1_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("0M1");
    private void Shade_0M2_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("0M2");
    private void Shade_0M3_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("0M3");
    private void Shade_0M4_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("0M4");
    private void Shade_BL1_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("BL1");
    private void Shade_BL2_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("BL2");
    private void Shade_BL3_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("BL3");
    private void Shade_BL4_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("BL4");
    private void Shade_010_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("010");
    private void Shade_020_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("020");
    private void Shade_030_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("030");
    private void Shade_040_Click(object sender, RoutedEventArgs e) => MainViewModel.Instance.SetShadeClick("040");

    
}