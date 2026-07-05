using System.Runtime.InteropServices;
using OpenSpace.Win32;

namespace OpenSpace.Win32;

internal sealed class VirtualDesktopHelper : IDisposable
{
    private readonly IVirtualDesktopManager _manager;

    public VirtualDesktopHelper()
    {
        var type = Type.GetTypeFromCLSID(new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A"));
        _manager = (IVirtualDesktopManager)Activator.CreateInstance(type!)!;
    }

    public bool IsWindowOnCurrentVirtualDesktop(IntPtr hWnd)
    {
        try
        {
            int hr = _manager.IsWindowOnCurrentVirtualDesktop(hWnd, out bool result);
            return hr >= 0 && result;
        }
        catch (COMException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (Marshal.IsComObject(_manager))
        {
            Marshal.ReleaseComObject(_manager);
        }
    }
}

[ComImport]
[Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IVirtualDesktopManager
{
    [PreserveSig]
    int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, [MarshalAs(UnmanagedType.Bool)] out bool onCurrentDesktop);

    [PreserveSig]
    int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);

    [PreserveSig]
    int MoveWindowToDesktop(IntPtr topLevelWindow, [MarshalAs(UnmanagedType.LPStruct)] Guid desktopId);
}
