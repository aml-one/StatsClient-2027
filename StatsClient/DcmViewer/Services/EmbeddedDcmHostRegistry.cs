using DCMViewer.Controls;

namespace DCMViewer.Services;

/// <summary>
/// Tracks embedded DCM canvas hosts so they can be torn down on application exit
/// (hidden embed windows are not always in Application.Windows).
/// </summary>
internal static class EmbeddedDcmHostRegistry
{
    private static readonly object Gate = new();
    private static readonly List<WeakReference<DcmViewerCanvasComponent>> Hosts = [];

    public static void Register(DcmViewerCanvasComponent host)
    {
        if (host is null)
        {
            return;
        }

        lock (Gate)
        {
            Hosts.RemoveAll(r => !r.TryGetTarget(out _));
            if (Hosts.Any(r => r.TryGetTarget(out var existing) && ReferenceEquals(existing, host)))
            {
                return;
            }

            Hosts.Add(new WeakReference<DcmViewerCanvasComponent>(host));
        }
    }

    public static void Unregister(DcmViewerCanvasComponent host)
    {
        lock (Gate)
        {
            Hosts.RemoveAll(r => !r.TryGetTarget(out var target) || ReferenceEquals(target, host));
        }
    }

    public static void ShutdownAll()
    {
        lock (Gate)
        {
            foreach (var reference in Hosts.ToList())
            {
                if (!reference.TryGetTarget(out var host))
                {
                    continue;
                }

                try
                {
                    host.ShutdownEmbeddedHost();
                }
                catch
                {
                }
            }

            Hosts.Clear();
        }
    }
}
