using OpenSpace.Win32;

namespace OpenSpace.Win32;

internal static class WindowActivator
{
    public static void Activate(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return;

        // Restore if minimized
        if (NativeMethods.IsIconic(hWnd))
        {
            NativeMethods.ShowWindowAsync(hWnd, NativeMethods.SW_RESTORE);
        }

        uint targetThreadId = NativeMethods.GetWindowThreadProcessId(hWnd, out _);
        uint currentThreadId = NativeMethods.GetCurrentThreadId();

        bool attached = false;
        if (targetThreadId != currentThreadId)
        {
            attached = NativeMethods.AttachThreadInput(currentThreadId, targetThreadId, true);
        }

        NativeMethods.BringWindowToTop(hWnd);
        NativeMethods.SetForegroundWindow(hWnd);

        if (attached)
        {
            NativeMethods.AttachThreadInput(currentThreadId, targetThreadId, false);
        }
    }
}
