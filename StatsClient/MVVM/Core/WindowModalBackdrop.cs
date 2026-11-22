using StatsClient.UserControls;
using Microsoft.Web.WebView2.Wpf;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace StatsClient.MVVM.Core;

/// <summary>
/// Shows a dimmed in-window backdrop on a parent <see cref="Window"/> while modal dialogs (e.g. <see cref="View.SMessageBox"/>) are open.
/// </summary>
internal static class WindowModalBackdrop
{
    private const string HostGridTag = "WindowModalBackdropHost";
    private const int OverlayZIndex = 12000;
    private const double ContentBlurRadius = 10;

    private static readonly ConditionalWeakTable<Window, HostEntry> Entries = new();

    public static void Show(Window? owner)
    {
        if (owner is null)
        {
            return;
        }

        owner.Dispatcher.Invoke(() =>
        {
            var entry = GetOrCreateEntry(owner);
            entry.ReferenceCount++;
            if (entry.ReferenceCount == 1)
            {
                ApplyContentBlur(entry.ContentRoot, enable: true);
                entry.Overlay.PlayEntrance();
            }
        });
    }

    public static void Hide(Window? owner)
    {
        if (owner is null)
        {
            return;
        }

        owner.Dispatcher.Invoke(() =>
        {
            if (!Entries.TryGetValue(owner, out var entry))
            {
                return;
            }

            entry.ReferenceCount = Math.Max(0, entry.ReferenceCount - 1);
            if (entry.ReferenceCount > 0)
            {
                return;
            }

            ApplyContentBlur(entry.ContentRoot, enable: false);
            entry.Overlay.PlayExit();
        });
    }

    public static void HideImmediately(Window? owner)
    {
        if (owner is null)
        {
            return;
        }

        owner.Dispatcher.Invoke(() =>
        {
            if (!Entries.TryGetValue(owner, out var entry))
            {
                return;
            }

            entry.ReferenceCount = 0;
            ApplyContentBlur(entry.ContentRoot, enable: false);
            entry.Overlay.HideImmediately();
        });
    }

    private static HostEntry GetOrCreateEntry(Window window)
    {
        if (Entries.TryGetValue(window, out var existing))
        {
            return existing;
        }

        var hostGrid = EnsureHostGrid(window);
        var contentRoot = hostGrid.Children
            .OfType<UIElement>()
            .FirstOrDefault(c => c is not WindowModalBackdropOverlay);

        var overlay = hostGrid.Children.OfType<WindowModalBackdropOverlay>().FirstOrDefault();
        if (overlay is null)
        {
            overlay = new WindowModalBackdropOverlay();
            hostGrid.Children.Add(overlay);
            Panel.SetZIndex(overlay, OverlayZIndex);
        }

        var entry = new HostEntry(hostGrid, contentRoot, overlay);
        Entries.Add(window, entry);
        return entry;
    }

    private static Grid EnsureHostGrid(Window window)
    {
        if (window.Content is Grid taggedHost && HostGridTag.Equals(taggedHost.Tag as string))
        {
            return taggedHost;
        }

        if (window.Content is Grid existingHost
            && existingHost.Children.OfType<WindowModalBackdropOverlay>().Any())
        {
            existingHost.Tag = HostGridTag;
            return existingHost;
        }

        var host = new Grid { Tag = HostGridTag };
        if (window.Content is UIElement existingContent)
        {
            EnsureWebViewSourcesAreNonNull(existingContent);
            // Content must be detached from the window before it can become a child of host.
            window.Content = null;
            host.Children.Add(existingContent);
        }

        window.Content = host;
        return host;
    }

    private static void EnsureWebViewSourcesAreNonNull(DependencyObject root)
    {
        if (root is WebView2 webView && webView.Source is null)
        {
            webView.Source = new Uri("about:blank");
        }

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            EnsureWebViewSourcesAreNonNull(VisualTreeHelper.GetChild(root, i));
        }
    }

    private static void ApplyContentBlur(UIElement? contentRoot, bool enable)
    {
        if (contentRoot is not FrameworkElement element)
        {
            return;
        }

        if (enable)
        {
            if (element.Effect is BlurEffect)
            {
                return;
            }

            element.SetValue(SavedEffectProperty, element.Effect);
            element.Effect = new BlurEffect
            {
                Radius = ContentBlurRadius,
                RenderingBias = RenderingBias.Performance,
            };
            return;
        }

        var saved = element.GetValue(SavedEffectProperty) as Effect;
        element.Effect = saved;
        element.ClearValue(SavedEffectProperty);
    }

    private static readonly DependencyProperty SavedEffectProperty =
        DependencyProperty.RegisterAttached(
            "SavedEffect",
            typeof(Effect),
            typeof(WindowModalBackdrop),
            new PropertyMetadata(null));

    private sealed class HostEntry(Grid hostGrid, UIElement? contentRoot, WindowModalBackdropOverlay overlay)
    {
        public Grid HostGrid { get; } = hostGrid;
        public UIElement? ContentRoot { get; } = contentRoot;
        public WindowModalBackdropOverlay Overlay { get; } = overlay;
        public int ReferenceCount;
    }
}
