using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using StatsClient.MVVM.Core;
using StatsClient.MVVM.ViewModel;
using System.Windows.Input;
using System.Windows.Media;

namespace StatsClient.MVVM.View;

public partial class ServerLogTabPanel
{
    public WebView2 WebView => webview;

    public ServerLogTabPanel()
    {
        InitializeComponent();
        ApplyWebViewBackgroundFromScheme();
    }

    private void ApplyWebViewBackgroundFromScheme()
    {
        // WebView2.DefaultBackgroundColor is System.Drawing.Color — not a WPF Brush/Color resource.
        var mediaColor = ColorSchemeResourceCatalog.GetColor("WindowBackgroundColor");
        webview.DefaultBackgroundColor = System.Drawing.Color.FromArgb(
            mediaColor.A, mediaColor.R, mediaColor.G, mediaColor.B);
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
