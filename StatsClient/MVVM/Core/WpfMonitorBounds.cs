using System.Runtime.InteropServices;
using System.Windows;

namespace StatsClient.MVVM.Core;

/// <summary>Win32 monitor bounds for borderless exclusive fullscreen (covers taskbar).</summary>
internal static class WpfMonitorBounds
{
    private const uint MonitorDefaultToNearest = 2;

    public static Rect GetBoundsForWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return GetPrimaryMonitorBounds();
        }

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return GetPrimaryMonitorBounds();
        }

        var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info))
        {
            return GetPrimaryMonitorBounds();
        }

        return new Rect(
            info.rcMonitor.Left,
            info.rcMonitor.Top,
            info.rcMonitor.Right - info.rcMonitor.Left,
            info.rcMonitor.Bottom - info.rcMonitor.Top);
    }

    public static Rect GetPrimaryMonitorBounds() =>
        new(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int cbSize;
        public NativeRect rcMonitor;
        public NativeRect rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
