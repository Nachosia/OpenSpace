using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using OpenSpace.Win32;

namespace OpenSpace.Input;

internal sealed class HotKeyHelper : IDisposable
{
    private readonly Window _window;
    private readonly int _id;
    private HwndSource? _source;

    public event Action? HotKeyPressed;

    public HotKeyHelper(Window window, int id)
    {
        _window = window;
        _id = id;

        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero)
        {
            helper.EnsureHandle();
        }

        _source = HwndSource.FromHwnd(helper.Handle);
        if (_source != null)
        {
            _source.AddHook(WndProc);
        }
    }

    public bool Register(uint modifiers, uint vk)
    {
        var helper = new WindowInteropHelper(_window);
        return NativeMethods.RegisterHotKey(helper.Handle, _id, modifiers, vk);
    }

    public void Unregister()
    {
        var helper = new WindowInteropHelper(_window);
        NativeMethods.UnregisterHotKey(helper.Handle, _id);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == _id)
        {
            HotKeyPressed?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        if (_source != null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }
    }
}
