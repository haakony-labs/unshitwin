using System.Text.Json.Serialization;
using UnshitWindows.Native;

namespace UnshitWindows.Config;

internal enum PopupPosition
{
    /// <summary>At the text caret when the target app exposes one, otherwise at the mouse.</summary>
    Caret,
    Mouse,
}

/// <summary>Everything the user can configure. Missing JSON fields keep these defaults.</summary>
internal sealed class AppConfig
{
    public const int MinHistorySize = 5;
    public const int MaxHistorySize = 30;

    public Hotkey NextWindowHotkey { get; set; } = Defaults.NextWindow;
    public Hotkey PreviousWindowHotkey { get; set; } = Defaults.PreviousWindow;
    public Hotkey ClipboardHotkey { get; set; } = Defaults.ClipboardPopup;

    public bool WindowSwitchingEnabled { get; set; } = true;
    public bool IncludeMinimizedWindows { get; set; } = true;

    public bool ClipboardHistoryEnabled { get; set; } = true;
    public int HistorySize { get; set; } = 10;
    public bool KeepImages { get; set; } = true;
    public bool KeepFiles { get; set; } = true;
    public PopupPosition PopupPosition { get; set; } = PopupPosition.Caret;
    public bool PasteAfterPick { get; set; } = true;

    public AppConfig Clone() => (AppConfig)MemberwiseClone();

    public void Normalize() => HistorySize = Math.Clamp(HistorySize, MinHistorySize, MaxHistorySize);
}

internal static class Defaults
{
    /// <summary>The key physically above Tab (scan code 0x29) in the current layout:
    /// VK_OEM_5 ('|') on Norwegian, VK_OEM_3 ('`') on US.</summary>
    public static Keys AboveTabKey { get; } = ResolveAboveTabKey();

    public static Hotkey NextWindow => new(HotkeyModifiers.Alt, AboveTabKey);
    public static Hotkey PreviousWindow => new(HotkeyModifiers.Alt | HotkeyModifiers.Shift, AboveTabKey);
    public static Hotkey ClipboardPopup => new(HotkeyModifiers.Control | HotkeyModifiers.Shift, Keys.V);

    private static Keys ResolveAboveTabKey()
    {
        uint vk = NativeMethods.MapVirtualKeyEx(0x29, NativeMethods.MAPVK_VSC_TO_VK, NativeMethods.GetKeyboardLayout(0));
        return vk != 0 ? (Keys)vk : Keys.Oem3;
    }
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppConfig))]
internal sealed partial class ConfigJsonContext : JsonSerializerContext;
