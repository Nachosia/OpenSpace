using System.Text;
using OpenSpace.Core;

namespace OpenSpace.Win32;

internal sealed class WindowEnumerator : IDisposable
{
    private readonly VirtualDesktopHelper _vdHelper;
    private readonly HashSet<IntPtr> _ownWindows = new();

    public WindowEnumerator()
    {
        _vdHelper = new VirtualDesktopHelper();
    }

    public void TrackOwnWindow(IntPtr hwnd)
    {
        _ownWindows.Add(hwnd);
    }

    public IReadOnlyList<SpatialWindow> Enumerate()
    {
        var windows = new List<SpatialWindow>();
        var shellWindow = NativeMethods.GetShellWindow();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hWnd))
                return true;

            if (hWnd == shellWindow)
                return true;

            if (_ownWindows.Contains(hWnd))
                return true;

            // Skip owned windows (e.g. modal dialogs) unless they are top-level explorers
            if (NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER) != IntPtr.Zero)
                return true;

            // Skip windows with no title
            int textLength = NativeMethods.GetWindowTextLength(hWnd);
            if (textLength == 0)
                return true;

            // Skip tool windows
            var exStyle = (uint)NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
            if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0)
                return true;

            // Skip cloaked windows (DWM thumbnails will fail anyway, but this avoids listing them)
            // Note: checking DWM cloaked attribute could be added later if needed.

            if (!_vdHelper.IsWindowOnCurrentVirtualDesktop(hWnd))
                return true;

            var title = GetWindowText(hWnd, textLength);
            var className = GetClassName(hWnd);

            if (!NativeMethods.GetWindowRect(hWnd, out RECT rect))
                return true;

            // Skip zero-size windows
            if (rect.Width <= 0 || rect.Height <= 0)
                return true;

            windows.Add(new SpatialWindow(hWnd, title, className, rect));
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private static string GetWindowText(IntPtr hWnd, int length)
    {
        var buffer = new char[length + 1];
        NativeMethods.GetWindowText(hWnd, buffer, buffer.Length);
        return new string(buffer).TrimEnd('\0');
    }

    private static string GetClassName(IntPtr hWnd)
    {
        var buffer = new char[256];
        NativeMethods.GetClassName(hWnd, buffer, buffer.Length);
        return new string(buffer).TrimEnd('\0');
    }

    public void Dispose()
    {
        _vdHelper.Dispose();
    }
}
