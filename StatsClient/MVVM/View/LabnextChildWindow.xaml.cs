using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace StatsClient.MVVM.View
{
    /// <summary>
    /// Interaction logic for LabnextChildWindow.xaml
    /// </summary>
    public partial class LabnextChildWindow : Window
    {
        public LabnextChildWindow()
        {
            InitializeComponent();
        }
        
        public LabnextChildWindow(string url)
        {
            InitializeComponent();

            webviewLabnext.Source = new Uri(url);
            MessageBox.Show(url);
        }

        private void Webview_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            webviewLabnext.CoreWebView2.Settings.IsPasswordAutosaveEnabled = true;
            webviewLabnext.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            webviewLabnext.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webviewLabnext.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
            webviewLabnext.CoreWebView2.Settings.IsScriptEnabled = true;
            webviewLabnext.CoreWebView2.Settings.IsWebMessageEnabled = true;
            webviewLabnext.CoreWebView2.Settings.IsZoomControlEnabled = false;
            webviewLabnext.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
        }

        private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            //CoreWebView2 cwv2 = (CoreWebView2)sender;

            CoreWebView2Deferral deferral = e.GetDeferral();

            //LabnextChildWindow childWindow = new(e.Uri)
            //{
            //    Title = "Child Window"
            //};
            //childWindow.Show();

            //e.Handled = true;
            deferral.Complete();
        }

        private void Webview_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            progressBar.Visibility = Visibility.Visible;
        }

        private void Webview_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            progressBar.Visibility = Visibility.Hidden;
        }
    }
}
