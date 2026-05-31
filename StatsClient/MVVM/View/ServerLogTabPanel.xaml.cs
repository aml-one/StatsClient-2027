using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using StatsClient.MVVM.ViewModel;
using System.Windows.Input;

namespace StatsClient.MVVM.View;

public partial class ServerLogTabPanel
{
    public WebView2 WebView => webview;

    public ServerLogTabPanel()
    {
        InitializeComponent();
    }

    private void Webview_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        webview.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
        webview.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

        MainViewModel.Instance.ServerLogWebViewIsInitialized = true;
    }

    private void Webview_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        MainViewModel.Instance.ScrollServerLogToBottom = false;
    }
}
