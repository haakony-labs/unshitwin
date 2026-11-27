using System.Runtime.InteropServices;
using UnshitWindows.Config;
using UnshitWindows.Native;

namespace UnshitWindows;

/// <summary>The value doubles as the RegisterHotKey id.</summary>
internal enum HotkeyAction
{
    NextWindow = 1,
    PreviousWindow = 2,
    ClipboardPopup = 3,
}

/// <summary>Registers the configured shortcuts with Windows.</summary>
internal sealed class HotkeyManager(nint hwnd)
{
    private readonly HashSet<HotkeyAction> _registered = [];

    public static string Describe(HotkeyAction action) => action switch
    {
        HotkeyAction.NextWindow => "Next window of same app",
        HotkeyAction.PreviousWindow => "Previous window of same app",
        HotkeyAction.ClipboardPopup => "Open clipboard history",
        _ => action.ToString(),
    };

    public static Hotkey Get(AppConfig config, HotkeyAction action) => action switch
    {
        HotkeyAction.NextWindow => config.NextWindowHotkey,
        HotkeyAction.PreviousWindow => config.PreviousWindowHotkey,
        HotkeyAction.ClipboardPopup => config.ClipboardHotkey,
        _ => Hotkey.None,
    };

    private static bool IsFeatureEnabled(AppConfig config, HotkeyAction action) =>
        action == HotkeyAction.ClipboardPopup ? config.ClipboardHistoryEnabled : config.WindowSwitchingEnabled;

    /// <summary>Unregisters everything, then registers the bindings of the enabled features.
    /// Bindings that succeed stay registered; failures are returned with a reason.</summary>
    public Dictionary<HotkeyAction, string> RegisterAll(AppConfig config)
    {
        UnregisterAll();
        var failures = new Dictionary<HotkeyAction, string>();
        foreach (var action in Enum.GetValues<HotkeyAction>())
        {
            var hotkey = Get(config, action);
            if (hotkey.IsEmpty || !IsFeatureEnabled(config, action)) continue;

            if (NativeMethods.RegisterHotKey(hwnd, (int)action, (uint)hotkey.Modifiers | NativeMethods.MOD_NOREPEAT, (uint)hotkey.Key))
            {
                _registered.Add(action);
                continue;
            }
            int error = Marshal.GetLastPInvokeError();
            failures[action] = error == NativeMethods.ERROR_HOTKEY_ALREADY_REGISTERED
                ? $"{hotkey.ToDisplayString()} is already taken by Windows or another program."
                : $"Windows refused {hotkey.ToDisplayString()} (error {error}).";
        }
        return failures;
    }

    public void UnregisterAll()
    {
        foreach (var action in _registered) NativeMethods.UnregisterHotKey(hwnd, (int)action);
        _registered.Clear();
    }

    /// <summary>Two actions bound to the same shortcut (checked whether or not the feature is enabled).</summary>
    public static Dictionary<HotkeyAction, string> FindDuplicates(AppConfig config)
    {
        var result = new Dictionary<HotkeyAction, string>();
        var bound = Enum.GetValues<HotkeyAction>()
            .Select(a => (Action: a, Hotkey: Get(config, a)))
            .Where(x => !x.Hotkey.IsEmpty)
            .ToList();
        foreach (var (action, hotkey) in bound)
        {
            var other = bound.FirstOrDefault(x => x.Action != action && x.Hotkey == hotkey);
            if (other.Action != 0)
                result[action] = $"Same shortcut as \"{Describe(other.Action)}\".";
        }
        return result;
    }

    /// <summary>Switches to <paramref name="next"/>'s bindings. On any problem nothing changes:
    /// <paramref name="current"/>'s bindings are restored and the problems are returned.</summary>
    public Dictionary<HotkeyAction, string> TryApply(AppConfig next, AppConfig current)
    {
        var duplicates = FindDuplicates(next);
        if (duplicates.Count > 0) return duplicates;

        var failures = RegisterAll(next);
        if (failures.Count > 0) RegisterAll(current);
        return failures;
    }
}
