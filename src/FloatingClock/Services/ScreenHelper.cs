using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace FloatingClock.Services;

public static class ScreenHelper
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect(ref RECT lprc, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private const uint MONITOR_DEFAULTTONULL = 0;
    private const uint MONITOR_DEFAULTTOPRIMARY = 1;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    /// <summary>
    /// Validates whether the given window rectangle intersects an active, connected monitor.
    /// If the window is off-screen (e.g. from a disconnected external monitor),
    /// it repositions the coordinates into the primary screen's work area.
    /// </summary>
    public static bool EnsureVisible(ref double left, ref double top, double width, double height)
    {
        var primaryWorkArea = SystemParameters.WorkArea;

        // If coordinates are invalid/NaN, center on primary work area
        if (double.IsNaN(left) || double.IsNaN(top) || double.IsInfinity(left) || double.IsInfinity(top))
        {
            left = primaryWorkArea.Left + Math.Max(20, (primaryWorkArea.Width - width) / 2);
            top = primaryWorkArea.Top + Math.Max(20, (primaryWorkArea.Height - height) / 2);
            ErrorLogger.LogInfo($"Initial window placement centered at ({left:F0}, {top:F0})");
            return true;
        }

        // Check against WPF VirtualScreen bounding box (covers all connected monitors in DIPs)
        double vLeft = SystemParameters.VirtualScreenLeft;
        double vTop = SystemParameters.VirtualScreenTop;
        double vWidth = SystemParameters.VirtualScreenWidth;
        double vHeight = SystemParameters.VirtualScreenHeight;

        // Require at least 50px overlap horizontally and vertically with the virtual screen
        bool outsideVirtualScreen =
            (left + width - 50 < vLeft) ||
            (left + 50 > vLeft + vWidth) ||
            (top + height - 50 < vTop) ||
            (top + 50 > vTop + vHeight);

        RECT rect = new RECT
        {
            Left = (int)Math.Floor(left),
            Top = (int)Math.Floor(top),
            Right = (int)Math.Ceiling(left + width),
            Bottom = (int)Math.Ceiling(top + height)
        };

        // Query Win32 for monitor under rectangle
        IntPtr hMonitor = MonitorFromRect(ref rect, MONITOR_DEFAULTTONULL);

        if (outsideVirtualScreen || hMonitor == IntPtr.Zero)
        {
            ErrorLogger.LogWarning(
                $"Window position ({left:F0}, {top:F0}, {width:F0}x{height:F0}) is not on any active monitor (disconnected display). Repositioning to primary monitor.");

            left = primaryWorkArea.Left + Math.Max(20, (primaryWorkArea.Width - width) / 2);
            top = primaryWorkArea.Top + Math.Max(20, (primaryWorkArea.Height - height) / 2);
            return true;
        }

        // The window intersects a monitor; verify it has adequate visible area (at least 60x60px)
        MONITORINFO info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (GetMonitorInfo(hMonitor, ref info))
        {
            int interLeft = Math.Max(rect.Left, info.rcWork.Left);
            int interTop = Math.Max(rect.Top, info.rcWork.Top);
            int interRight = Math.Min(rect.Right, info.rcWork.Right);
            int interBottom = Math.Min(rect.Bottom, info.rcWork.Bottom);

            int interWidth = Math.Max(0, interRight - interLeft);
            int interHeight = Math.Max(0, interBottom - interTop);

            if (interWidth < 60 || interHeight < 60)
            {
                ErrorLogger.LogWarning(
                    $"Window at ({left:F0}, {top:F0}) has insufficient visible area ({interWidth}x{interHeight}). Clamping to monitor work area.");

                left = Math.Clamp(left, info.rcWork.Left + 10, Math.Max(info.rcWork.Left + 10, info.rcWork.Right - width - 10));
                top = Math.Clamp(top, info.rcWork.Top + 10, Math.Max(info.rcWork.Top + 10, info.rcWork.Bottom - height - 10));
                return true;
            }
        }

        return false;
    }
}
