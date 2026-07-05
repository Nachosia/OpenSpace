using System.Runtime.InteropServices;
using OpenSpace.Win32;

namespace OpenSpace.Input;

internal sealed class GlobalKeyboardHook : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc _callback;
    private IntPtr _hookId = IntPtr.Zero;

    public event EventHandler<KeyHookEventArgs>? KeyDown;
    public event EventHandler<KeyHookEventArgs>? KeyUp;

    public GlobalKeyboardHook()
    {
        _callback = HookCallback;
    }

    public void Install()
    {
        if (_hookId != IntPtr.Zero)
            return;

        // Using LibraryImport with SetWindowsHookEx requires module handle; for WH_KEYBOARD_LL
        // hMod must be NULL when dwThreadId is 0.
        _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _callback, IntPtr.Zero, 0);
        if (_hookId == IntPtr.Zero)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Failed to set keyboard hook");
    }

    public void Uninstall()
    {
        if (_hookId == IntPtr.Zero)
            return;

        NativeMethods.UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            bool alt = (kb.flags & 0x20) == 0x20;
            bool shift = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift;
            bool control = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control;

            var args = new KeyHookEventArgs((int)kb.vkCode, shift, alt, control);

            if (wParam == NativeMethods.WM_KEYDOWN || wParam == NativeMethods.WM_SYSKEYDOWN)
            {
                KeyDown?.Invoke(this, args);
            }
            else if (wParam == NativeMethods.WM_KEYUP || wParam == NativeMethods.WM_SYSKEYUP)
            {
                KeyUp?.Invoke(this, args);
            }

            if (args.Handled)
                return (IntPtr)1;
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Uninstall();
    }
}

internal sealed class KeyHookEventArgs : EventArgs
{
    public int VirtualKey { get; }
    public bool Shift { get; }
    public bool Alt { get; }
    public bool Control { get; }
    public bool Handled { get; set; }

    public KeyHookEventArgs(int virtualKey, bool shift, bool alt, bool control)
    {
        VirtualKey = virtualKey;
        Shift = shift;
        Alt = alt;
        Control = control;
    }
}
