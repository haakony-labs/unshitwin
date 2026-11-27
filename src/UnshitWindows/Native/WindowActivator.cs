using static UnshitWindows.Native.NativeMethods;

namespace UnshitWindows.Native;

internal static class WindowActivator
{
    /// <summary>Restores (if minimized) and brings a top-level window to the foreground.</summary>
    public static void Activate(nint hwnd)
    {
        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
        if (SetForegroundWindow(hwnd) && GetForegroundWindow() == hwnd) return;

        // Fallback: share input state with the current foreground thread, which lifts
        // the foreground lock for this call.
        nint foreground = GetForegroundWindow();
        uint foregroundThread = foreground != 0 ? GetWindowThreadProcessId(foreground, out _) : 0;
        uint currentThread = GetCurrentThreadId();
        bool attached = foregroundThread != 0 && foregroundThread != currentThread
                        && AttachThreadInput(currentThread, foregroundThread, true);
        try
        {
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached) AttachThreadInput(currentThread, foregroundThread, false);
        }
    }

    /// <summary>True when <paramref name="hwnd"/> (or a window it owns) has the foreground.</summary>
    public static bool IsForeground(nint hwnd)
    {
        nint foreground = GetForegroundWindow();
        return foreground == hwnd || (foreground != 0 && GetRootOwner(foreground) == GetRootOwner(hwnd));
    }
}
