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

            if (NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER) != IntPtr.Zero)
                return true;

            int textLength = NativeMethods.GetWindowTextLength(hWnd);
            if (textLength == 0)
                return true;

            var exStyle = (uint)NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
            if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0)
                return true;

            if (!_vdHelper.IsWindowOnCurrentVirtualDesktop(hWnd))
                return true;

            var title = GetWindowText(hWnd, textLength);
            var className = GetClassName(hWnd);

            if (!NativeMethods.GetWindowRect(hWnd, out RECT rect))
                return true;

            if (rect.Width <= 0 || rect.Height <= 0)
                return true;

            windows.Add(new SpatialWindow(hWnd, title, className, rect));
            return true;
        }, IntPtr.Zero);

        App.LogException(new Exception($"[WindowEnumerator] Found {windows.Count} windows on current virtual desktop."));
        foreach (var w in windows)
        {
            App.LogException(new Exception($"[WindowEnumerator] Window: HWND={w.Hwnd}, Title={w.Title}, Rect={w.ScreenBounds.Left},{w.ScreenBounds.Top},{w.ScreenBounds.Width}x{w.ScreenBounds.Height}"));
        }

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
