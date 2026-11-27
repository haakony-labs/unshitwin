using UnshitWindows.Config;
using UnshitWindows.Native;

namespace UnshitWindows.Switching;

/// <summary>
/// Cycles through the windows of the foreground app. While the shortcut's modifier is held,
/// repeated presses walk a fixed snapshot; releasing it ends the session, so the next press
/// starts from the new z-order (a single tap toggles between the two most recent windows).
/// </summary>
internal sealed class SameAppSwitcher : IDisposable
{
    private readonly System.Windows.Forms.Timer _releaseTimer = new() { Interval = 25 };
    private CycleSession? _session;
    private HotkeyModifiers _held;

    public SameAppSwitcher() => _releaseTimer.Tick += (_, _) =>
    {
        if (!AreAllDown(_held)) EndSession();
    };

    public bool IncludeMinimized { get; set; } = true;

    public void OnHotkey(bool forward, HotkeyModifiers bindingModifiers)
    {
        if (_session == null && !StartSession(bindingModifiers)) return;

        nint target = _session!.Step(forward, NativeMethods.IsWindow);
        if (target != 0)
        {
            WindowActivator.Activate(target);
            if ((_held & HotkeyModifiers.Alt) != 0) Input.TapMenuMaskKey();
        }

        // Without a modifier to hold there is no way to tell when a session ends.
        if (_held == HotkeyModifiers.None) EndSession();
    }

    public void EndSession()
    {
        _releaseTimer.Stop();
        _session = null;
    }

    private bool StartSession(HotkeyModifiers bindingModifiers)
    {
        nint foreground = NativeMethods.GetForegroundWindow();
        if (foreground == 0) return false;

        var windows = WindowEnumerator.GetSameAppWindows(NativeMethods.GetRootOwner(foreground), IncludeMinimized);
        if (windows.Count < 2) return false;

        _session = new CycleSession(windows);
        // Shift is the usual "go backwards" modifier, so letting go of it doesn't end the session.
        _held = bindingModifiers & ~HotkeyModifiers.Shift;
        if (_held == HotkeyModifiers.None) _held = bindingModifiers;
        if (_held != HotkeyModifiers.None) _releaseTimer.Start();
        return true;
    }

    private static bool AreAllDown(HotkeyModifiers modifiers) =>
        ((modifiers & HotkeyModifiers.Alt) == 0 || Input.IsDown(NativeMethods.VK_MENU)) &&
        ((modifiers & HotkeyModifiers.Control) == 0 || Input.IsDown(NativeMethods.VK_CONTROL)) &&
        ((modifiers & HotkeyModifiers.Shift) == 0 || Input.IsDown(NativeMethods.VK_SHIFT)) &&
        ((modifiers & HotkeyModifiers.Win) == 0 || Input.AnyDown(NativeMethods.VK_LWIN, NativeMethods.VK_RWIN));

    public void Dispose()
    {
        EndSession();
        _releaseTimer.Dispose();
    }
}
