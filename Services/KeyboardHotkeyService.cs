using SqwizzeySwitch.Helpers;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace SqwizzeySwitch.Services;

/// <summary>
/// Detects the standard input-language switch hotkeys (Win+Space and Alt+Shift) with a
/// low-level keyboard hook and raises <see cref="SwitchPressed"/>. This is the last-resort
/// trigger for console windows (conhost: PowerShell, cmd): their layout never reaches us
/// any other way (frozen window-thread HKL; no HSHELL_LANGUAGE; TSF sink never fires from
/// a background app). The consumer acts on it only for console foreground — every other
/// window is handled by the layout poll.
///
/// Must be constructed on the UI thread (the hook is delivered through its message pump).
/// The event is marshalled back via the dispatcher so the hook proc returns immediately and
/// never lags keyboard input.
/// </summary>
public sealed class KeyboardHotkeyService : IDisposable
{
    /// <summary>Fired when a layout-switch hotkey is pressed (Win+Space or Alt+Shift).</summary>
    public event Action? SwitchPressed;

    private const uint VK_LWIN = 0x5B, VK_RWIN = 0x5C, VK_SPACE = 0x20;
    private const uint VK_SHIFT = 0x10, VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1;
    private const uint VK_MENU  = 0x12, VK_LMENU  = 0xA4, VK_RMENU  = 0xA5; // Alt

    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private readonly Dispatcher _dispatcher;
    private IntPtr   _hook;
    private bool     _winDown, _altDown, _shiftDown;
    private DateTime _lastFire = DateTime.MinValue;
    private bool     _disposed;

    public KeyboardHotkeyService()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _proc = HookProc;
        var module = System.Diagnostics.Process.GetCurrentProcess().MainModule?.ModuleName;
        _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc,
            NativeMethods.GetModuleHandle(module), 0);
        if (_hook == IntPtr.Zero)
            Logger.Log("KeyboardHotkeyService: SetWindowsHookEx failed");
    }

    private static bool IsWin(uint vk)   => vk == VK_LWIN  || vk == VK_RWIN;
    private static bool IsAlt(uint vk)   => vk == VK_MENU  || vk == VK_LMENU  || vk == VK_RMENU;
    private static bool IsShift(uint vk) => vk == VK_SHIFT || vk == VK_LSHIFT || vk == VK_RSHIFT;

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && !_disposed)
        {
            try
            {
                int  msg = wParam.ToInt32();
                bool down = msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN;
                bool up   = msg == NativeMethods.WM_KEYUP   || msg == NativeMethods.WM_SYSKEYUP;
                uint vk   = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam).vkCode;

                if (IsWin(vk))        { if (down) _winDown = true;  else if (up) _winDown = false; }
                else if (IsAlt(vk))
                {
                    if (down) { if (!_altDown)   { _altDown = true;   if (_shiftDown) Fire(); } }
                    else if (up) _altDown = false;
                }
                else if (IsShift(vk))
                {
                    if (down) { if (!_shiftDown) { _shiftDown = true; if (_altDown)   Fire(); } }
                    else if (up) _shiftDown = false;
                }
                else if (vk == VK_SPACE && down && _winDown)
                {
                    Fire(); // Win+Space
                }
            }
            catch (Exception ex) { Logger.Log(ex, nameof(HookProc)); }
        }
        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    // Debounced; raised off the hook thread so the proc returns at once.
    private void Fire()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastFire).TotalMilliseconds < 250) return;
        _lastFire = now;
        _dispatcher.BeginInvoke(() => { if (!_disposed) SwitchPressed?.Invoke(); });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_hook != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
    }
}
