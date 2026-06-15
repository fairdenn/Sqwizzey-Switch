using SqwizzeySwitch.Helpers;

namespace SqwizzeySwitch.Services;

/// <summary>
/// Raises <see cref="ForegroundChanged"/> whenever a different window becomes the
/// foreground window. Uses a WinEvent hook (EVENT_SYSTEM_FOREGROUND) so the OS
/// pushes us the change instantly — no polling. Events from our own process are
/// filtered out by WINEVENT_SKIPOWNPROCESS, so the overlay and settings windows
/// never trigger it.
///
/// Must be constructed on the UI thread: with WINEVENT_OUTOFCONTEXT the callback
/// is delivered through that thread's message queue, which the WPF Dispatcher pumps.
/// </summary>
public sealed class ForegroundChangeService : IDisposable
{
    /// <summary>Fired with the handle of the newly-focused window.</summary>
    public event Action<IntPtr>? ForegroundChanged;

    private IntPtr _hook;
    // The delegate must be held in a field — if it's collected the native callback
    // dangles and the process crashes.
    private readonly NativeMethods.WinEventDelegate _proc;
    private bool _disposed;

    public ForegroundChangeService()
    {
        _proc = OnWinEvent;
        _hook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _proc,
            idProcess: 0, idThread: 0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

        if (_hook == IntPtr.Zero)
            Logger.Log("ForegroundChangeService: SetWinEventHook returned NULL");
    }

    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // idObject == 0 (OBJID_WINDOW) means the event is about the window itself,
        // not a child control. Ignore everything else.
        if (_disposed || hwnd == IntPtr.Zero || idObject != 0) return;

        try { ForegroundChanged?.Invoke(hwnd); }
        catch (Exception ex) { Logger.Log(ex, nameof(OnWinEvent)); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
