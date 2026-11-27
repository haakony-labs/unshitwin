using UnshitWindows.Native;

namespace UnshitWindows;

/// <summary>Hidden message-only window that receives WM_HOTKEY, WM_CLIPBOARDUPDATE and
/// the "open Settings" request from a second launch of the exe.</summary>
internal sealed class MessageWindow : NativeWindow, IDisposable
{
    private const string Caption = "UnshitWindows.MessageWindow";
    private const int WM_APP_SHOW_SETTINGS = 0x8000 + 1;
    private static readonly nint HWND_MESSAGE = -3;

    public event Action<int>? HotkeyPressed;
    public event Action? ClipboardUpdated;
    public event Action? ShowSettingsRequested;

    public MessageWindow() =>
        CreateHandle(new CreateParams { Caption = Caption, Parent = HWND_MESSAGE });

    /// <summary>Asks an already running instance to open its Settings window.</summary>
    public static bool SignalRunningInstance()
    {
        nint hwnd = NativeMethods.FindWindowEx(HWND_MESSAGE, 0, null, Caption);
        if (hwnd == 0) return false;
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        NativeMethods.AllowSetForegroundWindow(pid); // let it bring Settings to the front
        return NativeMethods.PostMessage(hwnd, WM_APP_SHOW_SETTINGS, 0, 0);
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case NativeMethods.WM_HOTKEY:
                HotkeyPressed?.Invoke((int)m.WParam);
                return;
            case NativeMethods.WM_CLIPBOARDUPDATE:
                ClipboardUpdated?.Invoke();
                return;
            case WM_APP_SHOW_SETTINGS:
                ShowSettingsRequested?.Invoke();
                return;
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (Handle != 0) DestroyHandle();
    }
}
